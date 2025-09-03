namespace NFe.Core.Models;

/// <summary>
/// Resultado do envio da NFe para SEFAZ
/// </summary>
public class SefazEnvioResult
{
    public bool Success { get; set; }
    public string? ChaveAcesso { get; set; }
    public string? NumeroNFe { get; set; }
    public string? NumeroProtocolo { get; set; }
    public DateTime? DataProtocolo { get; set; }
    public string Status { get; set; } = string.Empty;
    public string MensagemSefaz { get; set; } = string.Empty;
    public string? XmlNFeAssinado { get; set; }
    public string? XmlProtocolo { get; set; }
    public int CodigoStatus { get; set; }
    public List<string> Erros { get; set; } = new();
    public List<string> Alertas { get; set; } = new();
}

/// <summary>
/// Resultado da consulta de status do lote
/// </summary>
public class SefazConsultaResult
{
    public bool Success { get; set; }
    public string Status { get; set; } = string.Empty;
    public string MensagemSefaz { get; set; } = string.Empty;
    public string? XmlProtocolo { get; set; }
    public int CodigoStatus { get; set; }
    public DateTime? DataProcessamento { get; set; }
}

/// <summary>
/// Dados do emitente da NFe
/// </summary>
public class EmitenteModel
{
    public string CNPJ { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = string.Empty;
    public string NomeFantasia { get; set; } = string.Empty;
    public string InscricaoEstadual { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string CEP { get; set; } = string.Empty;
    public string Municipio { get; set; } = string.Empty;
    public string UF { get; set; } = string.Empty;
    public int CodigoMunicipio { get; set; }
    public int RegimeTributario { get; set; }
}

/// <summary>
/// Dados do destinatário da NFe
/// </summary>
public class DestinatarioModel
{
    public string? CPF { get; set; }
    public string? CNPJ { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? InscricaoEstadual { get; set; }
    public string Endereco { get; set; } = string.Empty;
    public string Numero { get; set; } = "S/N";
    public string Bairro { get; set; } = "Centro";
    public string CEP { get; set; } = string.Empty;
    public string Municipio { get; set; } = "São Paulo";
    public string UF { get; set; } = "SP";
    public int CodigoMunicipio { get; set; } = 3550308;
    public bool ConsumidorFinal { get; set; } = true;
    public bool PresencaComprador { get; set; } = true;
}

/// <summary>
/// Item da NFe com dados fiscais
/// </summary>
public class ItemNFeModel
{
    public int Numero { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string NCM { get; set; } = string.Empty;
    public string CFOP { get; set; } = string.Empty;
    public string UnidadeComercial { get; set; } = "UN";
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }
    
    // Dados tributários
    public string OrigemMercadoria { get; set; } = "0"; // Nacional
    public string CSTICMS { get; set; } = "102"; // Tributada sem cobrança
    public decimal BaseCalculoICMS { get; set; }
    public decimal AliquotaICMS { get; set; }
    public decimal ValorICMS { get; set; }
    
    // PIS/COFINS
    public string CSTPIS { get; set; } = "07"; // Isenta
    public string CSTCOFINS { get; set; } = "07"; // Isenta
    public decimal BaseCalculoPIS { get; set; }
    public decimal AliquotaPIS { get; set; }
    public decimal ValorPIS { get; set; }
    public decimal BaseCalculoCOFINS { get; set; }
    public decimal AliquotaCOFINS { get; set; }
    public decimal ValorCOFINS { get; set; }
}

/// <summary>
/// Totais da NFe
/// </summary>
public class TotaisNFeModel
{
    public decimal BaseCalculoICMS { get; set; }
    public decimal ValorICMS { get; set; }
    public decimal BaseCalculoICMSST { get; set; }
    public decimal ValorICMSST { get; set; }
    public decimal ValorProdutos { get; set; }
    public decimal ValorFrete { get; set; }
    public decimal ValorSeguro { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorII { get; set; }
    public decimal ValorIPI { get; set; }
    public decimal ValorPIS { get; set; }
    public decimal ValorCOFINS { get; set; }
    public decimal ValorOutros { get; set; }
    public decimal ValorTotalNota { get; set; }
}

/// <summary>
/// Status possíveis da NFe no SEFAZ
/// </summary>
public static class StatusSefaz
{
    public const string Autorizada = "Autorizada";
    public const string Rejeitada = "Rejeitada";
    public const string Processando = "Processando";
    public const string Cancelada = "Cancelada";
    public const string Inutilizada = "Inutilizada";
    public const string Erro = "Erro";
}

/// <summary>
/// Códigos de status SEFAZ mais comuns
/// </summary>
public static class CodigosSefaz
{
    public const int Autorizada = 100;
    public const int LoteRecebido = 103;
    public const int LoteProcessado = 104;
    public const int ServicoParalisado = 108;
    public const int ServicoParalisadoSemPrazo = 109;
    public const int Denegada = 110;
    public const int ConsumoIndevido = 135;
    public const int RejeicaoGenerica = 999;
}