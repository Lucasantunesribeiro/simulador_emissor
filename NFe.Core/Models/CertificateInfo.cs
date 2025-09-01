namespace NFe.Core.Models
{
    /// <summary>
    /// Informações de metadados do certificado digital.
    /// </summary>
    public class CertificateInfo
    {
        /// <summary>
        /// Thumbprint do certificado.
        /// </summary>
        public required string Thumbprint { get; init; }

        /// <summary>
        /// Nome do titular do certificado.
        /// </summary>
        public required string SubjectName { get; init; }

        /// <summary>
        /// Nome do emissor do certificado.
        /// </summary>
        public required string IssuerName { get; init; }

        /// <summary>
        /// Data de início de validade do certificado.
        /// </summary>
        public required DateTime NotBefore { get; init; }

        /// <summary>
        /// Data de expiração do certificado.
        /// </summary>
        public required DateTime NotAfter { get; init; }

        /// <summary>
        /// Indica se o certificado está válido no momento atual.
        /// </summary>
        public bool IsValid => DateTime.UtcNow >= NotBefore && DateTime.UtcNow <= NotAfter;

        /// <summary>
        /// Dias restantes até a expiração do certificado.
        /// </summary>
        public int DaysUntilExpiration => (int)(NotAfter - DateTime.UtcNow).TotalDays;

        /// <summary>
        /// Indica se o certificado expira em breve (menos de 30 dias).
        /// </summary>
        public bool IsExpiringSoon => DaysUntilExpiration <= 30 && DaysUntilExpiration > 0;

        /// <summary>
        /// Número de série do certificado.
        /// </summary>
        public required string SerialNumber { get; init; }
    }
}