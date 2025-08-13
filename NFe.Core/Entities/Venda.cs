namespace NFe.Core.Entities
{
    public class Venda
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ClienteNome { get; set; } = string.Empty;
        public string ClienteDocumento { get; set; } = string.Empty; // CPF/CNPJ
        public string ClienteEndereco { get; set; } = string.Empty;
        public decimal ValorTotal { get; set; }
        public DateTime DataVenda { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pendente"; // Pendente, Processando, Autorizada, Rejeitada
        public string? ChaveAcesso { get; set; } // Chave de 44 dígitos da NFe
        public string? NumeroNFe { get; set; }
        public string? SerieNFe { get; set; } = "1";
        public string Observacoes { get; set; } = string.Empty;
        public List<ItemVenda> Itens { get; set; } = new List<ItemVenda>();
    }
}
