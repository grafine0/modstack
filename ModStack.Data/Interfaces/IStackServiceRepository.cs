using ModStack.Common.Models;

namespace ModStack.Data.Interfaces;

/// <summary>
/// Repository interface for stack services
/// </summary>
public interface IStackServiceRepository
{
    Task<List<StackService>> GetAllAsync();
    Task<StackService?> GetByIdAsync(long id);
    Task<List<StackService>> GetByStackIdAsync(long stackId);
    Task<List<StackService>> GetByServiceIdAsync(long serviceId);
    Task<StackService?> GetByStackAndServiceAsync(long stackId, long serviceId);
    Task<StackService> CreateAsync(StackService stackService);
    Task<bool> UpdateAsync(StackService stackService);
    Task<bool> DeleteAsync(long id);
    Task<bool> DeleteByStackIdAsync(long stackId);
    Task<bool> DeleteByStackAndServiceAsync(long stackId, long serviceId);
}
