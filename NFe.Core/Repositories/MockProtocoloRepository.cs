using NFe.Core.Entities;
using NFe.Core.Interfaces;

namespace NFe.Core.Repositories;

public class MockProtocoloRepository : IProtocoloRepository
{
    private static readonly Dictionary<Guid, Protocolo> _protocolos = new();
    private static readonly List<Protocolo> _protocolosList = new();

    public Task<Protocolo?> GetByIdAsync(Guid id)
    {
        if (_protocolos.ContainsKey(id))
        {
            return Task.FromResult(_protocolos[id]);
        }
        return Task.FromResult<Protocolo?>(null);
    }

    public Task<Protocolo?> GetByChaveAcessoAsync(string chaveAcesso)
    {
        var protocolo = _protocolosList.FirstOrDefault(p => p.ChaveAcesso == chaveAcesso);
        return Task.FromResult(protocolo);
    }

    public Task<Protocolo?> GetByVendaIdAsync(Guid vendaId)
    {
        var protocolo = _protocolosList.FirstOrDefault(p => p.VendaId == vendaId);
        return Task.FromResult(protocolo);
    }

    public Task<Guid> AddAsync(Protocolo protocolo)
    {
        protocolo.Id = Guid.NewGuid();
        protocolo.DataProtocolo = DateTime.Now;
        
        _protocolos[protocolo.Id] = protocolo;
        _protocolosList.Add(protocolo);
        
        return Task.FromResult(protocolo.Id);
    }

    public Task UpdateAsync(Protocolo protocolo)
    {
        if (_protocolos.ContainsKey(protocolo.Id))
        {
            _protocolos[protocolo.Id] = protocolo;
            var index = _protocolosList.FindIndex(p => p.Id == protocolo.Id);
            if (index >= 0)
            {
                _protocolosList[index] = protocolo;
            }
        }
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Protocolo>> GetAllAsync()
    {
        return Task.FromResult(_protocolosList.AsEnumerable());
    }
}

