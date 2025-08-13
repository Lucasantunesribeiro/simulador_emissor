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

        public ProtocolosController(INFeService nfeService)
        {
            _nfeService = nfeService;
        }

        /// <summary>
        /// Obter todos os protocolos
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProtocoloResponseDto>>> GetProtocolos()
        {
            var protocolos = await _nfeService.ObterTodosProtocolosAsync();
            return Ok(protocolos);
        }

        /// <summary>
        /// Obter protocolo por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ProtocoloResponseDto>> GetProtocolo(Guid id)
        {
            var protocolo = await _nfeService.ObterProtocoloAsync(id);
            if (protocolo == null)
            {
                return NotFound(new { message = "Protocolo não encontrado" });
            }
            return Ok(protocolo);
        }

        /// <summary>
        /// Consultar protocolo por chave de acesso
        /// </summary>
        [HttpGet("chave/{chaveAcesso}")]
        public async Task<ActionResult<ProtocoloResponseDto>> GetProtocoloPorChave(string chaveAcesso)
        {
            if (string.IsNullOrEmpty(chaveAcesso) || chaveAcesso.Length != 44)
            {
                return BadRequest(new { message = "Chave de acesso deve ter 44 dígitos" });
            }

            var protocolo = await _nfeService.ObterProtocoloPorChaveAsync(chaveAcesso);
            if (protocolo == null)
            {
                return NotFound(new { message = "Protocolo não encontrado para a chave de acesso informada" });
            }
            return Ok(protocolo);
        }
    }
}