using Microsoft.Extensions.Logging;
using NFe.Core.DTOs;
using NFe.Core.Entities;
using NFe.Core.Interfaces;
using NFe.Core.Models;

namespace NFe.Core.Services;

/// <summary>
/// Implementação real do serviço NFe usando integração com SEFAZ
/// </summary>
public class RealNFeService : INFeService
{
    private readonly IVendaRepository _vendaRepository;
    private readonly IProtocoloRepository _protocoloRepository;
    private readonly INFeGenerator _nfeGenerator;
    private readonly ICertificateService _certificateService;
    private readonly ISefazClient _sefazClient;
    private readonly ILogger<RealNFeService> _logger;

    public RealNFeService(
        IVendaRepository vendaRepository,
        IProtocoloRepository protocoloRepository,
        INFeGenerator nfeGenerator,
        ICertificateService certificateService,
        ISefazClient sefazClient,
        ILogger<RealNFeService> logger)
    {
        _vendaRepository = vendaRepository;
        _protocoloRepository = protocoloRepository;
        _nfeGenerator = nfeGenerator;
        _certificateService = certificateService;
        _sefazClient = sefazClient;
        _logger = logger;
    }

    public async Task<Guid> CriarVendaAsync(VendaCreateDto vendaDto)
    {
        _logger.LogInformation("Criando nova venda - Cliente: {Cliente}", vendaDto.ClienteNome);

        var venda = new Venda
        {
            ClienteNome = vendaDto.ClienteNome,
            ClienteDocumento = vendaDto.ClienteDocumento,
            ClienteEndereco = vendaDto.ClienteEndereco,
            Observacoes = vendaDto.Observacoes,
            Status = "Pendente"
        };

        // Adicionar itens
        foreach (var itemDto in vendaDto.Itens)
        {
            venda.Itens.Add(new ItemVenda
            {
                VendaId = venda.Id,
                Codigo = itemDto.Codigo,
                Descricao = itemDto.Descricao,
                Quantidade = itemDto.Quantidade,
                ValorUnitario = itemDto.ValorUnitario,
                NCM = itemDto.NCM,
                CFOP = itemDto.CFOP,
                UnidadeMedida = itemDto.UnidadeMedida
            });
        }

        var vendaId = await _vendaRepository.AddAsync(venda);
        
        _logger.LogInformation("Venda criada com sucesso - ID: {VendaId}", vendaId);
        
        return vendaId;
    }

    public async Task<VendaResponseDto?> ObterVendaAsync(Guid id)
    {
        _logger.LogDebug("Obtendo venda {VendaId}", id);

        var venda = await _vendaRepository.GetByIdAsync(id);
        if (venda == null)
        {
            _logger.LogWarning("Venda não encontrada - ID: {VendaId}", id);
            return null;
        }

        return MapearVendaParaDto(venda);
    }

    public async Task<IEnumerable<VendaResponseDto>> ObterTodasVendasAsync()
    {
        _logger.LogDebug("Obtendo todas as vendas");

        var vendas = await _vendaRepository.GetAllAsync();
        return vendas.Select(MapearVendaParaDto);
    }

    public async Task<IEnumerable<VendaResponseDto>> ObterVendasPendentesAsync()
    {
        _logger.LogDebug("Obtendo vendas pendentes");

        var vendas = await _vendaRepository.GetPendentesAsync();
        return vendas.Select(MapearVendaParaDto);
    }

