using Microsoft.Data.Sqlite;
using ModStack.Logging;
using Dapper;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 012: Add IsSystemPackage and IsSystemVersion flags
/// </summary>
public class Migration012_AddSystemPackageFlags : IMigration
{
    public string Id => "012_AddSystemPackageFlags";
    public string Description => "Add IsSystemPackage to Packages and IsSystemVersion to PackageVersions tables";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        try
        {
            LogService.Info($"🔧 Applying migration {Id}: {Description}");

            // Add IsSystemPackage column to Packages table
            await connection.ExecuteAsync(@"
                ALTER TABLE Packages 
                ADD COLUMN IsSystemPackage INTEGER NOT NULL DEFAULT 0");

            LogService.Info("✅ Added IsSystemPackage column to Packages table");

            // Add IsSystemVersion column to PackageVersions table
            await connection.ExecuteAsync(@"
                ALTER TABLE PackageVersions 
                ADD COLUMN IsSystemVersion INTEGER NOT NULL DEFAULT 0");

            LogService.Info("✅ Added IsSystemVersion column to PackageVersions table");

            // Mark existing packages as system packages (shipped with application)
            var systemPackageNames = new[] { "apache", "mysql", "php", "phpmyadmin", "nginx", "postgresql", "redis" };
            
            foreach (var packageName in systemPackageNames)
            {
                await connection.ExecuteAsync(@"
                    UPDATE Packages 
                    SET IsSystemPackage = 1 
                    WHERE LOWER(Name) = @PackageName",
                    new { PackageName = packageName.ToLower() });
            }

            LogService.Info($"✅ Marked {systemPackageNames.Length} existing packages as system packages");

            // Mark existing package versions as system versions
            await connection.ExecuteAsync(@"
                UPDATE PackageVersions 
                SET IsSystemVersion = 1 
                WHERE PackageId IN (SELECT Id FROM Packages WHERE IsSystemPackage = 1)");

            var markedVersions = await connection.QuerySingleAsync<int>(@"
                SELECT COUNT(*) FROM PackageVersions WHERE IsSystemVersion = 1");

            LogService.Info($"✅ Marked {markedVersions} existing versions as system versions");

            LogService.Info($"✅ Migration {Id} applied successfully");
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Error applying migration {Id}: {ex.Message}", ex);
            throw;
        }
    }
}

