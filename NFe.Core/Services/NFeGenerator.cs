using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NFe.Core.Configuration;
using NFe.Core.Entities;
using NFe.Core.Interfaces;
using NFe.Core.Models;
using System.Text;
using System.Text.RegularExpressions;
using Unimake.Business.DFe.Xml.NFe;

namespace NFe.Core.Services;

/// <summary>
/// Gerador de XML NFe usando Unimake.DFe
/// </summary>
public class NFeGenerator : INFeGenerator
{
    private readonly SefazSettings _settings;
    private readonly ILogger<NFeGenerator> _logger;

    public NFeGenerator(
        IOptions<SefazSettings> settings,
        ILogger<NFeGenerator> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> GerarXmlNFeAsync(Venda venda)
    {
        try
        {
            _logger.LogInformation("Gerando XML NFe para venda {VendaId}", venda.Id);

            // Validar dados antes de gerar
            var erros = ValidarDadosVenda(venda);
            if (erros.Any())
            {
                throw new InvalidOperationException($"Dados inválidos para NFe: {string.Join(", ", erros)}");
            }

            // Gerar chave de acesso
            var chaveAcesso = GerarChaveAcesso(venda);
            venda.ChaveAcesso = chaveAcesso;

            // Criar objeto NFe
            var nfe = new Unimake.Business.DFe.Xml.NFe.NFe();
            
            // Configurar identificação
            nfe.InfNFe = new InfNFe
            {
                Id = $"NFe{chaveAcesso}",
                Versao = "4.00"
            };

            // IDE - Identificação do Documento Fiscal
            ConfigurarIde(nfe.InfNFe, venda, chaveAcesso);

            // Emitente
            ConfigurarEmitente(nfe.InfNFe);

            // Destinatário
            ConfigurarDestinatario(nfe.InfNFe, venda);

            // Itens da NFe
            ConfigurarItens(nfe.InfNFe, venda);

            // Totais
            ConfigurarTotais(nfe.InfNFe, venda);

            // Transporte
            ConfigurarTransporte(nfe.InfNFe);

            // Informações de Pagamento
            ConfigurarPagamento(nfe.InfNFe, venda);

            // Informações Adicionais
            ConfigurarInformacoesAdicionais(nfe.InfNFe, venda);

            var xmlNFe = nfe.GetXML();
            
            _logger.LogInformation("XML NFe gerado com sucesso - ChaveAcesso: {ChaveAcesso}", chaveAcesso);
            
            return xmlNFe;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar XML NFe para venda {VendaId}", venda.Id);
            throw;
        }
    }

    public string GerarChaveAcesso(Venda venda)
    {
        try
        {
            // Formato da chave: UFAAAAMMSSCNPJMMMVVVNNNNNNNNNDV
            // UF: 2 dígitos - código da UF
            var codigoUF = ObterCodigoUF(_settings.UF).ToString("D2");
            
            // AAAA: 4 dígitos - ano
            var ano = venda.DataVenda.ToString("yy");
            
            // MM: 2 dígitos - mês
            var mes = venda.DataVenda.ToString("MM");
            
            // SS: 2 dígitos - série
            var serie = _settings.Serie.ToString("D3");
            
            // CNPJ: 14 dígitos - CNPJ do emitente (sem pontuação)
            var cnpj = LimparDocumento(_settings.CNPJ).PadLeft(14, '0');
            
            // MMM: 3 dígitos - modelo (55 para NFe)
            var modelo = "55";
            
            // VVVVVVVVV: 9 dígitos - número sequencial da NFe
            var numeroNFe = (venda.NumeroNFe ?? "1").PadLeft(9, '0');
            
            // DV: 1 dígito - dígito verificador
            var chave43Digitos = $"{codigoUF}{ano}{mes}{serie}{cnpj}{modelo}{numeroNFe}";
            var digitoVerificador = CalcularDigitoVerificador(chave43Digitos);
            
            var chaveCompleta = chave43Digitos + digitoVerificador;
            
            _logger.LogDebug("Chave de acesso gerada: {ChaveAcesso}", chaveCompleta);
            
            return chaveCompleta;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar chave de acesso");
            throw;
        }
    }

    public List<string> ValidarDadosVenda(Venda venda)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(venda.ClienteNome))
            erros.Add("Nome do cliente é obrigatório");