    public async Task<bool> ProcessarVendaAsync(Guid vendaId)
    {
        _logger.LogInformation("Iniciando processamento da venda {VendaId} com integração SEFAZ real", vendaId);

        var venda = await _vendaRepository.GetByIdAsync(vendaId);
        if (venda == null)
        {
            _logger.LogWarning("Venda não encontrada para processamento - ID: {VendaId}", vendaId);
            return false;
        }

        if (venda.Status != "Pendente")
        {
            _logger.LogWarning("Venda não está pendente - ID: {VendaId}, Status: {Status}", vendaId, venda.Status);
            return false;
        }

        // Verificar se o serviço SEFAZ está disponível
        var sefazDisponivel = await _sefazClient.VerificarStatusServicoAsync();
        if (!sefazDisponivel)
        {
            _logger.LogError("Serviço SEFAZ indisponível para processamento da venda {VendaId}", vendaId);
            venda.Status = "Erro";
            await _vendaRepository.UpdateAsync(venda);
            return false;
        }

        // Atualizar status para processando
        venda.Status = "Processando";
        await _vendaRepository.UpdateAsync(venda);
        
        _logger.LogInformation("Venda {VendaId} marcada como processando", vendaId);

        try
        {
            // 1. Validar dados da venda
            var errosValidacao = _nfeGenerator.ValidarDadosVenda(venda);
            if (errosValidacao.Any())
            {
                var mensagemErro = $"Dados inválidos: {string.Join(", ", errosValidacao)}";
                _logger.LogError("Erro de validação na venda {VendaId}: {Erros}", vendaId, mensagemErro);
                
                await RegistrarErroProcessamento(venda, mensagemErro);
                return false;
            }

            // 2. Gerar XML NFe
            _logger.LogInformation("Gerando XML NFe para venda {VendaId}", vendaId);
            var xmlNFe = await _nfeGenerator.GerarXmlNFeAsync(venda);

            // 3. Assinar XML com certificado digital
            _logger.LogInformation("Assinando XML NFe para venda {VendaId}", vendaId);
            var xmlAssinado = await _certificateService.AssinarXmlAsync(xmlNFe);
            
            if (string.IsNullOrWhiteSpace(xmlAssinado))
            {
                const string mensagemErro = "Falha na assinatura digital do XML";
                _logger.LogError("{MensagemErro} - VendaId: {VendaId}", mensagemErro, vendaId);
                
                await RegistrarErroProcessamento(venda, mensagemErro);
                return false;
            }

            // 4. Enviar para SEFAZ
            _logger.LogInformation("Enviando NFe para SEFAZ - VendaId: {VendaId}, ChaveAcesso: {ChaveAcesso}", 
                vendaId, venda.ChaveAcesso);
            
            var resultadoEnvio = await _sefazClient.EnviarNFeAsync(xmlAssinado);

            // 5. Processar resposta SEFAZ
            if (resultadoEnvio.Success)
            {
                _logger.LogInformation("NFe autorizada com sucesso - VendaId: {VendaId}, ChaveAcesso: {ChaveAcesso}, Protocolo: {Protocolo}", 
                    vendaId, resultadoEnvio.ChaveAcesso, resultadoEnvio.NumeroProtocolo);

                // Atualizar dados da venda
                venda.Status = StatusSefaz.Autorizada;
                venda.ChaveAcesso = resultadoEnvio.ChaveAcesso;
                venda.NumeroNFe = resultadoEnvio.NumeroNFe;
                venda.SerieNFe = "1"; // TODO: Configurar série

                await _vendaRepository.UpdateAsync(venda);

                // Salvar protocolo de autorização
                await SalvarProtocoloAutorizacao(venda, resultadoEnvio);

                return true;
            }
            else
            {
                _logger.LogWarning("NFe rejeitada pela SEFAZ - VendaId: {VendaId}, ChaveAcesso: {ChaveAcesso}, Motivo: {Motivo}, Codigo: {Codigo}", 
                    vendaId, resultadoEnvio.ChaveAcesso, resultadoEnvio.MensagemSefaz, resultadoEnvio.CodigoStatus);

                venda.Status = StatusSefaz.Rejeitada;
                venda.ChaveAcesso = resultadoEnvio.ChaveAcesso;
                await _vendaRepository.UpdateAsync(venda);

                // Salvar protocolo de rejeição
                await SalvarProtocoloRejeicao(venda, resultadoEnvio);

                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao processar venda {VendaId}", vendaId);
            
            await RegistrarErroProcessamento(venda, $"Erro interno: {ex.Message}");
            return false;
        }
    }

    public async Task<ProtocoloResponseDto?> ObterProtocoloAsync(Guid protocoloId)
    {
        _logger.LogDebug("Obtendo protocolo {ProtocoloId}", protocoloId);

        var protocolo = await _protocoloRepository.GetByIdAsync(protocoloId);
        if (protocolo == null)
        {
            _logger.LogWarning("Protocolo não encontrado - ID: {ProtocoloId}", protocoloId);
            return null;
        }

        return MapearProtocoloParaDto(protocolo);
    }

    public async Task<ProtocoloResponseDto?> ObterProtocoloPorChaveAsync(string chaveAcesso)
    {
        _logger.LogDebug("Obtendo protocolo por chave de acesso: {ChaveAcesso}", chaveAcesso);

        var protocolo = await _protocoloRepository.GetByChaveAcessoAsync(chaveAcesso);
        if (protocolo == null)
        {
            _logger.LogWarning("Protocolo não encontrado para chave de acesso: {ChaveAcesso}", chaveAcesso);
            return null;
        }

        return MapearProtocoloParaDto(protocolo);
    }

    public async Task<IEnumerable<ProtocoloResponseDto>> ObterTodosProtocolosAsync()
    {
        _logger.LogDebug("Obtendo todos os protocolos");

        var protocolos = await _protocoloRepository.GetAllAsync();
        return protocolos.Select(MapearProtocoloParaDto);
    }

    #region Métodos auxiliares

    private async Task SalvarProtocoloAutorizacao(Venda venda, SefazEnvioResult resultado)
    {
        try
        {
            var protocolo = new Protocolo
            {
                VendaId = venda.Id,
                ChaveAcesso = resultado.ChaveAcesso ?? venda.ChaveAcesso ?? string.Empty,
                NumeroProtocolo = resultado.NumeroProtocolo ?? string.Empty,
                Status = StatusSefaz.Autorizada,
                MensagemSefaz = resultado.MensagemSefaz,
                XmlNFe = resultado.XmlNFeAssinado ?? string.Empty,
                XmlProtocolo = resultado.XmlProtocolo ?? string.Empty,
                DataProtocolo = resultado.DataProtocolo ?? DateTime.Now
            };

            await _protocoloRepository.AddAsync(protocolo);
            
            _logger.LogInformation("Protocolo de autorização salvo - VendaId: {VendaId}, Protocolo: {NumeroProtocolo}", 
                venda.Id, resultado.NumeroProtocolo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar protocolo de autorização - VendaId: {VendaId}", venda.Id);
        }
    }

    private async Task SalvarProtocoloRejeicao(Venda venda, SefazEnvioResult resultado)
    {
        try
        {
            var protocolo = new Protocolo
            {
                VendaId = venda.Id,
                ChaveAcesso = resultado.ChaveAcesso ?? venda.ChaveAcesso ?? string.Empty,
                NumeroProtocolo = string.Empty,
                Status = StatusSefaz.Rejeitada,
                MensagemSefaz = resultado.MensagemSefaz,
                XmlNFe = resultado.XmlNFeAssinado ?? string.Empty,
                XmlProtocolo = resultado.XmlProtocolo ?? string.Empty,
                DataProtocolo = DateTime.Now
            };

            await _protocoloRepository.AddAsync(protocolo);
            
            _logger.LogInformation("Protocolo de rejeição salvo - VendaId: {VendaId}, Codigo: {CodigoStatus}", 
                venda.Id, resultado.CodigoStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar protocolo de rejeição - VendaId: {VendaId}", venda.Id);
        }
    }

    private async Task RegistrarErroProcessamento(Venda venda, string mensagemErro)
    {
        try
        {
            venda.Status = StatusSefaz.Erro;
            await _vendaRepository.UpdateAsync(venda);

            // Salvar protocolo de erro
            var protocoloErro = new Protocolo
            {
                VendaId = venda.Id,
                ChaveAcesso = venda.ChaveAcesso ?? string.Empty,
                NumeroProtocolo = string.Empty,
                Status = StatusSefaz.Erro,
                MensagemSefaz = mensagemErro,
                XmlNFe = string.Empty,
                XmlProtocolo = string.Empty,
                DataProtocolo = DateTime.Now
            };

            await _protocoloRepository.AddAsync(protocoloErro);
            
            _logger.LogInformation("Erro de processamento registrado - VendaId: {VendaId}", venda.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar erro de processamento - VendaId: {VendaId}", venda.Id);
        }
    }

    private VendaResponseDto MapearVendaParaDto(Venda venda)
    {
        return new VendaResponseDto
        {
            Id = venda.Id,
            ClienteNome = venda.ClienteNome,
            ClienteDocumento = venda.ClienteDocumento,
            ClienteEndereco = venda.ClienteEndereco,
            ValorTotal = venda.ValorTotal,
            DataVenda = venda.DataVenda,
            Status = venda.Status,
            ChaveAcesso = venda.ChaveAcesso,
            NumeroNFe = venda.NumeroNFe,
            SerieNFe = venda.SerieNFe,
            Observacoes = venda.Observacoes,
            Itens = venda.Itens.Select(i => new ItemVendaResponseDto
            {
                Id = i.Id,
                Codigo = i.Codigo,
                Descricao = i.Descricao,
                Quantidade = i.Quantidade,
                ValorUnitario = i.ValorUnitario,
                ValorTotal = i.ValorTotal,
                NCM = i.NCM,
                CFOP = i.CFOP,
                UnidadeMedida = i.UnidadeMedida
            }).ToList()
        };
    }

    private ProtocoloResponseDto MapearProtocoloParaDto(Protocolo protocolo)
    {
        return new ProtocoloResponseDto
        {
            Id = protocolo.Id,
            VendaId = protocolo.VendaId,
            ChaveAcesso = protocolo.ChaveAcesso,
            NumeroProtocolo = protocolo.NumeroProtocolo,
            DataProtocolo = protocolo.DataProtocolo,
            Status = protocolo.Status,
            MensagemSefaz = protocolo.MensagemSefaz
        };
    }

    #endregion
}