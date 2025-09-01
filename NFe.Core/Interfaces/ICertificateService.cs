using System.Security.Cryptography.X509Certificates;

namespace NFe.Core.Interfaces
{
    /// <summary>
    /// Serviço responsável pelo gerenciamento de certificados digitais A1 para assinatura de NFe.
    /// </summary>
    public interface ICertificateService
    {
        /// <summary>
        /// Obtém o certificado digital A1 configurado para o ambiente atual.
        /// </summary>
        /// <returns>Certificado X509 válido para assinatura digital.</returns>
        /// <exception cref="InvalidOperationException">Quando o certificado não é encontrado ou é inválido.</exception>
        Task<X509Certificate2> ObterCertificadoAsync();

        /// <summary>
        /// Valida se o certificado atual é válido e não está expirado.
        /// </summary>
        /// <returns>True se o certificado é válido, False caso contrário.</returns>
        Task<bool> ValidarCertificadoAsync();

        /// <summary>
        /// Assina digitalmente o conteúdo XML usando o certificado configurado.
        /// </summary>
        /// <param name="xmlContent">Conteúdo XML a ser assinado.</param>
        /// <returns>XML assinado em formato de bytes.</returns>
        /// <exception cref="ArgumentNullException">Quando xmlContent é null ou vazio.</exception>
        /// <exception cref="InvalidOperationException">Quando não é possível assinar o XML.</exception>
        Task<byte[]> AssinarXmlAsync(string xmlContent);

        /// <summary>
        /// Obtém o thumbprint (impressão digital) do certificado atual.
        /// </summary>
        /// <returns>Thumbprint do certificado em formato hexadecimal.</returns>
        Task<string> ObterCertificateThumbprintAsync();

        /// <summary>
        /// Força a renovação do cache de certificado.
        /// </summary>
        Task RenovarCacheAsync();
    }
}