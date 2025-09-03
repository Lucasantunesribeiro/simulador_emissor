using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NFe.Core.Interfaces;
using NFe.Infrastructure.Services;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace NFe.Tests.Services
{
    public class CertificateServiceTests : IDisposable
    {
        private readonly Mock<IAmazonSecretsManager> _secretsManagerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<CertificateService>> _loggerMock;
        private readonly ICertificateService _certificateService;
        private readonly X509Certificate2 _testCertificate;

        public CertificateServiceTests()
        {
            _secretsManagerMock = new Mock<IAmazonSecretsManager>();
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<CertificateService>>();

            // Configura mock de configuration
            _configurationMock.Setup(c => c["AWS:SecretsManager:CertificateSecretName"])
                .Returns("test-certificate-secret");
            _configurationMock.Setup(c => c["AWS:SecretsManager:CertificatePasswordSecretName"])
                .Returns("test-certificate-password-secret");

            // Cria certificado de teste
            _testCertificate = CreateTestCertificate();

            _certificateService = new CertificateService(
                _secretsManagerMock.Object,
                _configurationMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task ObterCertificadoAsync_DeveRetornarCertificadoValido()
        {
            // Arrange
            var pfxData = _testCertificate.Export(X509ContentType.Pfx, "test123");
            
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretBinary = new MemoryStream(pfxData) });
            
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-password-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = "test123" });

            // Act
            var resultado = await _certificateService.ObterCertificadoAsync();

            // Assert
            resultado.Should().NotBeNull();
            resultado.HasPrivateKey.Should().BeTrue();
            resultado.Thumbprint.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ObterCertificadoAsync_QuandoCertificadoSecretVazio_DeveLancarExcecao()
        {
            // Arrange
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretBinary = new MemoryStream() });

            // Act & Assert
            var action = async () => await _certificateService.ObterCertificadoAsync();
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Dados do certificado estão vazios*");
        }

        [Fact]
        public async Task ObterCertificadoAsync_QuandoSenhaCertificadoVazio_DeveLancarExcecao()
        {
            // Arrange
            var pfxData = _testCertificate.Export(X509ContentType.Pfx, "test123");
            
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretBinary = new MemoryStream(pfxData) });
            
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-password-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = "" });

            // Act & Assert
            var action = async () => await _certificateService.ObterCertificadoAsync();
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Senha do certificado está vazia*");
        }

        [Fact]
        public async Task ValidarCertificadoAsync_ComCertificadoValido_DeveRetornarTrue()
        {
            // Arrange
            var pfxData = _testCertificate.Export(X509ContentType.Pfx, "test123");
            
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretBinary = new MemoryStream(pfxData) });
            
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-password-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = "test123" });

            // Act
            var resultado = await _certificateService.ValidarCertificadoAsync();

            // Assert
            resultado.Should().BeTrue();
        }

        [Fact]
        public async Task AssinarXmlAsync_ComXmlValido_DeveRetornarXmlAssinado()
        {
            // Arrange
            var xmlContent = """
                <?xml version="1.0" encoding="UTF-8"?>
                <TestElement>
                    <Data>Test data for signing</Data>
                </TestElement>
                """;

            var pfxData = _testCertificate.Export(X509ContentType.Pfx, "test123");
            
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretBinary = new MemoryStream(pfxData) });
            
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-password-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = "test123" });

            // Act
            var resultado = await _certificateService.AssinarXmlAsync(xmlContent);

            // Assert
            resultado.Should().NotBeNull();
            resultado.Length.Should().BeGreaterThan(0);
            
            var xmlResultado = System.Text.Encoding.UTF8.GetString(resultado);
            xmlResultado.Should().Contain("<Signature");
            xmlResultado.Should().Contain("</Signature>");
        }

        [Fact]
        public async Task AssinarXmlAsync_ComXmlVazio_DeveLancarArgumentNullException()
        {
            // Act & Assert
            var action = async () => await _certificateService.AssinarXmlAsync("");
            await action.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task AssinarXmlAsync_ComXmlNull_DeveLancarArgumentNullException()
        {
            // Act & Assert
            var action = async () => await _certificateService.AssinarXmlAsync(null!);
            await action.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task ObterCertificateThumbprintAsync_DeveRetornarThumbprintCorreto()
        {
            // Arrange
            var pfxData = _testCertificate.Export(X509ContentType.Pfx, "test123");
            
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretBinary = new MemoryStream(pfxData) });
            
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-password-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = "test123" });

            // Act
            var resultado = await _certificateService.ObterCertificateThumbprintAsync();

            // Assert
            resultado.Should().NotBeNullOrEmpty();
            resultado.Should().Be(_testCertificate.Thumbprint);
        }

        [Fact]
        public async Task RenovarCacheAsync_DeveLimparCacheERecarregarCertificado()
        {
            // Arrange
            var pfxData = _testCertificate.Export(X509ContentType.Pfx, "test123");
            
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretBinary = new MemoryStream(pfxData) });
            
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.Is<GetSecretValueRequest>(r => r.SecretId == "test-certificate-password-secret"), default))
                .ReturnsAsync(new GetSecretValueResponse { SecretString = "test123" });

            // Primeiro carregamento
            await _certificateService.ObterCertificadoAsync();

            // Act
            await _certificateService.RenovarCacheAsync();

            // Assert
            _secretsManagerMock.Verify(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), default), 
                Times.AtLeast(4)); // 2 calls no primeiro carregamento + 2 calls no renovar cache
        }

        [Fact]
        public async Task ObterCertificadoAsync_ComConfiguracaoInvalida_DeveLancarInvalidOperationException()
        {
            // Arrange
            _configurationMock.Setup(c => c["AWS:SecretsManager:CertificateSecretName"])
                .Returns((string?)null);

            // Act & Assert
            var action = async () => await _certificateService.ObterCertificadoAsync();
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Configurações de certificado não encontradas*");
        }

        [Fact]
        public async Task ObterCertificadoAsync_ComErroDoSecretsManager_DeveLancarInvalidOperationException()
        {
            // Arrange
            _secretsManagerMock.Setup(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), default))
                .ThrowsAsync(new ResourceNotFoundException("Secret not found"));

            // Act & Assert
            var action = async () => await _certificateService.ObterCertificadoAsync();
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Não foi possível obter o certificado do AWS Secrets Manager*");
        }

        private static X509Certificate2 CreateTestCertificate()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=Test Certificate", 
                rsa, 
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));

            var certificate = request.CreateSelfSigned(
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow.AddDays(365));

            return new X509Certificate2(certificate.Export(X509ContentType.Pfx, "test123"), "test123", 
                X509KeyStorageFlags.Exportable);
        }

        public void Dispose()
        {
            _testCertificate?.Dispose();
            (_certificateService as IDisposable)?.Dispose();
        }
    }
}