        if (string.IsNullOrWhiteSpace(venda.ClienteDocumento))
            erros.Add("Documento do cliente é obrigatório");

        if (string.IsNullOrWhiteSpace(venda.ClienteEndereco))
            erros.Add("Endereço do cliente é obrigatório");

        if (!venda.Itens.Any())
            erros.Add("Pelo menos um item é obrigatório");

        foreach (var item in venda.Itens)
        {
            if (string.IsNullOrWhiteSpace(item.Descricao))
                erros.Add($"Descrição do item {item.Codigo} é obrigatória");

            if (string.IsNullOrWhiteSpace(item.NCM))
                erros.Add($"NCM do item {item.Codigo} é obrigatório");

            if (string.IsNullOrWhiteSpace(item.CFOP))
                erros.Add($"CFOP do item {item.Codigo} é obrigatório");

            if (item.Quantidade <= 0)
                erros.Add($"Quantidade do item {item.Codigo} deve ser maior que zero");

            if (item.ValorUnitario <= 0)
                erros.Add($"Valor unitário do item {item.Codigo} deve ser maior que zero");
        }

        // Validar configurações do emitente
        if (string.IsNullOrWhiteSpace(_settings.CNPJ))
            erros.Add("CNPJ do emitente não configurado");

        if (string.IsNullOrWhiteSpace(_settings.RazaoSocial))
            erros.Add("Razão social do emitente não configurada");

