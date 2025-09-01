using Microsoft.AspNetCore.Mvc;
using NFe.Core.Configuration;
using NFe.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace NFe.API.Controllers;

/// <summary>
/// Controller para testes da integração NFe/SEFAZ
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NFeTestController : ControllerBase
{
    private readonly INFeService _nfeService;
    private readonly ISefazClient? _sefazClient;
    private readonly SefazSettings _sefazSettings;
    private readonly ILogger<NFeTestController> _logger;

    public NFeTestController(
        INFeService nfeService,
        IServiceProvider serviceProvider,
        IOptions<SefazSettings> sefazSettings,
        ILogger<NFeTestController> logger)
    {
        _nfeService = nfeService;
        _sefazClient = serviceProvider.GetService<ISefazClient>();
        _sefazSettings = sefazSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Verifica qual implementação NFe está sendo utilizada
    /// </summary>
    [HttpGet("implementacao")]
    public IActionResult VerificarImplementacao()
    {
        var implementacao = _nfeService.GetType().Name;
        var temSefazClient = _sefazClient != null;
        
        return Ok(new
        {
            ImplementacaoNFe = implementacao,
            TemClienteSefaz = temSefazClient,
            Ambiente = _sefazSettings.Ambiente,
            UF = _sefazSettings.UF,
            EhProducao = implementacao == "RealNFeService",
            EhSimulacao = implementacao == "SimulacaoNFeService"
        });
    }

    /// <summary>
    /// Testa conectividade com SEFAZ (apenas se estiver usando implementação real)
    /// </summary>
    [HttpGet("status-sefaz")]
    public async Task<IActionResult> TestarConectividadeSefaz()
    {
        if (_sefazClient == null)
        {
            return Ok(new
            {
                Status = "Simulação",
                Mensagem = "Integração SEFAZ não habilitada - rodando em modo simulação",
                Disponivel = true
            });
        }

        try
        {
            _logger.LogInformation("Testando conectividade com SEFAZ");
            
            var disponivel = await _sefazClient.VerificarStatusServicoAsync();
            
            return Ok(new
            {
                Status = disponivel ? "Disponível" : "Indisponível",
                Mensagem = disponivel 
                    ? "Serviço SEFAZ está operacional" 
                    : "Serviço SEFAZ temporariamente indisponível",
                Disponivel = disponivel,
                Ambiente = _sefazSettings.Ambiente == 1 ? "Produção" : "Homologação",
                UF = _sefazSettings.UF,
                TesteRealizado = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar conectividade SEFAZ");
            
            return StatusCode(500, new
            {
                Status = "Erro",
                Mensagem = $"Erro ao conectar com SEFAZ: {ex.Message}",
                Disponivel = false
            });
        }
    }

    /// <summary>
    /// Retorna configurações atuais (dados sensíveis mascarados)
    /// </summary>
    [HttpGet("configuracoes")]
    public IActionResult ObterConfiguracoes()
    {
        return Ok(new
        {
            Ambiente = _sefazSettings.Ambiente,
            AmbienteDescricao = _sefazSettings.Ambiente == 1 ? "Produção" : "Homologação",
            UF = _sefazSettings.UF,
            Serie = _sefazSettings.Serie,
            CodigoMunicipio = _sefazSettings.CodigoMunicipio,
            CNPJ = MascararDocumento(_sefazSettings.CNPJ),
            RazaoSocial = _sefazSettings.RazaoSocial,
            NomeFantasia = _sefazSettings.NomeFantasia,
            Endereco = _sefazSettings.Endereco,
            Bairro = _sefazSettings.Bairro,
            CEP = MascararCEP(_sefazSettings.CEP),
            InscricaoEstadual = MascararDocumento(_sefazSettings.InscricaoEstadual),
            RegimeTributario = _sefazSettings.RegimeTributario,
            TimeoutSeconds = _sefazSettings.TimeoutSeconds,
            RetryAttempts = _sefazSettings.RetryAttempts
        });
    }

    #region Métodos auxiliares

    private string MascararDocumento(string documento)
    {
        if (string.IsNullOrWhiteSpace(documento) || documento.Length < 4)
            return "***";
        
        return documento.Substring(0, 4) + "****" + documento.Substring(documento.Length - 2);
    }

    private string MascararCEP(string cep)
    {
        if (string.IsNullOrWhiteSpace(cep) || cep.Length < 3)
            return "***";
        
        return cep.Substring(0, 2) + "***" + cep.Substring(cep.Length - 1);
    }

    #endregion
}