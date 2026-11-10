using Microsoft.Data.Sqlite;
using ModStack.Logging;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 002: Add performance indexes to key tables
/// </summary>
public class Migration002_AddPerformanceIndexes : IMigration
{
    public string Id => "002_AddPerformanceIndexes";
    public string Description => "Add performance indexes to Services and Packages tables";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_services_name ON Services(Name)",
            "CREATE INDEX IF NOT EXISTS idx_services_type ON Services(ServiceType)",
            "CREATE INDEX IF NOT EXISTS idx_services_installed ON Services(IsInstalled)",
            "CREATE INDEX IF NOT EXISTS idx_packages_name ON Packages(Name)",
            "CREATE INDEX IF NOT EXISTS idx_packages_category ON Packages(Category)",
            "CREATE INDEX IF NOT EXISTS idx_packages_popular ON Packages(IsPopular)",
            "CREATE INDEX IF NOT EXISTS idx_package_versions_package_id ON PackageVersions(PackageId)",
            "CREATE INDEX IF NOT EXISTS idx_package_versions_recommended ON PackageVersions(IsRecommended)"
        };

        foreach (var indexSql in indexes)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = indexSql;
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                
                var indexName = ExtractIndexName(indexSql);
                LogService.Info($"✅ Created index: {indexName}");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1) // SQLITE_ERROR - index already exists
            {
                var indexName = ExtractIndexName(indexSql);
                LogService.Info($"ℹ️ Index already exists: {indexName}");
            }
        }

        LogService.Info($"✅ Applied migration {Id}: {Description}");
    }

    private static string ExtractIndexName(string sql)
    {
        // Extract index name from CREATE INDEX statement
        var parts = sql.Split(' ');
        var indexIndex = Array.IndexOf(parts, "INDEX");
        if (indexIndex >= 0 && indexIndex + 2 < parts.Length)
        {
            return parts[indexIndex + 2];
        }
        return "unknown";
    }
}
