using Microsoft.AspNetCore.Mvc;
using NFe.Core.Entities;
using NFe.Core.Interfaces;

namespace NFe.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class VendasController : ControllerBase
    {
        private readonly IVendaRepository _vendaRepository;
        private readonly INFeService _nfeService;
        
        public VendasController(IVendaRepository vendaRepository, INFeService nfeService)
        {
            _vendaRepository = vendaRepository;
            _nfeService = nfeService;
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<Venda>> Get(Guid id)
        {
            var venda = await _vendaRepository.GetByIdAsync(id);
            
            if (venda == null)
            {
                return NotFound();
            }
            
            return Ok(venda);
        }
        
        [HttpPost]
        public async Task<ActionResult<Guid>> Post([FromBody] Venda venda)
        {
            venda.DataVenda = DateTime.Now;
            venda.Processada = false;
            
            var id = await _vendaRepository.AddAsync(venda);
            
            // Em um cenário real, aqui seria enviado para uma fila
            // para processamento assíncrono pelo Worker
            
            return CreatedAtAction(nameof(Get), new { id }, id);
        }
        
        [HttpGet("{id}/status")]
        public async Task<ActionResult<string>> GetStatus(Guid id)
        {
            var venda = await _vendaRepository.GetByIdAsync(id);
            
            if (venda == null)
            {
                return NotFound();
            }
            
            return Ok(new { 
                Status = venda.Processada ? "Processada" : "Pendente",
                NumeroNota = venda.NumeroNota,
                ChaveAcesso = venda.ChaveAcesso
            });
        }
    }
}
