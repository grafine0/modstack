using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using Microsoft.Data.Sqlite;
using Dapper;

namespace ModStack.Data.Repositories;

/// <summary>
/// Repository implementation for application settings
/// </summary>
public class SettingsRepository : ISettingsRepository
{
    private readonly string _connectionString;

    public SettingsRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<AppSetting?> GetByCategoryAndKeyAsync(string category, string key)
    {
        using var connection = new SqliteConnection(_connectionString);
        const string sql = @"
            SELECT Id, Category, Key, Value, DataType, Description, IsUserSetting, CreatedAt, UpdatedAt
            FROM Settings 
            WHERE Category = @Category AND Key = @Key";

        return await connection.QueryFirstOrDefaultAsync<AppSetting>(sql, new { Category = category, Key = key });
    }

    public async Task<List<AppSetting>> GetByCategoryAsync(string category)
    {
        using var connection = new SqliteConnection(_connectionString);
        const string sql = @"
            SELECT Id, Category, Key, Value, DataType, Description, IsUserSetting, CreatedAt, UpdatedAt
            FROM Settings 
            WHERE Category = @Category
            ORDER BY Key";

        var result = await connection.QueryAsync<AppSetting>(sql, new { Category = category });
        return result.ToList();
    }

    public async Task<List<AppSetting>> GetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        const string sql = @"
            SELECT Id, Category, Key, Value, DataType, Description, IsUserSetting, CreatedAt, UpdatedAt
            FROM Settings 
            ORDER BY Category, Key";

        var result = await connection.QueryAsync<AppSetting>(sql);
        return result.ToList();
    }

    public async Task<AppSetting> CreateAsync(AppSetting setting)
    {
        using var connection = new SqliteConnection(_connectionString);
        const string sql = @"
            INSERT INTO Settings (Category, Key, Value, DataType, Description, IsUserSetting, CreatedAt, UpdatedAt)
            VALUES (@Category, @Key, @Value, @DataType, @Description, @IsUserSetting, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();";

        var id = await connection.QuerySingleAsync<long>(sql, setting);
        setting.Id = id;
        return setting;
    }

    public async Task<AppSetting> UpdateAsync(AppSetting setting)
    {
        using var connection = new SqliteConnection(_connectionString);
        const string sql = @"
            UPDATE Settings 
            SET Category = @Category, Key = @Key, Value = @Value, DataType = @DataType, 
                Description = @Description, IsUserSetting = @IsUserSetting, UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        await connection.ExecuteAsync(sql, setting);
        return setting;
    }

    public async Task DeleteAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        const string sql = "DELETE FROM Settings WHERE Id = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });
    }
}
