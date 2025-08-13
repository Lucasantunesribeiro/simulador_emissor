namespace NFe.Core.DTOs
{
    public class VendaResponseDto
    {
        public Guid Id { get; set; }
        public string ClienteNome { get; set; } = string.Empty;
        public string ClienteDocumento { get; set; } = string.Empty;
        public string ClienteEndereco { get; set; } = string.Empty;
        public decimal ValorTotal { get; set; }
        public DateTime DataVenda { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ChaveAcesso { get; set; }
        public string? NumeroNFe { get; set; }
        public string? SerieNFe { get; set; }
        public string Observacoes { get; set; } = string.Empty;
        public List<ItemVendaResponseDto> Itens { get; set; } = new List<ItemVendaResponseDto>();
    }
    
    public class ItemVendaResponseDto
    {
        public Guid Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal ValorUnitario { get; set; }
        public decimal ValorTotal { get; set; }
        public string NCM { get; set; } = string.Empty;
        public string CFOP { get; set; } = string.Empty;
        public string UnidadeMedida { get; set; } = string.Empty;
    }
    
    public class ProtocoloResponseDto
    {
        public Guid Id { get; set; }
        public Guid VendaId { get; set; }
        public string ChaveAcesso { get; set; } = string.Empty;
        public string NumeroProtocolo { get; set; } = string.Empty;
        public DateTime DataProtocolo { get; set; }
        public string Status { get; set; } = string.Empty;
        public string MensagemSefaz { get; set; } = string.Empty;
    }
}