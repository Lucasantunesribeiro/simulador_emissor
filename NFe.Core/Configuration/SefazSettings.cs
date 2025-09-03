namespace NFe.Core.Configuration;

public class SefazSettings
{
    /// <summary>
    /// Ambiente SEFAZ (1=Produção, 2=Homologação)
    /// </summary>
    public int Ambiente { get; set; } = 2; // Padrão homologação
    
    /// <summary>
    /// UF do emissor
    /// </summary>
    public string UF { get; set; } = "SP";
    
    /// <summary>
    /// Timeout em segundos para requisições SEFAZ
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Número de tentativas em caso de falha
    /// </summary>
    public int RetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// Nome do secret no AWS Secrets Manager contendo o certificado
    /// </summary>
    public string CertificateSecretName { get; set; } = "nfe-certificate";
    
    /// <summary>
    /// Série da NFe
    /// </summary>
    public int Serie { get; set; } = 1;
    
    /// <summary>
    /// Código do município do emitente
    /// </summary>
    public int CodigoMunicipio { get; set; } = 3550308; // São Paulo
    
    /// <summary>
    /// CNPJ do emitente
    /// </summary>
    public string CNPJ { get; set; } = string.Empty;
    
    /// <summary>
    /// Razão social do emitente
    /// </summary>
    public string RazaoSocial { get; set; } = string.Empty;
    
    /// <summary>
    /// Nome fantasia do emitente
    /// </summary>
    public string NomeFantasia { get; set; } = string.Empty;
    
    /// <summary>
    /// Endereço do emitente
    /// </summary>
    public string Endereco { get; set; } = string.Empty;
    
    /// <summary>
    /// Bairro do emitente
    /// </summary>
    public string Bairro { get; set; } = string.Empty;
    
    /// <summary>
    /// CEP do emitente
    /// </summary>
    public string CEP { get; set; } = string.Empty;
    
    /// <summary>
    /// Inscrição Estadual do emitente
    /// </summary>
    public string InscricaoEstadual { get; set; } = string.Empty;
    
    /// <summary>
    /// Regime tributário (1=Simples Nacional, 2=Simples Nacional - Excesso, 3=Regime Normal)
    /// </summary>
    public int RegimeTributario { get; set; } = 3;
}