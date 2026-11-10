using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using Microsoft.Data.Sqlite;
using Dapper;

namespace ModStack.Data.Repositories;

public class PortAllocationRepository : IPortAllocationRepository
{
    private readonly string _connectionString;

    public PortAllocationRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<PortAllocation>> GetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT Id, Port, ServiceId, StackId, Protocol, IsReserved, Description, CreatedAt
            FROM PortAllocations 
            ORDER BY Port";
        
        var results = await connection.QueryAsync<PortAllocation>(sql);
        return results.ToList();
    }

    public async Task<PortAllocation?> GetByIdAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT Id, Port, ServiceId, StackId, Protocol, IsReserved, Description, CreatedAt
            FROM PortAllocations 
            WHERE Id = @Id";
        
        return await connection.QuerySingleOrDefaultAsync<PortAllocation>(sql, new { Id = id });
    }

    public async Task<PortAllocation?> GetByPortAsync(int port)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT Id, Port, ServiceId, StackId, Protocol, IsReserved, Description, CreatedAt
            FROM PortAllocations 
            WHERE Port = @Port";
        
        return await connection.QuerySingleOrDefaultAsync<PortAllocation>(sql, new { Port = port });
    }

    public async Task<List<PortAllocation>> GetByServiceIdAsync(long serviceId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT Id, Port, ServiceId, StackId, Protocol, IsReserved, Description, CreatedAt
            FROM PortAllocations 
            WHERE ServiceId = @ServiceId
            ORDER BY Port";
        
        var results = await connection.QueryAsync<PortAllocation>(sql, new { ServiceId = serviceId });
        return results.ToList();
    }

    public async Task<List<PortAllocation>> GetByStackIdAsync(long stackId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT Id, Port, ServiceId, StackId, Protocol, IsReserved, Description, CreatedAt
            FROM PortAllocations 
            WHERE StackId = @StackId
            ORDER BY Port";
        
        var results = await connection.QueryAsync<PortAllocation>(sql, new { StackId = stackId });
        return results.ToList();
    }

    public async Task<PortAllocation> CreateAsync(PortAllocation allocation)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            INSERT INTO PortAllocations (Port, ServiceId, StackId, Protocol, IsReserved, Description, CreatedAt)
            VALUES (@Port, @ServiceId, @StackId, @Protocol, @IsReserved, @Description, @CreatedAt);
            SELECT last_insert_rowid();";
        
        allocation.CreatedAt = DateTime.UtcNow;
        
        var id = await connection.QuerySingleAsync<long>(sql, allocation);
        allocation.Id = id;
        return allocation;
    }

    public async Task<bool> UpdateAsync(PortAllocation allocation)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            UPDATE PortAllocations 
            SET Port = @Port, ServiceId = @ServiceId, StackId = @StackId, 
                Protocol = @Protocol, IsReserved = @IsReserved, Description = @Description
            WHERE Id = @Id";
        
        var affected = await connection.ExecuteAsync(sql, allocation);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = "DELETE FROM PortAllocations WHERE Id = @Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }
}
