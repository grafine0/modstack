using ModStack.Common.Models;

namespace ModStack.Data.Interfaces;

/// <summary>
/// Repository interface for port allocations
/// </summary>
public interface IPortAllocationRepository
{
    Task<List<PortAllocation>> GetAllAsync();
    Task<PortAllocation?> GetByIdAsync(long id);
    Task<PortAllocation?> GetByPortAsync(int port);
    Task<List<PortAllocation>> GetByServiceIdAsync(long serviceId);
    Task<List<PortAllocation>> GetByStackIdAsync(long stackId);
    Task<PortAllocation> CreateAsync(PortAllocation allocation);
    Task<bool> UpdateAsync(PortAllocation allocation);
    Task<bool> DeleteAsync(long id);
}
