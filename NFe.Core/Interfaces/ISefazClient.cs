using NFe.Core.Entities;

namespace NFe.Core.Interfaces
{
    public interface ISefazClient
    {
        Task<Protocolo> TransmitirNFeAsync(string xmlAssinado);
        Task<Protocolo> ConsultarReciboAsync(string numeroRecibo);
    }
}
