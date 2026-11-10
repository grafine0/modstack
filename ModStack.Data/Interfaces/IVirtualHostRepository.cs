using ModStack.Common.Models;

namespace ModStack.Data.Interfaces;

/// <summary>
/// Repository interface for virtual host data access
/// </summary>
public interface IVirtualHostRepository
{
    Task<List<VirtualHost>> GetAllAsync();
    Task<VirtualHost?> GetByIdAsync(long id);
    Task<List<VirtualHost>> GetByProjectIdAsync(long projectId);
    Task<List<VirtualHost>> GetByServiceIdAsync(long serviceId);
    Task<VirtualHost?> GetByServerNameAsync(string serverName);
    Task<VirtualHost> CreateAsync(VirtualHost virtualHost);
    Task<bool> UpdateAsync(VirtualHost virtualHost);
    Task<bool> DeleteAsync(long id);
    Task<bool> DeleteByProjectIdAsync(long projectId);
    Task<List<VirtualHost>> GetEnabledVirtualHostsAsync();
    Task<bool> ServerNameExistsAsync(string serverName, long? excludeId = null);
}
