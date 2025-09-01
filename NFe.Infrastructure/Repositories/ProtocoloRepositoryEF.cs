using Microsoft.EntityFrameworkCore;
using NFe.Core.Entities;
using NFe.Core.Interfaces;
using NFe.Infrastructure.Data;

namespace NFe.Infrastructure.Repositories;

public class ProtocoloRepositoryEF : IProtocoloRepository
{
    private readonly NFeDbContext _context;

    public ProtocoloRepositoryEF(NFeDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> AddAsync(Protocolo protocolo)
    {
        _context.Protocolos.Add(protocolo);
        await _context.SaveChangesAsync();
        return protocolo.Id;
    }

    public async Task<Protocolo?> GetByIdAsync(Guid id)
    {
        return await _context.Protocolos
            .Include(p => p.Venda)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Protocolo?> GetByChaveAcessoAsync(string chaveAcesso)
    {
        return await _context.Protocolos
            .Include(p => p.Venda)
            .FirstOrDefaultAsync(p => p.ChaveAcesso == chaveAcesso);
    }

    public async Task<Protocolo?> GetByVendaIdAsync(Guid vendaId)
    {
        return await _context.Protocolos
            .Include(p => p.Venda)
            .FirstOrDefaultAsync(p => p.VendaId == vendaId);
    }

    public async Task<IEnumerable<Protocolo>> GetAllAsync()
    {
        return await _context.Protocolos
            .Include(p => p.Venda)
            .OrderByDescending(p => p.DataProtocolo)
            .ToListAsync();
    }

    public async Task UpdateAsync(Protocolo protocolo)
    {
        _context.Protocolos.Update(protocolo);
        await _context.SaveChangesAsync();
    }
}