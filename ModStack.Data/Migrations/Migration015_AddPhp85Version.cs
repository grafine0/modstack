using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using ModStack.Logging;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 015: Add PHP 8.5.0 version to PackageVersions and update PHPExtensions supported versions
/// </summary>
public class Migration015_AddPhp85Version : IMigration
{
    public string Id => "015_AddPhp85Version";
    public string Description => "Add PHP 8.5.0 package version and enable 8.5 support in PHP extensions";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        try
        {
            LogService.Info($"🔧 Applying migration {Id}: {Description}");

            // Check if Packages table exists
            var packagesTableExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Packages'").ConfigureAwait(false);

            if (packagesTableExists == 0)
            {
                LogService.Info($"ℹ️ Migration {Id} skipped: Packages table does not exist");
                return;
            }

            // Locate the 'php' package
            var phpPackageId = await connection.QuerySingleOrDefaultAsync<long?>(
                "SELECT Id FROM Packages WHERE Name = 'php'").ConfigureAwait(false);

            if (phpPackageId != null)
            {
                // Ensure ServiceName column exists on PackageVersions before inserting
                var hasServiceNameColumn = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM pragma_table_info('PackageVersions') WHERE name = 'ServiceName'").ConfigureAwait(false);

                var downloadUrlsJson = JsonSerializer.Serialize(new[]
                {
                    "https://windows.php.net/downloads/releases/archives/php-8.5.0-Win32-vs17-x64.zip",
                    "https://windows.php.net/downloads/releases/php-8.5.12-1-Win32-vs17-x64.zip"
                });

                if (hasServiceNameColumn > 0)
                {
                    const string insertSql = @"
                        INSERT OR IGNORE INTO PackageVersions (
                            PackageId, Version, DisplayName, ServiceName,
                            ReleaseDate, IsStable, IsRecommended, IsLTS,
                            Size, DownloadUrls
                        )
                        VALUES (
                            @PackageId, @Version, @DisplayName, @ServiceName,
                            @ReleaseDate, @IsStable, @IsRecommended, @IsLTS,
                            @Size, @DownloadUrls
                        )";

                    await connection.ExecuteAsync(insertSql, new
                    {
                        PackageId = phpPackageId.Value,
                        Version = "8.5.0",
                        DisplayName = "PHP 8.5.0",
                        ServiceName = "php-8.5.0",
                        ReleaseDate = new DateTime(2025, 11, 20, 0, 0, 0, DateTimeKind.Utc).ToString("O"),
                        IsStable = true,
                        IsRecommended = false,
                        IsLTS = false,
                        Size = 33554432L,
                        DownloadUrls = downloadUrlsJson
                    }).ConfigureAwait(false);
                }
                else
                {
                    const string insertFallbackSql = @"
                        INSERT OR IGNORE INTO PackageVersions (
                            PackageId, Version, DisplayName,
                            ReleaseDate, IsStable, IsRecommended, IsLTS,
                            Size, DownloadUrls
                        )
                        VALUES (
                            @PackageId, @Version, @DisplayName,
                            @ReleaseDate, @IsStable, @IsRecommended, @IsLTS,
                            @Size, @DownloadUrls
                        )";

                    await connection.ExecuteAsync(insertFallbackSql, new
                    {
                        PackageId = phpPackageId.Value,
                        Version = "8.5.0",
                        DisplayName = "PHP 8.5.0",
                        ReleaseDate = new DateTime(2025, 11, 20, 0, 0, 0, DateTimeKind.Utc).ToString("O"),
                        IsStable = true,
                        IsRecommended = false,
                        IsLTS = false,
                        Size = 33554432L,
                        DownloadUrls = downloadUrlsJson
                    }).ConfigureAwait(false);
                }

                LogService.Info($"✅ Added PHP 8.5.0 to PackageVersions (PackageId: {phpPackageId.Value})");
            }
            else
            {
                LogService.Info($"ℹ️ Migration {Id}: PHP package not yet present in Packages table");
            }

            // Update PHPExtensions table so extensions supporting 8.4 also support 8.5
            var extensionsTableExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='PHPExtensions'").ConfigureAwait(false);

            if (extensionsTableExists > 0)
            {
                var extensions = await connection.QueryAsync<(long Id, string SupportedPHPVersions)>(
                    "SELECT Id, SupportedPHPVersions FROM PHPExtensions WHERE SupportedPHPVersions LIKE '%8.4%'").ConfigureAwait(false);

                foreach (var ext in extensions)
                {
                    try
                    {
                        var versions = JsonSerializer.Deserialize<List<string>>(ext.SupportedPHPVersions) ?? [];
                        if (versions.Contains("8.4") && !versions.Contains("8.5"))
                        {
                            versions.Add("8.5");
                            var updatedJson = JsonSerializer.Serialize(versions);
                            await connection.ExecuteAsync(
                                "UPDATE PHPExtensions SET SupportedPHPVersions = @SupportedPHPVersions, UpdatedAt = CURRENT_TIMESTAMP WHERE Id = @Id",
                                new { SupportedPHPVersions = updatedJson, ext.Id }).ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                        // Ignore malformed JSON rows
                    }
                }
            }

            LogService.Info($"✅ Applied migration {Id}: {Description}");
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Failed to apply migration {Id}: {Description}", ex);
            throw;
        }
    }
}

