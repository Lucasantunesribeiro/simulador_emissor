using NFe.Core.Entities;

namespace NFe.Core.Interfaces
{
    public interface IProtocoloRepository
    {
        Task<Protocolo> GetByIdAsync(Guid id);
        Task<Protocolo> GetByChaveAcessoAsync(string chaveAcesso);
        Task<Guid> AddAsync(Protocolo protocolo);
        Task UpdateAsync(Protocolo protocolo);
    }
}
