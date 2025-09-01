using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NFe.Core.Configuration;
using NFe.Core.Interfaces;
using NFe.Core.Models;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using Unimake.Business.DFe.Servicos.NFe;
using Unimake.Business.DFe.Xml.NFe;

namespace NFe.Core.Services;

/// <summary>
/// Cliente para comunicação com SEFAZ usando Unimake.DFe
/// </summary>
public class SefazClient : ISefazClient
{
    private readonly SefazSettings _settings;
    private readonly ICertificateService _certificateService;
    private readonly ILogger<SefazClient> _logger;

    public SefazClient(
        IOptions<SefazSettings> settings,
        ICertificateService certificateService,
        ILogger<SefazClient> logger)
    {
        _settings = settings.Value;
        _certificateService = certificateService;
        _logger = logger;
    }

    public async Task<SefazEnvioResult> EnviarNFeAsync(string xmlNFeAssinado)
    {
        try
        {
            _logger.LogInformation("Iniciando envio de NFe para SEFAZ - Ambiente: {Ambiente}, UF: {UF}", 
                _settings.Ambiente, _settings.UF);

            var certificado = await _certificateService.ObterCertificadoAsync();
            if (certificado == null)
            {
                return CriarResultadoErro("Certificado digital não encontrado");
            }

            // Criar objeto NFe a partir do XML
            var nfeXml = new XmlDocument();
            nfeXml.LoadXml(xmlNFeAssinado);
            
            var enviNFe = new EnviNFe
            {
                Versao = "4.00",
                IdLote = GerarIdLote(),
                IndSinc = SimNao.Nao, // Assincrono
                NFe = new List<Unimake.Business.DFe.Xml.NFe.NFe>()
            };

            // Converter XML para objeto NFe
            var nfe = new Unimake.Business.DFe.Xml.NFe.NFe();
            nfe.LoadFromXML(xmlNFeAssinado);
            enviNFe.NFe.Add(nfe);

            // Configurar serviço
            var servico = new Envio(nfe.InfNFe.Ide.CUF, certificado)
            {
                TpAmb = (TipoAmbiente)_settings.Ambiente
            };

            // Executar envio
            servico.Executar(enviNFe, certificado);

            var resultado = servico.Result;
            var chaveAcesso = nfe.InfNFe.ChaveAcesso;

            _logger.LogInformation("Resposta SEFAZ - Status: {Status}, Motivo: {Motivo}", 
                resultado.CStat, resultado.XMotivo);

            if (resultado.CStat == 103) // Lote recebido
            {
                // Aguardar processamento do lote
                var consultaResult = await ConsultarStatusLoteAsync(resultado.InfRec.NRec);
                
                if (consultaResult.Success && consultaResult.CodigoStatus == 100) // Autorizada
                {
                    return new SefazEnvioResult
                    {
                        Success = true,
                        ChaveAcesso = chaveAcesso,
                        NumeroNFe = nfe.InfNFe.Ide.NNF?.ToString(),
                        NumeroProtocolo = consultaResult.XmlProtocolo?.ExtractProtocolNumber(),
                        DataProtocolo = consultaResult.DataProcessamento ?? DateTime.Now,
                        Status = StatusSefaz.Autorizada,
                        MensagemSefaz = consultaResult.MensagemSefaz,
                        XmlNFeAssinado = xmlNFeAssinado,
                        XmlProtocolo = consultaResult.XmlProtocolo,
                        CodigoStatus = consultaResult.CodigoStatus
                    };
                }
                else
                {
                    return new SefazEnvioResult
                    {
                        Success = false,
                        ChaveAcesso = chaveAcesso,
                        Status = StatusSefaz.Rejeitada,
                        MensagemSefaz = consultaResult.MensagemSefaz,
                        CodigoStatus = consultaResult.CodigoStatus,
                        Erros = new List<string> { consultaResult.MensagemSefaz }
                    };
                }
            }
            else if (resultado.CStat == 100) // Autorizada sincrona
            {
                return new SefazEnvioResult
                {
                    Success = true,
                    ChaveAcesso = chaveAcesso,
                    NumeroNFe = nfe.InfNFe.Ide.NNF?.ToString(),
                    Status = StatusSefaz.Autorizada,
                    MensagemSefaz = resultado.XMotivo,
                    XmlNFeAssinado = xmlNFeAssinado,
                    CodigoStatus = resultado.CStat
                };
            }
            else
            {
                return CriarResultadoErro($"Erro SEFAZ {resultado.CStat}: {resultado.XMotivo}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar NFe para SEFAZ");
            return CriarResultadoErro($"Erro interno: {ex.Message}");
        }
    }

    public async Task<SefazConsultaResult> ConsultarStatusLoteAsync(string recibo)
    {
        try
        {
            _logger.LogInformation("Consultando status do lote: {Recibo}", recibo);

            var certificado = await _certificateService.ObterCertificadoAsync();
            if (certificado == null)
            {
                return new SefazConsultaResult
                {
                    Success = false,
                    MensagemSefaz = "Certificado digital não encontrado"
                };
            }

            var consReciNFe = new ConsReciNFe
            {
                Versao = "4.00",
                TpAmb = (TipoAmbiente)_settings.Ambiente,
                NRec = recibo
            };

            var servico = new ConsultaRecibo(ObterCodigoUF(_settings.UF), certificado);
            servico.Executar(consReciNFe, certificado);

            var resultado = servico.Result;

            _logger.LogInformation("Resultado consulta lote - Status: {Status}, Motivo: {Motivo}", 
                resultado.CStat, resultado.XMotivo);

            return new SefazConsultaResult
            {
                Success = resultado.CStat == 104, // Lote processado
                Status = resultado.CStat == 104 ? StatusSefaz.Autorizada : StatusSefaz.Rejeitada,
                MensagemSefaz = resultado.XMotivo,
                CodigoStatus = resultado.CStat,
                DataProcessamento = DateTime.Now,
                XmlProtocolo = resultado?.GetXML()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar status do lote: {Recibo}", recibo);
            return new SefazConsultaResult
            {
                Success = false,
                MensagemSefaz = $"Erro ao consultar lote: {ex.Message}"
            };
        }
    }

    public async Task<SefazConsultaResult> ConsultarProtocoloAsync(string chaveAcesso)
    {
        try
        {
            _logger.LogInformation("Consultando protocolo da NFe: {ChaveAcesso}", chaveAcesso);

            var certificado = await _certificateService.ObterCertificadoAsync();
            if (certificado == null)
            {
                return new SefazConsultaResult
                {
                    Success = false,
                    MensagemSefaz = "Certificado digital não encontrado"
                };
            }

            var consSitNFe = new ConsSitNFe
            {
                Versao = "4.00",
                TpAmb = (TipoAmbiente)_settings.Ambiente,
                ChNFe = chaveAcesso
            };

            var servico = new ConsultaProtocolo(ObterCodigoUF(_settings.UF), certificado);
            servico.Executar(consSitNFe, certificado);

            var resultado = servico.Result;

            _logger.LogInformation("Resultado consulta protocolo - Status: {Status}, Motivo: {Motivo}", 
                resultado.CStat, resultado.XMotivo);

            return new SefazConsultaResult
            {
                Success = resultado.CStat == 100,
                Status = resultado.CStat == 100 ? StatusSefaz.Autorizada : StatusSefaz.Rejeitada,
                MensagemSefaz = resultado.XMotivo,
                CodigoStatus = resultado.CStat,
                DataProcessamento = DateTime.Now,
                XmlProtocolo = resultado?.GetXML()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar protocolo da NFe: {ChaveAcesso}", chaveAcesso);
            return new SefazConsultaResult
            {
                Success = false,
                MensagemSefaz = $"Erro ao consultar protocolo: {ex.Message}"
            };
        }
    }

    public async Task<bool> VerificarStatusServicoAsync()
    {
        try
        {
            _logger.LogInformation("Verificando status do serviço SEFAZ - UF: {UF}", _settings.UF);

            var certificado = await _certificateService.ObterCertificadoAsync();
            if (certificado == null)
            {
                _logger.LogWarning("Certificado digital não encontrado para verificação de status");
                return false;
            }

            var consStatServ = new ConsStatServ
            {
                Versao = "4.00",
                TpAmb = (TipoAmbiente)_settings.Ambiente,
                CUF = ObterCodigoUF(_settings.UF)
            };

            var servico = new StatusServico(ObterCodigoUF(_settings.UF), certificado);
            servico.Executar(consStatServ, certificado);

            var resultado = servico.Result;
            var disponivel = resultado.CStat == 107; // Serviço em operação

            _logger.LogInformation("Status serviço SEFAZ - Disponível: {Disponivel}, Status: {Status}, Motivo: {Motivo}", 
                disponivel, resultado.CStat, resultado.XMotivo);

            return disponivel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar status do serviço SEFAZ");
            return false;
        }
    }

    #region Métodos auxiliares

    private int GerarIdLote()
    {
        return new Random().Next(100000, 999999);
    }

    private int ObterCodigoUF(string uf)
    {
        return uf.ToUpper() switch
        {
            "SP" => 35,
            "RJ" => 33,
            "MG" => 31,
            "RS" => 43,
            "PR" => 41,
            "SC" => 42,
            "BA" => 29,
            "GO" => 52,
            "ES" => 32,
            "PE" => 26,
            "CE" => 23,
            "PA" => 15,
            "MT" => 51,
            "MS" => 50,
            "DF" => 53,
            "AL" => 27,
            "RN" => 20,
            "PB" => 21,
            "SE" => 28,
            "PI" => 22,
            "MA" => 21,
            "TO" => 17,
            "AM" => 13,
            "RO" => 11,
            "AC" => 12,
            "AP" => 16,
            "RR" => 14,
            _ => 35 // Default SP
        };
    }

    private SefazEnvioResult CriarResultadoErro(string mensagem)
    {
        return new SefazEnvioResult
        {
            Success = false,
            Status = StatusSefaz.Erro,
            MensagemSefaz = mensagem,
            Erros = new List<string> { mensagem }
        };
    }

    #endregion
}

/// <summary>
/// Extensions para XML de protocolo
/// </summary>
public static class ProtocoloXmlExtensions
{
    public static string? ExtractProtocolNumber(this string? xmlProtocolo)
    {
        if (string.IsNullOrEmpty(xmlProtocolo)) return null;
        
        try
        {
            var xml = new XmlDocument();
            xml.LoadXml(xmlProtocolo);
            return xml.SelectSingleNode("//nProt")?.InnerText;
        }
        catch
        {
            return null;
        }
    }
}