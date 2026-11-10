using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Text.Json;
using System.Data;
using System.Linq;

namespace ModStack.Data.Repositories;

public class StackRepository : IStackRepository
{
    private readonly string _connectionString;

    public StackRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<Stack>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);  // ✅ Open the connection!
        
        // Add custom type mapping for Configuration
        SqlMapper.AddTypeHandler(new ConfigurationTypeHandler());
        
        var sql = @"
            SELECT Id, Name, DisplayName, Description, IsActive, IsDefault, Status,
                   COALESCE(Configuration, '{}') as Configuration, CreatedAt, UpdatedAt
            FROM Stacks 
            ORDER BY Name";
        
        var stacks = await connection.QueryAsync<Stack>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        var result = stacks.ToList();
        
        System.Diagnostics.Debug.WriteLine($"🔍 StackRepository.GetAllAsync: Found {result.Count} stacks");
        
        // Load services for each stack
        foreach (var stack in result)
        {
            stack.Services = await GetStackServicesAsync(connection, stack.Id, cancellationToken);
            System.Diagnostics.Debug.WriteLine($"   Stack '{stack.DisplayName}': Loaded {stack.Services.Count} services, IsActive={stack.IsActive}, Status={stack.Status}");
            foreach (var service in stack.Services)
            {
                System.Diagnostics.Debug.WriteLine($"      - Service: {service.Service.Name} (ID: {service.ServiceId}, IsEnabled: {service.IsEnabled})");
            }
            System.Diagnostics.Debug.WriteLine($"   CanStart = {stack.CanStart} (IsActive: {stack.IsActive}, HasServices: {stack.Services.Any()})");
        }
        
        return result;
    }

    public async Task<Stack?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);  // ✅ Open the connection!
        
        // Add custom type mapping for Configuration
        SqlMapper.AddTypeHandler(new ConfigurationTypeHandler());
        
        var sql = @"
            SELECT Id, Name, DisplayName, Description, IsActive, IsDefault, Status,
                   COALESCE(Configuration, '{}') as Configuration, CreatedAt, UpdatedAt
            FROM Stacks 
            WHERE Id = @Id";
        
        var stack = await connection.QuerySingleOrDefaultAsync<Stack>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        if (stack != null)
        {
            stack.Services = await GetStackServicesAsync(connection, stack.Id, cancellationToken);
        }
        
        return stack;
    }

    public async Task<Stack?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        
        // Add custom type mapping for Configuration
        SqlMapper.AddTypeHandler(new ConfigurationTypeHandler());
        
        var sql = @"
            SELECT Id, Name, DisplayName, Description, IsActive, IsDefault, 
                   COALESCE(Configuration, '{}') as Configuration, CreatedAt, UpdatedAt
            FROM Stacks 
            WHERE Name = @Name";
        
        var stack = await connection.QuerySingleOrDefaultAsync<Stack>(new CommandDefinition(sql, new { Name = name }, cancellationToken: cancellationToken));
        if (stack != null)
        {
            stack.Services = await GetStackServicesAsync(connection, stack.Id, cancellationToken);
        }
        
        return stack;
    }

    public async Task<Stack> CreateAsync(Stack stack, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        
        // Add custom type mapping for Configuration
        SqlMapper.AddTypeHandler(new ConfigurationTypeHandler());
        
        var sql = @"
            INSERT INTO Stacks (Name, DisplayName, Description, IsActive, IsDefault, Configuration, CreatedAt, UpdatedAt)
            VALUES (@Name, @DisplayName, @Description, @IsActive, @IsDefault, @Configuration, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();";
        
        stack.CreatedAt = DateTime.UtcNow;
        stack.UpdatedAt = DateTime.UtcNow;
        
        var id = await connection.QuerySingleAsync<long>(new CommandDefinition(sql, stack, cancellationToken: cancellationToken));
        stack.Id = id;
        return stack;
    }

    public async Task<bool> UpdateAsync(Stack stack, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        
        // Add custom type mapping for Configuration
        SqlMapper.AddTypeHandler(new ConfigurationTypeHandler());
        
        var sql = @"
            UPDATE Stacks 
            SET Name = @Name, DisplayName = @DisplayName, Description = @Description,
                IsActive = @IsActive, IsDefault = @IsDefault, Configuration = @Configuration, UpdatedAt = @UpdatedAt
            WHERE Id = @Id";
        
        stack.UpdatedAt = DateTime.UtcNow;
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, stack, cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();
        
        try
        {
            // Delete stack services first (foreign key constraint)
            var deleteServicesSQL = "DELETE FROM StackServices WHERE StackId = @Id";
            await connection.ExecuteAsync(new CommandDefinition(deleteServicesSQL, new { Id = id }, transaction, cancellationToken: cancellationToken));
            
            // Delete the stack
            var deleteStackSQL = "DELETE FROM Stacks WHERE Id = @Id";
            var affected = await connection.ExecuteAsync(new CommandDefinition(deleteStackSQL, new { Id = id }, transaction, cancellationToken: cancellationToken));
            
            transaction.Commit();
            return affected > 0;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<Stack?> GetActiveStackAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        
        // Add custom type mapping for Configuration
        SqlMapper.AddTypeHandler(new ConfigurationTypeHandler());
        
        var sql = @"
            SELECT Id, Name, DisplayName, Description, IsActive, IsDefault, 
                   COALESCE(Configuration, '{}') as Configuration, CreatedAt, UpdatedAt
            FROM Stacks 
            WHERE IsActive = 1
            ORDER BY UpdatedAt DESC
            LIMIT 1";
        
        var stack = await connection.QuerySingleOrDefaultAsync<Stack>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        if (stack != null)
        {
            stack.Services = await GetStackServicesAsync(connection, stack.Id, cancellationToken);
        }
        
        return stack;
    }

    public async Task<List<Stack>> GetStacksWithProjectsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        
        // Add custom type mapping for Configuration
        SqlMapper.AddTypeHandler(new ConfigurationTypeHandler());
        
        var sql = @"
            SELECT DISTINCT s.Id, s.Name, s.DisplayName, s.Description, s.IsActive, s.IsDefault, 
                   COALESCE(s.Configuration, '{}') as Configuration, s.CreatedAt, s.UpdatedAt
            FROM Stacks s
            INNER JOIN Projects p ON s.Id = p.StackId
            ORDER BY s.Name";
        
        var stacks = await connection.QueryAsync<Stack>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        var result = stacks.ToList();
        
        // Load services for each stack
        foreach (var stack in result)
        {
            stack.Services = await GetStackServicesAsync(connection, stack.Id, cancellationToken);
        }
        
        return result;
    }

    private async Task<List<StackService>> GetStackServicesAsync(SqliteConnection connection, long stackId, CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT ss.Id, ss.StackId, ss.ServiceId, ss.Port, ss.Priority, ss.IsEnabled, 
                   COALESCE(ss.Configuration, '{}') as Configuration,
                   s.Id as ServiceDefinitionId, s.Name, s.DisplayName, s.ServiceType, s.DefaultPort, s.BinaryPath as DefaultPath, s.Version as DefaultVersion,
                   s.IsInstalled, s.IsEnabled as ServiceEnabled, s.InstallDate, s.CreatedAt, s.UpdatedAt
            FROM StackServices ss
            INNER JOIN Services s ON ss.ServiceId = s.Id
            WHERE ss.StackId = @StackId
            ORDER BY ss.Priority, s.Name";
        
        var result = await connection.QueryAsync<StackService, ServiceDefinition, StackService>(
            new CommandDefinition(sql, new { StackId = stackId }, cancellationToken: cancellationToken),
            (stackService, service) =>
            {
                stackService.Service = service;
                return stackService;
            },
            splitOn: "ServiceDefinitionId");
        
        return result.ToList();
    }
}

