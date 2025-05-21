using NFe.Core.Entities;

namespace NFe.Core.Interfaces
{
    public interface IVendaRepository
    {
        Task<Venda> GetByIdAsync(Guid id);
        Task<Guid> AddAsync(Venda venda);
        Task UpdateAsync(Venda venda);
        Task<IEnumerable<Venda>> GetPendentesAsync();
    }
}
