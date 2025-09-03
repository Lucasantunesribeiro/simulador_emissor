using NFe.Core.Entities;
using NFe.Core.Interfaces;

namespace NFe.Core.Repositories;

public class MockVendaRepository : IVendaRepository
{
    private static readonly Dictionary<Guid, Venda> _vendas = new();
    private static readonly List<Venda> _vendasList = new();

    public Task<Guid> AddAsync(Venda venda)
    {
        venda.Id = Guid.NewGuid();
        venda.DataVenda = DateTime.Now;
        
        _vendas[venda.Id] = venda;
        _vendasList.Add(venda);
        
        return Task.FromResult(venda.Id);
    }

    public Task<Venda?> GetByIdAsync(Guid id)
    {
        if (_vendas.ContainsKey(id))
        {
            return Task.FromResult(_vendas[id]);
        }
        return Task.FromResult<Venda?>(null);
    }

    public Task UpdateAsync(Venda venda)
    {
        if (_vendas.ContainsKey(venda.Id))
        {
            _vendas[venda.Id] = venda;
            var index = _vendasList.FindIndex(v => v.Id == venda.Id);
            if (index >= 0)
            {
                _vendasList[index] = venda;
            }
        }
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Venda>> GetAllAsync()
    {
        return Task.FromResult(_vendasList.AsEnumerable());
    }

    public Task<IEnumerable<Venda>> GetPendentesAsync()
    {
        var vendas = _vendasList.Where(v => v.Status == "Pendente");
        return Task.FromResult(vendas);
    }

    public Task<IEnumerable<Venda>> GetByStatusAsync(string status)
    {
        var vendas = _vendasList.Where(v => v.Status == status);
        return Task.FromResult(vendas);
    }
}

