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
        private readonly ILogger<VendasController> _logger;

        public VendasController(INFeService nfeService, ILogger<VendasController> logger)
        {
            _nfeService = nfeService;
            _logger = logger;
        }

        /// <summary>
        /// Obter todas as vendas
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<VendaResponseDto>>> GetVendas()
        {
            try
            {
                _logger.LogInformation("Iniciando busca por todas as vendas");
                var vendas = await _nfeService.ObterTodasVendasAsync();
                _logger.LogInformation("Encontradas {Count} vendas", vendas.Count());
                return Ok(vendas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter todas as vendas");
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Obter vendas pendentes de processamento
        /// </summary>
        [HttpGet("pendentes")]
        public async Task<ActionResult<IEnumerable<VendaResponseDto>>> GetVendasPendentes()
        {
            try
            {
                _logger.LogInformation("Iniciando busca por vendas pendentes");
                var vendas = await _nfeService.ObterVendasPendentesAsync();
                _logger.LogInformation("Encontradas {Count} vendas pendentes", vendas.Count());
                return Ok(vendas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter vendas pendentes");
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Obter venda por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<VendaResponseDto>> GetVenda(Guid id)
        {
            try
            {
                _logger.LogInformation("Buscando venda com ID: {VendaId}", id);
                var venda = await _nfeService.ObterVendaAsync(id);
                if (venda == null)
                {
                    _logger.LogWarning("Venda não encontrada: {VendaId}", id);
                    return NotFound(new { message = "Venda não encontrada" });
                }
                return Ok(venda);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter venda {VendaId}", id);
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Criar nova venda
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<VendaResponseDto>> CreateVenda(VendaCreateDto vendaDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Dados inválidos para criação de venda: {ModelState}", ModelState);
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("Criando nova venda para cliente: {ClienteNome}", vendaDto.ClienteNome);
                var vendaId = await _nfeService.CriarVendaAsync(vendaDto);
                var venda = await _nfeService.ObterVendaAsync(vendaId);
                
                _logger.LogInformation("Venda criada com sucesso: {VendaId}", vendaId);
                return CreatedAtAction(nameof(GetVenda), new { id = vendaId }, venda);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar venda para cliente: {ClienteNome}", vendaDto?.ClienteNome);
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Processar venda (gerar NFe simulada)
        /// </summary>
        [HttpPost("{id}/processar")]
        public async Task<ActionResult> ProcessarVenda(Guid id)
        {
            try
            {
                _logger.LogInformation("Processando venda: {VendaId}", id);
                var sucesso = await _nfeService.ProcessarVendaAsync(id);
                if (!sucesso)
                {
                    _logger.LogWarning("Falha ao processar venda: {VendaId}", id);
                    return BadRequest(new { message = "Não foi possível processar a venda. Verifique se ela existe e está pendente." });
                }

                var venda = await _nfeService.ObterVendaAsync(id);
                _logger.LogInformation("Venda processada com sucesso: {VendaId}", id);
                return Ok(new { message = "Venda processada com sucesso", venda });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar venda: {VendaId}", id);
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }
    }
}
