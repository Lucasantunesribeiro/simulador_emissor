using NFe.Core.Entities;
using NFe.Core.Interfaces;

namespace NFe.Infrastructure.Repositories
{
    // IMPLEMENTAÇÃO DE SIMULAÇÃO: Armazena vendas em memória.
    // Para produção, troque por repositório com banco de dados (ex: EF Core, Dapper, etc).
    public class VendaRepository : IVendaRepository
    {
        private readonly List<Venda> _vendas = new List<Venda>();

        public Task<Venda> GetByIdAsync(Guid id)
        {
            return Task.FromResult(
                _vendas.FirstOrDefault(v => v.Id == id)
                ?? throw new InvalidOperationException("Venda não encontrada."));
        }

        public Task<Guid> AddAsync(Venda venda)
        {
            if (venda.Id == Guid.Empty)
            {
                venda.Id = Guid.NewGuid();
            }
            
            _vendas.Add(venda);
            return Task.FromResult(venda.Id);
        }

        public Task UpdateAsync(Venda venda)
        {
            var existingIndex = _vendas.FindIndex(v => v.Id == venda.Id);
            if (existingIndex >= 0)
            {
                _vendas[existingIndex] = venda;
            }
            
            return Task.CompletedTask;
        }

        public Task<IEnumerable<Venda>> GetPendentesAsync()
        {
            return Task.FromResult(_vendas.Where(v => !v.Processada));
        }
    }
}
