using NFe.Core.Entities;
using NFe.Core.Interfaces;

namespace NFe.Infrastructure.Repositories
{
    // IMPLEMENTAÇÃO DE SIMULAÇÃO: Armazena protocolos em memória.
    // Para produção, troque por repositório com banco de dados (ex: EF Core, Dapper, etc).
    public class ProtocoloRepository : IProtocoloRepository
    {
        private readonly List<Protocolo> _protocolos = new List<Protocolo>();

        public Task<Protocolo> GetByIdAsync(Guid id)
        {
            return Task.FromResult(
                _protocolos.FirstOrDefault(p => p.Id == id)
                ?? throw new InvalidOperationException("Protocolo não encontrado."));
        }

        public Task<Protocolo> GetByChaveAcessoAsync(string chaveAcesso)
        {
            return Task.FromResult(
                _protocolos.FirstOrDefault(p => p.ChaveAcesso == chaveAcesso)
                ?? throw new InvalidOperationException("Protocolo não encontrado."));
        }

        public Task<Guid> AddAsync(Protocolo protocolo)
        {
            if (protocolo.Id == Guid.Empty)
            {
                protocolo.Id = Guid.NewGuid();
            }
            
            _protocolos.Add(protocolo);
            return Task.FromResult(protocolo.Id);
        }

        public Task UpdateAsync(Protocolo protocolo)
        {
            var existingIndex = _protocolos.FindIndex(p => p.Id == protocolo.Id);
            if (existingIndex >= 0)
            {
                _protocolos[existingIndex] = protocolo;
            }
            
            return Task.CompletedTask;
        }
    }
}
