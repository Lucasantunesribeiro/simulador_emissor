using Microsoft.EntityFrameworkCore;
using NFe.Core.Entities;
using NFe.Core.Interfaces;
using NFe.Infrastructure.Data;

namespace NFe.Infrastructure.Repositories;

public class VendaRepositoryEF : IVendaRepository
{
    private readonly NFeDbContext _context;

    public VendaRepositoryEF(NFeDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> AddAsync(Venda venda)
    {
        // Calcular valor total
        venda.ValorTotal = venda.Itens.Sum(i => i.ValorTotal);
        
        _context.Vendas.Add(venda);
        await _context.SaveChangesAsync();
        return venda.Id;
    }

    public async Task<Venda?> GetByIdAsync(Guid id)
    {
        return await _context.Vendas
            .Include(v => v.Itens)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<IEnumerable<Venda>> GetAllAsync()
    {
        return await _context.Vendas
            .Include(v => v.Itens)
            .OrderByDescending(v => v.DataVenda)
            .ToListAsync();
    }

    public async Task<IEnumerable<Venda>> GetPendentesAsync()
    {
        return await _context.Vendas
            .Include(v => v.Itens)
            .Where(v => v.Status == "Pendente")
            .ToListAsync();
    }

    public async Task<IEnumerable<Venda>> GetByStatusAsync(string status)
    {
        return await _context.Vendas
            .Include(v => v.Itens)
            .Where(v => v.Status == status)
            .ToListAsync();
    }

    public async Task UpdateAsync(Venda venda)
    {
        // Recalcular valor total
        venda.ValorTotal = venda.Itens.Sum(i => i.ValorTotal);
        
        _context.Vendas.Update(venda);
        await _context.SaveChangesAsync();
    }
}