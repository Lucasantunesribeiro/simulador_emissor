namespace NFe.Core.Entities
{
    public class Protocolo
    {
        public Guid Id { get; set; }
        public required string NumeroRecibo { get; set; }
        public DateTime DataAutorizacao { get; set; }
        public required string XmlPath { get; set; }
        public required string ChaveAcesso { get; set; }
        public required string Status { get; set; }
        public required string Mensagem { get; set; }
    }
}
