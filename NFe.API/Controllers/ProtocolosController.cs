using Microsoft.AspNetCore.Mvc;
using NFe.Core.DTOs;
using NFe.Core.Interfaces;

namespace NFe.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ProtocolosController : ControllerBase
    {
        private readonly INFeService _nfeService;
        private readonly ILogger<ProtocolosController> _logger;

        public ProtocolosController(INFeService nfeService, ILogger<ProtocolosController> logger)
        {
            _nfeService = nfeService;
            _logger = logger;
        }

        /// <summary>
        /// Obter todos os protocolos
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProtocoloResponseDto>>> GetProtocolos()
        {
            try
            {
                _logger.LogInformation("Iniciando busca por todos os protocolos");
                var protocolos = await _nfeService.ObterTodosProtocolosAsync();
                _logger.LogInformation("Encontrados {Count} protocolos", protocolos.Count());
                return Ok(protocolos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter todos os protocolos");
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Obter protocolo por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ProtocoloResponseDto>> GetProtocolo(Guid id)
        {
            try
            {
                _logger.LogInformation("Buscando protocolo com ID: {ProtocoloId}", id);
                var protocolo = await _nfeService.ObterProtocoloAsync(id);
                if (protocolo == null)
                {
                    _logger.LogWarning("Protocolo não encontrado: {ProtocoloId}", id);
                    return NotFound(new { message = "Protocolo não encontrado" });
                }
                return Ok(protocolo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter protocolo {ProtocoloId}", id);
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Obter protocolo por chave de acesso
        /// </summary>
        [HttpGet("chave/{chaveAcesso}")]
        public async Task<ActionResult<ProtocoloResponseDto>> GetProtocoloPorChave(string chaveAcesso)
        {
            try
            {
                _logger.LogInformation("Buscando protocolo por chave: {ChaveAcesso}", chaveAcesso);
                var protocolo = await _nfeService.ObterProtocoloPorChaveAsync(chaveAcesso);
                if (protocolo == null)
                {
                    _logger.LogWarning("Protocolo não encontrado para chave: {ChaveAcesso}", chaveAcesso);
                    return NotFound(new { message = "Protocolo não encontrado" });
                }
                return Ok(protocolo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter protocolo por chave: {ChaveAcesso}", chaveAcesso);
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }
    }
}