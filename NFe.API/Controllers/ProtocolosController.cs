using Microsoft.AspNetCore.Mvc;
using NFe.Core.Entities;
using NFe.Core.Interfaces;

namespace NFe.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ProtocolosController : ControllerBase
    {
        private readonly IProtocoloRepository _protocoloRepository;
        
        public ProtocolosController(IProtocoloRepository protocoloRepository)
        {
            _protocoloRepository = protocoloRepository;
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<Protocolo>> Get(Guid id)
        {
            var protocolo = await _protocoloRepository.GetByIdAsync(id);
            
            if (protocolo == null)
            {
                return NotFound();
            }
            
            return Ok(protocolo);
        }
        
        [HttpGet("chave/{chaveAcesso}")]
        public async Task<ActionResult<Protocolo>> GetByChaveAcesso(string chaveAcesso)
        {
            var protocolo = await _protocoloRepository.GetByChaveAcessoAsync(chaveAcesso);
            
            if (protocolo == null)
            {
                return NotFound();
            }
            
            return Ok(protocolo);
        }
    }
}
