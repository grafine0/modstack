using ModStack.Common.Models;
using System.Threading;

namespace ModStack.Data.Interfaces;

/// <summary>
/// Repository interface for stacks
/// </summary>
public interface IStackRepository
{
    Task<List<Stack>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Stack?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Stack?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<Stack> CreateAsync(Stack stack, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Stack stack, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<Stack?> GetActiveStackAsync(CancellationToken cancellationToken = default);
    Task<List<Stack>> GetStacksWithProjectsAsync(CancellationToken cancellationToken = default);
}