/// <summary>
/// Custom type handler for Dapper to serialize/deserialize Configuration dictionary
/// </summary>
public class ConfigurationTypeHandler : SqlMapper.TypeHandler<Dictionary<string, object>>
{
    public override void SetValue(IDbDataParameter parameter, Dictionary<string, object>? value)
    {
        parameter.Value = value != null ? JsonSerializer.Serialize(value) : "{}";
    }

    public override Dictionary<string, object> Parse(object? value)
    {
        if (value == null || value == DBNull.Value)
            return new Dictionary<string, object>();

        var jsonString = value.ToString();
        if (string.IsNullOrEmpty(jsonString))
            return new Dictionary<string, object>();

        try
        {
            // Deserialize as JsonDocument to handle JsonElement values properly
            using var document = JsonDocument.Parse(jsonString);
            var result = new Dictionary<string, object>();
            
            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = ConvertJsonElementToObject(property.Value);
            }
            
            return result;
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Converts JsonElement to appropriate .NET object
    /// </summary>
    private static object ConvertJsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => ConvertNumberElement(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToObject).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                prop => prop.Name, 
                prop => ConvertJsonElementToObject(prop.Value)),
            _ => element.ToString()
        };
    }

    private static object ConvertNumberElement(JsonElement element)
    {
        if (element.TryGetInt32(out var intVal))
        {
            return intVal;
        }
        return element.GetDouble();
    }
}
