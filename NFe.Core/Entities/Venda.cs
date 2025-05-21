namespace NFe.Core.Entities
{
    public class Venda
    {
        public Guid Id { get; set; }
        public required string ClienteDocumento { get; set; }
        public required string ClienteNome { get; set; }
        public DateTime DataVenda { get; set; }
        public decimal ValorTotal { get; set; }
        public required string Observacoes { get; set; }
        public bool Processada { get; set; }
        public required string NumeroNota { get; set; }
        public required string ChaveAcesso { get; set; }
    }
}
