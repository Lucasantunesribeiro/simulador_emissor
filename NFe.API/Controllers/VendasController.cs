using Microsoft.AspNetCore.Mvc;
using NFe.Core.DTOs;
using NFe.Core.Interfaces;

namespace NFe.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class VendasController : ControllerBase
    {
        private readonly INFeService _nfeService;

        public VendasController(INFeService nfeService)
        {
            _nfeService = nfeService;
        }

        /// <summary>
        /// Obter todas as vendas
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<VendaResponseDto>>> GetVendas()
        {
            var vendas = await _nfeService.ObterTodasVendasAsync();
            return Ok(vendas);
        }

        /// <summary>
        /// Obter vendas pendentes de processamento
        /// </summary>
        [HttpGet("pendentes")]
        public async Task<ActionResult<IEnumerable<VendaResponseDto>>> GetVendasPendentes()
        {
            var vendas = await _nfeService.ObterVendasPendentesAsync();
            return Ok(vendas);
        }

        /// <summary>
        /// Obter venda por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<VendaResponseDto>> GetVenda(Guid id)
        {
            var venda = await _nfeService.ObterVendaAsync(id);
            if (venda == null)
            {
                return NotFound(new { message = "Venda não encontrada" });
            }
            return Ok(venda);
        }

        /// <summary>
        /// Criar nova venda
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<VendaResponseDto>> CreateVenda(VendaCreateDto vendaDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var vendaId = await _nfeService.CriarVendaAsync(vendaDto);
            var venda = await _nfeService.ObterVendaAsync(vendaId);
            
            return CreatedAtAction(nameof(GetVenda), new { id = vendaId }, venda);
        }

        /// <summary>
        /// Processar venda (gerar NFe simulada)
        /// </summary>
        [HttpPost("{id}/processar")]
        public async Task<ActionResult> ProcessarVenda(Guid id)
        {
            var sucesso = await _nfeService.ProcessarVendaAsync(id);
            if (!sucesso)
            {
                return BadRequest(new { message = "Não foi possível processar a venda. Verifique se ela existe e está pendente." });
            }

            var venda = await _nfeService.ObterVendaAsync(id);
            return Ok(new { message = "Venda processada com sucesso", venda });
        }
    }
}
