namespace NFe.Core.Entities
{
    public class ItemVenda
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid VendaId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal ValorUnitario { get; set; }
        public decimal ValorTotal => Quantidade * ValorUnitario;
        public string NCM { get; set; } = "00000000"; // Código NCM
        public string CFOP { get; set; } = "5102"; // Código CFOP padrão
        public string UnidadeMedida { get; set; } = "UN";
        
        // Navegação
        public Venda? Venda { get; set; }
    }
}