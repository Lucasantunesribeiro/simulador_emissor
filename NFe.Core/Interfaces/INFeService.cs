using NFe.Core.DTOs;

namespace NFe.Core.Interfaces
{
    public interface INFeService
    {
        Task<Guid> CriarVendaAsync(VendaCreateDto vendaDto);
        Task<VendaResponseDto?> ObterVendaAsync(Guid id);
        Task<IEnumerable<VendaResponseDto>> ObterTodasVendasAsync();
        Task<IEnumerable<VendaResponseDto>> ObterVendasPendentesAsync();
        Task<bool> ProcessarVendaAsync(Guid vendaId);
        Task<ProtocoloResponseDto?> ObterProtocoloAsync(Guid protocoloId);
        Task<ProtocoloResponseDto?> ObterProtocoloPorChaveAsync(string chaveAcesso);
        Task<IEnumerable<ProtocoloResponseDto>> ObterTodosProtocolosAsync();
    }
}
