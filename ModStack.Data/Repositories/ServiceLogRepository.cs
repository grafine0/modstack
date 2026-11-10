using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Common;
using Microsoft.Data.Sqlite;
using Dapper;

namespace ModStack.Data.Repositories;

public class ServiceLogRepository : IServiceLogRepository
{
    private readonly string _connectionString;

    public ServiceLogRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<ServiceLog>> GetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT Id, ServiceId, LogLevel, Message, Details, Timestamp
            FROM ServiceLogs 
            ORDER BY Timestamp DESC";
        
        var results = await connection.QueryAsync<ServiceLog>(sql);
        return results.ToList();
    }

    public async Task<ServiceLog?> GetByIdAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT Id, ServiceId, LogLevel, Message, Details, Timestamp
            FROM ServiceLogs 
            WHERE Id = @Id";
        
        return await connection.QuerySingleOrDefaultAsync<ServiceLog>(sql, new { Id = id });
    }

    public async Task<List<ServiceLog>> GetByServiceIdAsync(long serviceId, int limit = 100)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT Id, ServiceId, LogLevel, Message, Details, Timestamp
            FROM ServiceLogs 
            WHERE ServiceId = @ServiceId
            ORDER BY Timestamp DESC
            LIMIT @Limit";
        
        var results = await connection.QueryAsync<ServiceLog>(sql, new { ServiceId = serviceId, Limit = limit });
        return results.ToList();
    }

    public async Task<ServiceLog> CreateAsync(ServiceLog log)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            INSERT INTO ServiceLogs (ServiceId, LogLevel, Message, Details, Timestamp)
            VALUES (@ServiceId, @LogLevel, @Message, @Details, @Timestamp);
            SELECT last_insert_rowid();";
        
        log.Timestamp = DateTime.UtcNow;
        
        var id = await connection.QuerySingleAsync<long>(sql, new 
        { 
            log.ServiceId, 
            LogLevel = log.LogLevel.ToString(), 
            log.Message, 
            log.Details, 
            log.Timestamp 
        });
        log.Id = id;
        return log;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = "DELETE FROM ServiceLogs WHERE Id = @Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<bool> DeleteOldLogsAsync(long retentionDays)
    {
        using var connection = new SqliteConnection(_connectionString);
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var sql = "DELETE FROM ServiceLogs WHERE Timestamp < @CutoffDate";
        var affected = await connection.ExecuteAsync(sql, new { CutoffDate = cutoffDate });
        return affected > 0;
    }

    public async Task<List<ServiceLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT Id, ServiceId, LogLevel, Message, Details, Timestamp
            FROM ServiceLogs 
            WHERE Timestamp >= @StartDate AND Timestamp <= @EndDate
            ORDER BY Timestamp DESC";
        
        var results = await connection.QueryAsync<ServiceLog>(sql, new { StartDate = startDate, EndDate = endDate });
        return results.ToList();
    }

    public async Task<List<ServiceLog>> GetByLogLevelAsync(LogLevel logLevel)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT Id, ServiceId, LogLevel, Message, Details, Timestamp
            FROM ServiceLogs 
            WHERE LogLevel = @LogLevel
            ORDER BY Timestamp DESC";
        
        var results = await connection.QueryAsync<ServiceLog>(sql, new { LogLevel = logLevel.ToString() });
        return results.ToList();
    }
}
