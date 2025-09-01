using NFe.Core.Entities;

namespace NFe.Core.Interfaces;

public interface IMockRepository
{
    Task<T> AddAsync<T>(T entity) where T : class;
    Task<T?> GetByIdAsync<T>(Guid id) where T : class;
    Task<IEnumerable<T>> GetAllAsync<T>() where T : class;
    Task<bool> UpdateAsync<T>(T entity) where T : class;
}

