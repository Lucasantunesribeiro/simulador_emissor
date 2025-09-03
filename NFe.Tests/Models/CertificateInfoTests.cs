using FluentAssertions;
using NFe.Core.Models;
using Xunit;

namespace NFe.Tests.Models
{
    public class CertificateInfoTests
    {
        [Fact]
        public void IsValid_ComCertificadoValido_DeveRetornarTrue()
        {
            // Arrange
            var certInfo = new CertificateInfo
            {
                Thumbprint = "ABC123",
                SubjectName = "CN=Test",
                IssuerName = "CN=Test CA",
                NotBefore = DateTime.UtcNow.AddDays(-1),
                NotAfter = DateTime.UtcNow.AddDays(365),
                SerialNumber = "123456"
            };

            // Act & Assert
            certInfo.IsValid.Should().BeTrue();
        }

        [Fact]
        public void IsValid_ComCertificadoExpirado_DeveRetornarFalse()
        {
            // Arrange
            var certInfo = new CertificateInfo
            {
                Thumbprint = "ABC123",
                SubjectName = "CN=Test",
                IssuerName = "CN=Test CA",
                NotBefore = DateTime.UtcNow.AddDays(-365),
                NotAfter = DateTime.UtcNow.AddDays(-1),
                SerialNumber = "123456"
            };

            // Act & Assert
            certInfo.IsValid.Should().BeFalse();
        }

        [Fact]
        public void IsValid_ComCertificadoAindaNaoValido_DeveRetornarFalse()
        {
            // Arrange
            var certInfo = new CertificateInfo
            {
                Thumbprint = "ABC123",
                SubjectName = "CN=Test",
                IssuerName = "CN=Test CA",
                NotBefore = DateTime.UtcNow.AddDays(1),
                NotAfter = DateTime.UtcNow.AddDays(365),
                SerialNumber = "123456"
            };

            // Act & Assert
            certInfo.IsValid.Should().BeFalse();
        }

        [Fact]
        public void DaysUntilExpiration_ComCertificadoValidoPor30Dias_DeveRetornar30()
        {
            // Arrange
            var certInfo = new CertificateInfo
            {
                Thumbprint = "ABC123",
                SubjectName = "CN=Test",
                IssuerName = "CN=Test CA",
                NotBefore = DateTime.UtcNow.AddDays(-1),
                NotAfter = DateTime.UtcNow.AddDays(30),
                SerialNumber = "123456"
            };

            // Act & Assert
            certInfo.DaysUntilExpiration.Should().Be(30);
        }

        [Fact]
        public void DaysUntilExpiration_ComCertificadoExpirado_DeveRetornarValorNegativo()
        {
            // Arrange
            var certInfo = new CertificateInfo
            {
                Thumbprint = "ABC123",
                SubjectName = "CN=Test",
                IssuerName = "CN=Test CA",
                NotBefore = DateTime.UtcNow.AddDays(-365),
                NotAfter = DateTime.UtcNow.AddDays(-5),
                SerialNumber = "123456"
            };

            // Act & Assert
            certInfo.DaysUntilExpiration.Should().BeLessThan(0);
        }

        [Theory]
        [InlineData(15, true)]
        [InlineData(30, true)]
        [InlineData(31, false)]
        [InlineData(60, false)]
        [InlineData(-5, false)] // Expirado
        public void IsExpiringSoon_ComDiferentesDiasRestantes_DeveRetornarResultadoCorreto(int dias, bool esperado)
        {
            // Arrange
            var certInfo = new CertificateInfo
            {
                Thumbprint = "ABC123",
                SubjectName = "CN=Test",
                IssuerName = "CN=Test CA",
                NotBefore = DateTime.UtcNow.AddDays(-1),
                NotAfter = DateTime.UtcNow.AddDays(dias),
                SerialNumber = "123456"
            };

            // Act & Assert
            certInfo.IsExpiringSoon.Should().Be(esperado);
        }

        [Fact]
        public void CertificateInfo_ComPropriedadesObrigatorias_DeveSerCriadoCorretamente()
        {
            // Arrange & Act
            var certInfo = new CertificateInfo
            {
                Thumbprint = "ABCDEF123456",
                SubjectName = "CN=Empresa Teste LTDA,OU=IT,O=Empresa,L=Sao Paulo,S=SP,C=BR",
                IssuerName = "CN=AC SERASA NFe,OU=Autoridade Certificadora SERASA NFe,O=SERASA S.A.",
                NotBefore = DateTime.UtcNow.AddDays(-10),
                NotAfter = DateTime.UtcNow.AddDays(355),
                SerialNumber = "1234567890ABCDEF"
            };

            // Assert
            certInfo.Thumbprint.Should().Be("ABCDEF123456");
            certInfo.SubjectName.Should().Contain("Empresa Teste LTDA");
            certInfo.IssuerName.Should().Contain("SERASA");
            certInfo.SerialNumber.Should().Be("1234567890ABCDEF");
            certInfo.IsValid.Should().BeTrue();
            certInfo.IsExpiringSoon.Should().BeFalse();
        }
    }
}