using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
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
    /// Implementação do serviço de certificados digitais A1 com integração AWS Secrets Manager.
    /// </summary>
    public class CertificateService : ICertificateService, IDisposable
    {
        private readonly IAmazonSecretsManager _secretsManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CertificateService> _logger;
        private readonly SemaphoreSlim _certificateLock;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(1);
        
        private X509Certificate2? _cachedCertificate;
        private DateTime _cacheTime = DateTime.MinValue;
        private bool _disposed;

        public CertificateService(
            IAmazonSecretsManager secretsManager,
            IConfiguration configuration,
            ILogger<CertificateService> logger)
        {
            _secretsManager = secretsManager ?? throw new ArgumentNullException(nameof(secretsManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _certificateLock = new SemaphoreSlim(1, 1);
        }

        public async Task<X509Certificate2> ObterCertificadoAsync()
        {
            ObjectDisposedException.ThrowIfDisposed(_disposed, this);

            await _certificateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Verifica se há certificado em cache válido
                if (_cachedCertificate != null && 
                    DateTime.UtcNow - _cacheTime < _cacheExpiry &&
                    await ValidarCertificadoInternoAsync(_cachedCertificate))
                {
                    _logger.LogDebug("Certificado obtido do cache");
                    return _cachedCertificate;
                }

                _logger.LogInformation("Obtendo certificado do AWS Secrets Manager");
                
                var certificateData = await ObterCertificadoDoSecretsManagerAsync();
                var certificado = new X509Certificate2(
                    certificateData.PfxData, 
                    certificateData.Password, 
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

                if (!await ValidarCertificadoInternoAsync(certificado))
                {
                    certificado.Dispose();
                    throw new InvalidOperationException("Certificado obtido do Secrets Manager é inválido");
                }

                // Limpa certificado anterior do cache
                _cachedCertificate?.Dispose();
                _cachedCertificate = certificado;
                _cacheTime = DateTime.UtcNow;

                var certInfo = ExtrairInformacoesCertificado(certificado);
                _logger.LogInformation("Certificado carregado com sucesso. Subject: {SubjectName}, Expires: {NotAfter}, Days until expiration: {DaysUntilExpiration}", 
                    certInfo.SubjectName, certInfo.NotAfter, certInfo.DaysUntilExpiration);

                if (certInfo.IsExpiringSoon)
                {
                    _logger.LogWarning("ATENÇÃO: Certificado expira em {DaysUntilExpiration} dias", certInfo.DaysUntilExpiration);
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
            ObjectDisposedException.ThrowIfDisposed(_disposed, this);

            try
            {
                var certificado = await ObterCertificadoAsync();
                return await ValidarCertificadoInternoAsync(certificado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar certificado");
                return false;
            }
        }

        public async Task<byte[]> AssinarXmlAsync(string xmlContent)
        {
            ObjectDisposedException.ThrowIfDisposed(_disposed, this);

            if (string.IsNullOrWhiteSpace(xmlContent))
                throw new ArgumentNullException(nameof(xmlContent));

            try
            {
                _logger.LogDebug("Iniciando assinatura digital do XML");
                
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
                
                _logger.LogDebug("Assinatura digital concluída com sucesso. Tamanho do XML assinado: {Size} bytes", xmlBytes.Length);
                
                return xmlBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao assinar XML digitalmente");
                throw new InvalidOperationException("Não foi possível assinar o XML digitalmente", ex);
            }
        }

        public async Task<string> ObterCertificateThumbprintAsync()
        {
            ObjectDisposedException.ThrowIfDisposed(_disposed, this);

            var certificado = await ObterCertificadoAsync();
            return certificado.Thumbprint;
        }

        public async Task RenovarCacheAsync()
        {
            ObjectDisposedException.ThrowIfDisposed(_disposed, this);

            await _certificateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _logger.LogInformation("Renovando cache de certificado forçadamente");
                
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

        private async Task<CertificateData> ObterCertificadoDoSecretsManagerAsync()
        {
            var secretName = _configuration["AWS:SecretsManager:CertificateSecretName"];
            var passwordSecretName = _configuration["AWS:SecretsManager:CertificatePasswordSecretName"];

            if (string.IsNullOrEmpty(secretName) || string.IsNullOrEmpty(passwordSecretName))
            {
                throw new InvalidOperationException("Configurações de certificado não encontradas no appsettings");
            }

            try
            {
                _logger.LogDebug("Obtendo certificado do Secrets Manager: {SecretName}", secretName);

                var certificateRequest = new GetSecretValueRequest { SecretId = secretName };
                var certificateResponse = await _secretsManager.GetSecretValueAsync(certificateRequest);

                var passwordRequest = new GetSecretValueRequest { SecretId = passwordSecretName };
                var passwordResponse = await _secretsManager.GetSecretValueAsync(passwordRequest);

                if (certificateResponse.SecretBinary?.Length == 0)
                {
                    throw new InvalidOperationException("Dados do certificado estão vazios no Secrets Manager");
                }

                if (string.IsNullOrEmpty(passwordResponse.SecretString))
                {
                    throw new InvalidOperationException("Senha do certificado está vazia no Secrets Manager");
                }

                return new CertificateData
                {
                    PfxData = certificateResponse.SecretBinary.ToArray(),
                    Password = ConvertToSecureString(passwordResponse.SecretString)
                };
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                _logger.LogError(ex, "Erro ao obter certificado do AWS Secrets Manager");
                throw new InvalidOperationException("Não foi possível obter o certificado do AWS Secrets Manager", ex);
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
                    _logger.LogWarning("Certificado fora do período de validade. NotBefore: {NotBefore}, NotAfter: {NotAfter}, Now: {Now}",
                        certificado.NotBefore, certificado.NotAfter, agora);
                    return false;
                }

                // Verifica se possui chave privada
                if (!certificado.HasPrivateKey)
                {
                    _logger.LogWarning("Certificado não possui chave privada");
                    return false;
                }

                // Verifica cadeia de certificados
                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

                var isChainValid = chain.Build(certificado);
                if (!isChainValid)
                {
                    var chainErrors = chain.ChainStatus
                        .Select(status => $"{status.Status}: {status.StatusInformation}")
                        .ToList();
                    
                    _logger.LogWarning("Problemas na cadeia de certificados: {ChainErrors}", string.Join("; ", chainErrors));
                    
                    // Para certificados de teste, algumas falhas de cadeia são aceitáveis
                    var acceptableErrors = new[]
                    {
                        X509ChainStatusFlags.UntrustedRoot,
                        X509ChainStatusFlags.PartialChain
                    };
                    
                    var hasOnlyAcceptableErrors = chain.ChainStatus
                        .All(status => acceptableErrors.Contains(status.Status));
                    
                    if (!hasOnlyAcceptableErrors)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante validação interna do certificado");
                return false;
            }
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

        public void Dispose()
        {
            if (!_disposed)
            {
                _cachedCertificate?.Dispose();
                _certificateLock?.Dispose();
                _disposed = true;
            }
        }

        private class CertificateData
        {
            public required byte[] PfxData { get; init; }
            public required SecureString Password { get; init; }
        }
    }
}