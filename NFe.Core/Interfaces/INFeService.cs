using NFe.Core.Entities;

namespace NFe.Core.Interfaces
{
    public interface INFeService
    {
        Task<string> GerarXml(Venda venda);
        Task<string> AssinarXml(string xml);
        Task<Protocolo> TransmitirXml(string xml);
        Task<Protocolo> ConsultarProcessamento(string numeroRecibo);

        
    }
}