        return erros;
    }

    #region Configurações da NFe

    private void ConfigurarIde(InfNFe infNFe, Venda venda, string chaveAcesso)
    {
        var codigoUF = ObterCodigoUF(_settings.UF);
        var codigoNumerico = chaveAcesso.Substring(35, 8);
        var digitoVerificador = int.Parse(chaveAcesso.Substring(43, 1));

        infNFe.Ide = new Ide
        {
            CUF = codigoUF,
            CNF = int.Parse(codigoNumerico),
            NatOp = "Venda de mercadoria",
            Mod = ModeloDocumento.NFe,
            Serie = _settings.Serie,
            NNF = int.Parse(venda.NumeroNFe ?? "1"),
            DhEmi = venda.DataVenda,
            TpNF = TipoOperacao.Saida,
            IdDest = LocalDestino.Interna, // Assumindo operação interna
            CMunFG = _settings.CodigoMunicipio,
            TpImp = FormatoImpressaoDANFE.NormalRetrato,
            TpEmis = TipoEmissao.Normal,
            CDV = digitoVerificador,
            TpAmb = (TipoAmbiente)_settings.Ambiente,
            FinNFe = FinalidadeNFe.Normal,
            IndFinal = SimNao.Sim, // Consumidor final
            IndPres = IndicadorPresenca.OperacaoPresencial
        };
    }

    private void ConfigurarEmitente(InfNFe infNFe)
    {
        infNFe.Emit = new Emit
        {
            CNPJ = LimparDocumento(_settings.CNPJ),
            XNome = _settings.RazaoSocial,
            XFant = _settings.NomeFantasia,
            EnderEmit = new EnderEmit
            {
                XLgr = _settings.Endereco,
                Nro = "123", // TODO: Configurar número
                XBairro = _settings.Bairro,
                CMun = _settings.CodigoMunicipio,
                XMun = "São Paulo", // TODO: Configurar município
                UF = UFBrasil.SP, // TODO: Configurar UF
                CEP = LimparDocumento(_settings.CEP),
                CPais = 1058,
                XPais = "Brasil"
            },
            IE = _settings.InscricaoEstadual,
            CRT = (CRT)_settings.RegimeTributario
        };
    }

    private void ConfigurarDestinatario(InfNFe infNFe, Venda venda)
    {
        var documento = LimparDocumento(venda.ClienteDocumento);
        
        infNFe.Dest = new Dest
        {
            XNome = venda.ClienteNome
        };

        // Verificar se é CPF ou CNPJ
        if (documento.Length == 11)
        {
            infNFe.Dest.CPF = documento;
        }
        else
        {
            infNFe.Dest.CNPJ = documento.PadLeft(14, '0');
        }

        infNFe.Dest.EnderDest = new EnderDest
        {
            XLgr = venda.ClienteEndereco,
            Nro = "S/N",
            XBairro = "Centro",
            CMun = _settings.CodigoMunicipio,
            XMun = "São Paulo",
            UF = UFBrasil.SP,
            CEP = "01000000", // TODO: Configurar CEP do cliente
            CPais = 1058,
            XPais = "Brasil"
        };

        infNFe.Dest.IndIEDest = IndicadorIE.ContribuinteIsento;
    }

    private void ConfigurarItens(InfNFe infNFe, Venda venda)
    {
        infNFe.Det = new List<Det>();

        int numeroItem = 1;
        foreach (var item in venda.Itens)
        {
            var det = new Det
            {
                NItem = numeroItem++,
                Prod = new Prod
                {
                    CProd = item.Codigo,
                    CEAN = "SEM GTIN",
                    XProd = item.Descricao,
                    NCM = item.NCM.Replace(".", "").Replace("-", ""),
                    CFOP = item.CFOP,
                    UCom = item.UnidadeMedida ?? "UN",
                    QCom = item.Quantidade,
                    VUnCom = item.ValorUnitario,
                    VProd = item.ValorTotal,
                    CEANTrib = "SEM GTIN",
                    UTrib = item.UnidadeMedida ?? "UN",
                    QTrib = item.Quantidade,
                    VUnTrib = item.ValorUnitario,
                    IndTot = SimNao.Sim
                },
                Imposto = new Imposto
                {
                    ICMS = new ICMS
                    {
                        ICMS102 = new ICMS102
                        {
                            Orig = OrigemMercadoria.Nacional,
                            CST = "102"
                        }
                    },
                    PIS = new PIS
                    {
                        PISOutr = new PISOutr
                        {
                            CST = "07",
                            VBC = 0,
                            PPIS = 0,
                            VPIS = 0
                        }
                    },
                    COFINS = new COFINS
                    {
                        COFINSOutr = new COFINSOutr
                        {
                            CST = "07",
                            VBC = 0,
                            PCOFINS = 0,
                            VCOFINS = 0
                        }
                    }
                }
            };

            infNFe.Det.Add(det);
        }
    }

    private void ConfigurarTotais(InfNFe infNFe, Venda venda)
    {
        infNFe.Total = new Total
        {
            ICMSTot = new ICMSTot
            {
                VBC = 0,
                VICMS = 0,
                VICMSDeson = 0,
                VFCP = 0,
                VBCST = 0,
                VST = 0,
                VFCPST = 0,
                VFCPSTRet = 0,
                VProd = venda.ValorTotal,
                VFrete = 0,
                VSeg = 0,
                VDesc = 0,
                VII = 0,
                VIPI = 0,
                VIPIDevol = 0,
                VPIS = 0,
                VCOFINS = 0,
                VOutro = 0,
                VNF = venda.ValorTotal,
                VTotTrib = 0
            }
        };
    }

    private void ConfigurarTransporte(InfNFe infNFe)
    {
        infNFe.Transp = new Transp
        {
            ModFrete = ModalidadeFrete.SemOcorrenciaDeTransporte
        };
    }

    private void ConfigurarPagamento(InfNFe infNFe, Venda venda)
    {
        infNFe.Pag = new List<Pag>
        {
            new Pag
            {
                DetPag = new List<DetPag>
                {
                    new DetPag
                    {
                        IndPag = IndicadorPagamento.PagamentoVista,
                        TPag = MeioPagamento.Dinheiro,
                        VPag = venda.ValorTotal
                    }
                }
            }
        };
    }

    private void ConfigurarInformacoesAdicionais(InfNFe infNFe, Venda venda)
    {
        var observacoes = new StringBuilder();
        
        if (!string.IsNullOrWhiteSpace(venda.Observacoes))
        {
            observacoes.AppendLine(venda.Observacoes);
        }
        
        observacoes.AppendLine("NFe emitida em ambiente de homologação - sem valor fiscal");
        
        infNFe.InfAdic = new InfAdic
        {
            InfCpl = observacoes.ToString().Trim()
        };
    }

    #endregion

    #region Métodos auxiliares

    private string LimparDocumento(string documento)
    {
        return Regex.Replace(documento ?? "", @"\D", "");
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

    private int CalcularDigitoVerificador(string chave43Digitos)
    {
        var pesos = new int[] { 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        
        var soma = 0;
        for (int i = 0; i < 43; i++)
        {
            soma += int.Parse(chave43Digitos[i].ToString()) * pesos[i];
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    #endregion
}