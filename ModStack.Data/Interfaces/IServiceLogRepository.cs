using ModStack.Common.Models;

namespace ModStack.Data.Interfaces;

/// <summary>
/// Repository interface for service logs
/// </summary>
public interface IServiceLogRepository
{
    Task<List<ServiceLog>> GetAllAsync();
    Task<ServiceLog?> GetByIdAsync(long id);
    Task<List<ServiceLog>> GetByServiceIdAsync(long serviceId, int limit = 100);
    Task<ServiceLog> CreateAsync(ServiceLog log);
    Task<bool> DeleteAsync(long id);
    Task<bool> DeleteOldLogsAsync(long retentionDays);
    Task<List<ServiceLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<List<ServiceLog>> GetByLogLevelAsync(ModStack.Common.LogLevel logLevel);
}
