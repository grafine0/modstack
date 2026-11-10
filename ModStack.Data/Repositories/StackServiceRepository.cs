using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Text.Json;

namespace ModStack.Data.Repositories;

public class StackServiceRepository : IStackServiceRepository
{
    private readonly string _connectionString;

    public StackServiceRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<StackService>> GetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var sql = @"
            SELECT Id, StackId, ServiceId, Port, Priority, IsEnabled, Configuration
            FROM StackServices 
            ORDER BY StackId, Priority";
        
        var results = await connection.QueryAsync<StackServiceData>(sql);
        return results.Select(ConvertFromData).ToList();
    }

    public async Task<StackService?> GetByIdAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var sql = @"
            SELECT Id, StackId, ServiceId, Port, Priority, IsEnabled, Configuration
            FROM StackServices 
            WHERE Id = @Id";
        
        var result = await connection.QuerySingleOrDefaultAsync<StackServiceData>(sql, new { Id = id });
        return result != null ? ConvertFromData(result) : null;
    }

    public async Task<List<StackService>> GetByStackIdAsync(long stackId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var sql = @"
            SELECT ss.Id, ss.StackId, ss.ServiceId, ss.Port, ss.Priority, ss.IsEnabled, ss.Configuration,
                   s.Id, s.Name, s.DisplayName, s.ServiceType, s.DefaultPort, s.BinaryPath as DefaultPath, s.Version as DefaultVersion,
                   s.IsInstalled, s.IsEnabled, s.InstallDate, s.CreatedAt, s.UpdatedAt
            FROM StackServices ss
            INNER JOIN Services s ON ss.ServiceId = s.Id
            WHERE ss.StackId = @StackId
            ORDER BY ss.Priority, s.Name";
        
        var results = await connection.QueryAsync<StackServiceData, ServiceDefinition, StackService>(
            sql,
            (stackServiceData, service) =>
            {
                var stackService = ConvertFromData(stackServiceData);
                stackService.Service = service;
                return stackService;
            },
            new { StackId = stackId },
            splitOn: "Id");
        
        return results.ToList();
    }

    public async Task<List<StackService>> GetByServiceIdAsync(long serviceId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        const string sql = @"
            SELECT ss.Id, ss.StackId, ss.ServiceId, ss.Port, ss.Priority, ss.IsEnabled, ss.Configuration,
                   s.Id, s.Name, s.DisplayName, s.ServiceType, s.DefaultPort, s.BinaryPath as DefaultPath, s.Version as DefaultVersion,
                   s.IsInstalled, s.IsEnabled, s.InstallDate, s.CreatedAt, s.UpdatedAt
            FROM StackServices ss
            INNER JOIN Services s ON ss.ServiceId = s.Id
            WHERE ss.ServiceId = @ServiceId
            ORDER BY ss.Priority, s.Name";
        
        var results = await connection.QueryAsync<StackServiceData, ServiceDefinition, StackService>(
            sql,
            (stackServiceData, service) =>
            {
                var stackService = ConvertFromData(stackServiceData);
                stackService.Service = service;
                return stackService;
            },
            new { ServiceId = serviceId },
            splitOn: "Id");
        
        return results.ToList();
    }

    public async Task<StackService?> GetByStackAndServiceAsync(long stackId, long serviceId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var sql = @"
            SELECT Id, StackId, ServiceId, Port, Priority, IsEnabled, Configuration
            FROM StackServices 
            WHERE StackId = @StackId AND ServiceId = @ServiceId";
        
        var result = await connection.QuerySingleOrDefaultAsync<StackServiceData>(sql, new { StackId = stackId, ServiceId = serviceId });
        return result != null ? ConvertFromData(result) : null;
    }

    public async Task<StackService> CreateAsync(StackService stackService)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            System.Diagnostics.Debug.WriteLine($"🔧 StackServiceRepository.CreateAsync: Connection opened successfully");
            System.Diagnostics.Debug.WriteLine($"   StackId: {stackService.StackId}, ServiceId: {stackService.ServiceId}");
            System.Diagnostics.Debug.WriteLine($"   Port: {stackService.Port}, Priority: {stackService.Priority}, IsEnabled: {stackService.IsEnabled}");
            
            var sql = @"
                INSERT INTO StackServices (StackId, ServiceId, Port, Priority, IsEnabled, Configuration)
                VALUES (@StackId, @ServiceId, @Port, @Priority, @IsEnabled, @Configuration);
                SELECT last_insert_rowid();";
            
            var data = ConvertToData(stackService);
            System.Diagnostics.Debug.WriteLine($"🔧 Converted to data object, executing SQL...");
            
            var id = await connection.QuerySingleAsync<long>(sql, data);
            stackService.Id = id;
            System.Diagnostics.Debug.WriteLine($"✅ StackService inserted with ID: {id}");
            return stackService;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ StackServiceRepository.CreateAsync failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<bool> UpdateAsync(StackService stackService)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var sql = @"
            UPDATE StackServices 
            SET StackId = @StackId, ServiceId = @ServiceId, Port = @Port, 
                Priority = @Priority, IsEnabled = @IsEnabled, Configuration = @Configuration
            WHERE Id = @Id";
        
        var data = ConvertToData(stackService);
        var affected = await connection.ExecuteAsync(sql, data);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var sql = "DELETE FROM StackServices WHERE Id = @Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<bool> DeleteByStackIdAsync(long stackId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var sql = "DELETE FROM StackServices WHERE StackId = @StackId";
        var affected = await connection.ExecuteAsync(sql, new { StackId = stackId });
        return affected > 0;
    }

    public async Task<bool> DeleteByStackAndServiceAsync(long stackId, long serviceId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var sql = "DELETE FROM StackServices WHERE StackId = @StackId AND ServiceId = @ServiceId";
        var affected = await connection.ExecuteAsync(sql, new { StackId = stackId, ServiceId = serviceId });
        return affected > 0;
    }

    private static StackServiceData ConvertToData(StackService stackService)
    {
        return new StackServiceData
        {
            Id = stackService.Id,
            StackId = stackService.StackId,
            ServiceId = stackService.ServiceId,
            Port = stackService.Port,
            Priority = stackService.Priority,
            IsEnabled = stackService.IsEnabled,
            Configuration = stackService.Configuration.Any() ? JsonSerializer.Serialize(stackService.Configuration) : null
        };
    }

    private static StackService ConvertFromData(StackServiceData data)
    {
        var stackService = new StackService
        {
            Id = data.Id,
            StackId = data.StackId,
            ServiceId = data.ServiceId,
            Port = data.Port,
            Priority = data.Priority,
            IsEnabled = data.IsEnabled
        };

        if (!string.IsNullOrEmpty(data.Configuration) && data.Configuration.StartsWith("{"))
        {
            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(data.Configuration);
                stackService.Configuration = config ?? new Dictionary<string, object>();
            }
            catch
            {
                stackService.Configuration = new Dictionary<string, object>();
            }
        }
        else
        {
            stackService.Configuration = new Dictionary<string, object>();
        }

        return stackService;
    }

    private class StackServiceData
    {
        public long Id { get; set; }
        public long StackId { get; set; }
        public long ServiceId { get; set; }
        public int? Port { get; set; }
        public int Priority { get; set; }
        public bool IsEnabled { get; set; }
        public string? Configuration { get; set; }
    }
}
