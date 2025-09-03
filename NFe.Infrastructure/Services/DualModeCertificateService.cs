using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NFe.Core.Interfaces;
using NFe.Core.Models;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace NFe.Infrastructure.Services
{
    /// <summary>
    /// Implementação dual-mode do serviço de certificados digitais.
    /// PRODUÇÃO: Carrega certificado A1 real do AWS Secrets Manager
    /// OUTROS AMBIENTES: Gera certificado autoassinado para desenvolvimento/teste
    /// </summary>
    public class DualModeCertificateService : ICertificateService, IDisposable
    {
        private readonly IAmazonSecretsManager _secretsManager;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ILogger<DualModeCertificateService> _logger;
        private readonly SemaphoreSlim _certificateLock;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(1);
        
        private X509Certificate2? _cachedCertificate;
        private DateTime _cacheTime = DateTime.MinValue;
        private bool _disposed;

        public DualModeCertificateService(
            IAmazonSecretsManager secretsManager,
            IConfiguration configuration,
            IHostEnvironment hostEnvironment,
            ILogger<DualModeCertificateService> logger)
        {
            _secretsManager = secretsManager ?? throw new ArgumentNullException(nameof(secretsManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _certificateLock = new SemaphoreSlim(1, 1);
        }

        public async Task<X509Certificate2> ObterCertificadoAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DualModeCertificateService));

            await _certificateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Verifica se há certificado em cache válido
                if (_cachedCertificate != null && 
                    DateTime.UtcNow - _cacheTime < _cacheExpiry &&
                    await ValidarCertificadoInternoAsync(_cachedCertificate))
                {
                    _logger.LogDebug("Certificado obtido do cache ({Modo})", 
                        _hostEnvironment.IsProduction() ? "Produção" : "Desenvolvimento");
                    return _cachedCertificate;
                }

                X509Certificate2 certificado;

                if (_hostEnvironment.IsProduction())
                {
                    _logger.LogInformation("MODO PRODUÇÃO: Carregando certificado A1 real do AWS Secrets Manager");
                    certificado = await LoadProductionCertificateAsync();
                }
                else
                {
                    _logger.LogInformation("MODO DESENVOLVIMENTO: Gerando certificado autoassinado para testes");
                    _logger.LogWarning("ATENÇÃO: Usando certificado autoassinado - apenas para desenvolvimento/teste!");
                    certificado = GenerateSelfSignedCertificate();
                }

                if (!await ValidarCertificadoInternoAsync(certificado))
                {
                    certificado.Dispose();
                    throw new InvalidOperationException($"Certificado obtido ({GetCurrentMode()}) é inválido");
                }

                // Limpa certificado anterior do cache
                _cachedCertificate?.Dispose();
                _cachedCertificate = certificado;
                _cacheTime = DateTime.UtcNow;

                var certInfo = ExtrairInformacoesCertificado(certificado);
                _logger.LogInformation("Certificado carregado com sucesso ({Modo}). Subject: {SubjectName}, Expires: {NotAfter}, Days until expiration: {DaysUntilExpiration}", 
                    GetCurrentMode(), certInfo.SubjectName, certInfo.NotAfter, certInfo.DaysUntilExpiration);

                if (_hostEnvironment.IsProduction() && certInfo.IsExpiringSoon)
                {
                    _logger.LogWarning("ATENÇÃO: Certificado A1 expira em {DaysUntilExpiration} dias", certInfo.DaysUntilExpiration);
                }

                return certificado;
            }
            finally
            {
                _certificateLock.Release();
            }
        }

        public async Task<bool> ValidarCertificadoAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DualModeCertificateService));

            try
            {
                var certificado = await ObterCertificadoAsync();
                return await ValidarCertificadoInternoAsync(certificado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar certificado ({Modo})", GetCurrentMode());
                return false;
            }
        }

        public async Task<byte[]> AssinarXmlAsync(string xmlContent)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DualModeCertificateService));

            if (string.IsNullOrWhiteSpace(xmlContent))
                throw new ArgumentNullException(nameof(xmlContent));

            try
            {
                _logger.LogDebug("Iniciando assinatura digital do XML ({Modo})", GetCurrentMode());
                
                var certificado = await ObterCertificadoAsync();
                
                var xmlDoc = new XmlDocument { PreserveWhitespace = true };
                xmlDoc.LoadXml(xmlContent);

                var signedXml = new SignedXml(xmlDoc)
                {
                    SigningKey = certificado.GetRSAPrivateKey() ?? throw new InvalidOperationException("Certificado não possui chave privada RSA")
                };

                // Configura referência para o documento inteiro
                var reference = new Reference("")
                {
                    DigestMethod = SignedXml.XmlDsigSHA256Url
                };
                reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
                reference.AddTransform(new XmlDsigC14NTransform());

                signedXml.AddReference(reference);

                // Configura informações do certificado na assinatura
                var keyInfo = new KeyInfo();
                keyInfo.AddClause(new KeyInfoX509Data(certificado));
                signedXml.KeyInfo = keyInfo;

                // Assina o documento
                signedXml.ComputeSignature();

                // Adiciona a assinatura ao documento
                var xmlSignature = signedXml.GetXml();
                xmlDoc.DocumentElement!.AppendChild(xmlDoc.ImportNode(xmlSignature, true));

                // Converte para bytes
                var xmlBytes = Encoding.UTF8.GetBytes(xmlDoc.OuterXml);
                
                _logger.LogDebug("Assinatura digital concluída com sucesso ({Modo}). Tamanho do XML assinado: {Size} bytes", 
                    GetCurrentMode(), xmlBytes.Length);
                
                return xmlBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao assinar XML digitalmente ({Modo})", GetCurrentMode());
                throw new InvalidOperationException("Não foi possível assinar o XML digitalmente", ex);
            }
        }

        public async Task<string> ObterCertificateThumbprintAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DualModeCertificateService));

            var certificado = await ObterCertificadoAsync();
            return certificado.Thumbprint;
        }

        public async Task RenovarCacheAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DualModeCertificateService));

            await _certificateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _logger.LogInformation("Renovando cache de certificado forçadamente ({Modo})", GetCurrentMode());
                
                _cachedCertificate?.Dispose();
                _cachedCertificate = null;
                _cacheTime = DateTime.MinValue;
                
                // Força recarregamento
                await ObterCertificadoAsync();
            }
            finally
            {
                _certificateLock.Release();
            }
        }

        /// <summary>
        /// Carrega certificado A1 real do AWS Secrets Manager para produção
        /// </summary>
        private async Task<X509Certificate2> LoadProductionCertificateAsync()
        {
            var certificateData = await ObterCertificadoDoSecretsManagerAsync();
            return X509CertificateLoader.LoadPkcs12(
                certificateData.PfxData, 
                ConvertSecureStringToString(certificateData.Password), 
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
        }

        /// <summary>
        /// Gera certificado autoassinado para desenvolvimento/teste
        /// Especificações: CN=NFe Test Certificate, RSA 2048, 365 dias, Digital Signature + Document Signing
        /// </summary>
        private X509Certificate2 GenerateSelfSignedCertificate()
        {
            try
            {
                _logger.LogDebug("Gerando certificado autoassinado RSA 2048 bits");

                // Gera par de chaves RSA 2048 bits
                using var rsa = RSA.Create(2048);
                
                var subjectName = _configuration["Certificate:SubjectName"] ?? "CN=NFe Development Certificate";
                
                // Cria solicitação de certificado
                var certRequest = new CertificateRequest(
                    new X500DistinguishedName(subjectName),
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                // Adiciona extensões X.509
                
                // Key Usage: Digital Signature
                certRequest.CertificateExtensions.Add(
                    new X509KeyUsageExtension(
                        X509KeyUsageFlags.DigitalSignature,
                        critical: true));

                // Enhanced Key Usage: Document Signing (OID 1.3.6.1.5.5.7.3.36)
                certRequest.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection 
                        { 
                            new Oid("1.3.6.1.5.5.7.3.36", "Document Signing"),
                            new Oid("1.3.6.1.5.5.7.3.2", "Client Authentication")
                        },
                        critical: true));

                // Basic Constraints: Not a CA
                certRequest.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(
                        certificateAuthority: false,
                        hasPathLengthConstraint: false,
                        pathLengthConstraint: 0,
                        critical: true));

                // Subject Key Identifier
                certRequest.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(
                        certRequest.PublicKey,
                        critical: false));

                // Período de validade: 365 dias
                var notBefore = DateTimeOffset.UtcNow;
                var notAfter = notBefore.AddDays(365);

                // Cria certificado autoassinado
                var certificate = certRequest.CreateSelfSigned(notBefore, notAfter);

                // Converte para formato persistente com chave privada
                var pfxBytes = certificate.Export(X509ContentType.Pfx, (string?)null);
                var persistentCert = X509CertificateLoader.LoadPkcs12(pfxBytes, (string?)null, 
                    X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                _logger.LogInformation("Certificado autoassinado gerado com sucesso. Subject: {Subject}, NotAfter: {NotAfter}, Thumbprint: {Thumbprint}",
                    persistentCert.Subject, persistentCert.NotAfter, persistentCert.Thumbprint);

                certificate.Dispose();
                return persistentCert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar certificado autoassinado");
                throw new InvalidOperationException("Não foi possível gerar certificado autoassinado para desenvolvimento", ex);
            }
        }

        private async Task<CertificateData> ObterCertificadoDoSecretsManagerAsync()
        {
            var secretName = _configuration["Certificate:SecretName"] ?? _configuration["AWS:SecretsManager:CertificateSecretName"];
            var passwordSecretName = _configuration["Certificate:PasswordSecretName"] ?? _configuration["AWS:SecretsManager:CertificatePasswordSecretName"];

            if (string.IsNullOrEmpty(secretName) || string.IsNullOrEmpty(passwordSecretName))
            {
                throw new InvalidOperationException("Configurações de certificado A1 não encontradas para produção");
            }

            try
            {
                _logger.LogDebug("Obtendo certificado A1 do AWS Secrets Manager: {SecretName}", secretName);

                var certificateRequest = new GetSecretValueRequest { SecretId = secretName };
                var certificateResponse = await _secretsManager.GetSecretValueAsync(certificateRequest);

                var passwordRequest = new GetSecretValueRequest { SecretId = passwordSecretName };
                var passwordResponse = await _secretsManager.GetSecretValueAsync(passwordRequest);

                if (certificateResponse.SecretBinary?.Length == 0)
                {
                    throw new InvalidOperationException("Dados do certificado A1 estão vazios no AWS Secrets Manager");
                }

                if (string.IsNullOrEmpty(passwordResponse.SecretString))
                {
                    throw new InvalidOperationException("Senha do certificado A1 está vazia no AWS Secrets Manager");
                }

                return new CertificateData
                {
                    PfxData = certificateResponse.SecretBinary?.ToArray() ?? throw new InvalidOperationException("Dados do certificado A1 estão nulos"),
                    Password = ConvertToSecureString(passwordResponse.SecretString)
                };
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                _logger.LogError(ex, "Erro ao obter certificado A1 do AWS Secrets Manager");
                throw new InvalidOperationException("Não foi possível obter o certificado A1 do AWS Secrets Manager", ex);
            }
        }

        private async Task<bool> ValidarCertificadoInternoAsync(X509Certificate2 certificado)
        {
            if (certificado == null) return false;

            try
            {
                // Verifica validade temporal
                var agora = DateTime.UtcNow;
                if (agora < certificado.NotBefore || agora > certificado.NotAfter)
                {
                    _logger.LogWarning("Certificado fora do período de validade ({Modo}). NotBefore: {NotBefore}, NotAfter: {NotAfter}, Now: {Now}",
                        GetCurrentMode(), certificado.NotBefore, certificado.NotAfter, agora);
                    return false;
                }

                // Verifica se possui chave privada
                if (!certificado.HasPrivateKey)
                {
                    _logger.LogWarning("Certificado não possui chave privada ({Modo})", GetCurrentMode());
                    return false;
                }

                // Validações específicas por ambiente
                if (_hostEnvironment.IsProduction())
                {
                    // Permite relaxar validação de produção em cenários de teste via configuração
                    var relaxValidation = _configuration["Certificate:RelaxValidation"];
                    if (string.Equals(relaxValidation, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Relaxando validação de certificado A1 (apenas para testes)");
                        return ValidarCertificadoDesenvolvimentoAsync(certificado);
                    }

                    return await ValidarCertificadoProducaoAsync(certificado);
                }
                else
                {
                    return ValidarCertificadoDesenvolvimentoAsync(certificado);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante validação interna do certificado ({Modo})", GetCurrentMode());
                return false;
            }
        }

        /// <summary>
        /// Validações rigorosas para certificado A1 real em produção
        /// </summary>
        private Task<bool> ValidarCertificadoProducaoAsync(X509Certificate2 certificado)
        {
            // Verifica cadeia de certificados com validação rigorosa
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag; // Validação rigorosa

            var isChainValid = chain.Build(certificado);
            if (!isChainValid)
            {
                var chainErrors = chain.ChainStatus
                    .Select(status => $"{status.Status}: {status.StatusInformation}")
                    .ToList();
                
                _logger.LogError("Falhas na cadeia de certificados A1 (PRODUÇÃO): {ChainErrors}", string.Join("; ", chainErrors));
                return Task.FromResult(false);
            }

            // Verifica se é certificado A1 válido (ICP-Brasil)
            var issuer = certificado.Issuer;
            if (!issuer.Contains("ICP-Brasil", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Certificado não é ICP-Brasil válido. Issuer: {Issuer}", issuer);
            }

            _logger.LogInformation("Certificado A1 validado com sucesso para produção");
            return Task.FromResult(true);
        }

        /// <summary>
        /// Validações básicas para certificado autoassinado em desenvolvimento
        /// </summary>
        private bool ValidarCertificadoDesenvolvimentoAsync(X509Certificate2 certificado)
        {
            // Para desenvolvimento, apenas verificações básicas
            var isValid = certificado.HasPrivateKey && 
                          DateTime.UtcNow >= certificado.NotBefore && 
                          DateTime.UtcNow <= certificado.NotAfter;

            if (isValid)
            {
                _logger.LogDebug("Certificado autoassinado validado para desenvolvimento");
            }
            else
            {
                _logger.LogWarning("Certificado autoassinado inválido para desenvolvimento");
            }

            return isValid;
        }

        private static CertificateInfo ExtrairInformacoesCertificado(X509Certificate2 certificado)
        {
            return new CertificateInfo
            {
                Thumbprint = certificado.Thumbprint,
                SubjectName = certificado.Subject,
                IssuerName = certificado.Issuer,
                NotBefore = certificado.NotBefore.ToUniversalTime(),
                NotAfter = certificado.NotAfter.ToUniversalTime(),
                SerialNumber = certificado.SerialNumber
            };
        }

        private static SecureString ConvertToSecureString(string password)
        {
            var secureString = new SecureString();
            foreach (char c in password)
            {
                secureString.AppendChar(c);
            }
            secureString.MakeReadOnly();
            return secureString;
        }

        private string GetCurrentMode()
        {
            return _hostEnvironment.IsProduction() ? "Produção" : "Desenvolvimento";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cachedCertificate?.Dispose();
                _certificateLock?.Dispose();
                _disposed = true;
            }
        }

        private static string ConvertSecureStringToString(SecureString secureString)
        {
            if (secureString == null) return string.Empty;
            
            var unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return System.Runtime.InteropServices.Marshal.PtrToStringUni(unmanagedString) ?? string.Empty;
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
                }
            }
        }

        private class CertificateData
        {
            public required byte[] PfxData { get; init; }
            public required SecureString Password { get; init; }
        }
    }
}
