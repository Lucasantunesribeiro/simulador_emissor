using Microsoft.AspNetCore.Mvc;
using NFe.Core.Interfaces;
using NFe.Core.Models;
using System.Security.Cryptography.X509Certificates;

namespace NFe.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CertificateController : ControllerBase
    {
        private readonly ICertificateService _certificateService;
        private readonly ILogger<CertificateController> _logger;

        public CertificateController(ICertificateService certificateService, ILogger<CertificateController> logger)
        {
            _certificateService = certificateService;
            _logger = logger;
        }

        /// <summary>
        /// Obtém informações do certificado digital configurado.
        /// </summary>
        /// <returns>Informações detalhadas do certificado</returns>
        [HttpGet("info")]
        public async Task<ActionResult<CertificateInfo>> GetCertificateInfo()
        {
            try
            {
                _logger.LogInformation("Obtendo informações do certificado digital");
                
                var certificado = await _certificateService.ObterCertificadoAsync();
                var info = new CertificateInfo
                {
                    Thumbprint = certificado.Thumbprint,
                    SubjectName = certificado.Subject,
                    IssuerName = certificado.Issuer,
                    NotBefore = certificado.NotBefore.ToUniversalTime(),
                    NotAfter = certificado.NotAfter.ToUniversalTime(),
                    SerialNumber = certificado.SerialNumber
                };

                _logger.LogInformation("Informações do certificado obtidas com sucesso. Thumbprint: {Thumbprint}", info.Thumbprint);
                
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter informações do certificado");
                return StatusCode(500, new { message = "Erro interno do servidor ao obter certificado" });
            }
        }

        /// <summary>
        /// Valida se o certificado digital está válido e não expirado.
        /// </summary>
        /// <returns>Status de validação do certificado</returns>
        [HttpGet("validate")]
        public async Task<ActionResult<object>> ValidateCertificate()
        {
            try
            {
                _logger.LogInformation("Iniciando validação do certificado digital");
                
                var isValid = await _certificateService.ValidarCertificadoAsync();
                var thumbprint = await _certificateService.ObterCertificateThumbprintAsync();

                var result = new
                {
                    IsValid = isValid,
                    Thumbprint = thumbprint,
                    ValidatedAt = DateTime.UtcNow,
                    Message = isValid ? "Certificado válido" : "Certificado inválido ou expirado"
                };

                _logger.LogInformation("Validação do certificado concluída. Válido: {IsValid}, Thumbprint: {Thumbprint}", 
                    isValid, thumbprint);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar certificado");
                return StatusCode(500, new { message = "Erro interno do servidor ao validar certificado" });
            }
        }

        /// <summary>
        /// Testa a assinatura digital com um XML de exemplo.
        /// </summary>
        /// <returns>Resultado do teste de assinatura</returns>
        [HttpPost("test-signature")]
        public async Task<ActionResult<object>> TestSignature()
        {
            try
            {
                _logger.LogInformation("Iniciando teste de assinatura digital");

                const string xmlTest = """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <TestDocument>
                        <Data>Documento de teste para assinatura digital</Data>
                        <Timestamp>{0}</Timestamp>
                    </TestDocument>
                    """;

                var xmlContent = string.Format(xmlTest, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                var xmlAssinado = await _certificateService.AssinarXmlAsync(xmlContent);
                var thumbprint = await _certificateService.ObterCertificateThumbprintAsync();

                var result = new
                {
                    Success = true,
                    OriginalSize = xmlContent.Length,
                    SignedSize = xmlAssinado.Length,
                    Thumbprint = thumbprint,
                    SignedAt = DateTime.UtcNow,
                    Message = "XML assinado com sucesso"
                };

                _logger.LogInformation("Teste de assinatura concluído com sucesso. Tamanho original: {OriginalSize}, Tamanho assinado: {SignedSize}",
                    xmlContent.Length, xmlAssinado.Length);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao testar assinatura digital");
                return StatusCode(500, new { message = "Erro interno do servidor ao testar assinatura" });
            }
        }

        /// <summary>
        /// Força a renovação do cache do certificado.
        /// </summary>
        /// <returns>Confirmação da renovação</returns>
        [HttpPost("renew-cache")]
        public async Task<ActionResult<object>> RenewCache()
        {
            try
            {
                _logger.LogInformation("Iniciando renovação forçada do cache de certificado");
                
                await _certificateService.RenovarCacheAsync();

                var result = new
                {
                    Success = true,
                    RenewedAt = DateTime.UtcNow,
                    Message = "Cache de certificado renovado com sucesso"
                };

                _logger.LogInformation("Cache de certificado renovado com sucesso");
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao renovar cache do certificado");
                return StatusCode(500, new { message = "Erro interno do servidor ao renovar cache" });
            }
        }
    }
}