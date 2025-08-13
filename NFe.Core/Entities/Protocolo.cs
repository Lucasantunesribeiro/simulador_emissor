namespace NFe.Core.Entities
{
    public class Protocolo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid VendaId { get; set; }
        public string ChaveAcesso { get; set; } = string.Empty;
        public string NumeroProtocolo { get; set; } = string.Empty;
        public DateTime DataProtocolo { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = string.Empty; // Autorizada, Rejeitada, Processando
        public string MensagemSefaz { get; set; } = string.Empty;
        public string XmlNFe { get; set; } = string.Empty; // XML da NFe
        public string XmlProtocolo { get; set; } = string.Empty; // XML do protocolo
        
        // Navegação
        public Venda? Venda { get; set; }
    }
}
