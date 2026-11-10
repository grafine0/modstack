using Microsoft.Data.Sqlite;
using ModStack.Logging;
using Dapper;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 010: Update PHP package configuration with new extension options
/// </summary>
public class Migration010_UpdatePHPConfiguration : IMigration
{
    public string Id => "010_UpdatePHPConfiguration";
    public string Description => "Update PHP package configuration to include comprehensive extension options";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        try
        {
            LogService.Info($"🔧 Applying migration {Id}: {Description}");

            // Check if PHP package exists
            var phpPackageId = await connection.QuerySingleOrDefaultAsync<long?>(
                "SELECT Id FROM Packages WHERE Name = 'php'");
            
            if (phpPackageId == null)
            {
                LogService.Info($"ℹ️ Migration {Id} skipped: PHP package not found");
                return;
            }

            // New comprehensive PHP configuration
            var newConfiguration = @"[
                {
                    ""Key"": ""memory_limit"",
                    ""DisplayName"": ""Memory Limit"",
                    ""Description"": """",
                    ""Type"": ""text"",
                    ""DefaultValue"": ""256M"",
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""max_execution_time"",
                    ""DisplayName"": ""Max Execution Time"",
                    ""Description"": """",
                    ""Type"": ""number"",
                    ""DefaultValue"": 30,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""upload_max_filesize"",
                    ""DisplayName"": ""Upload Max Filesize"",
                    ""Description"": """",
                    ""Type"": ""text"",
                    ""DefaultValue"": ""2M"",
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""post_max_size"",
                    ""DisplayName"": ""Post Max Size"",
                    ""Description"": """",
                    ""Type"": ""text"",
                    ""DefaultValue"": ""8M"",
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_fileinfo"",
                    ""DisplayName"": ""Enable Fileinfo"",
                    ""Description"": ""File information functions"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": true,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_mbstring"",
                    ""DisplayName"": ""Enable MBString"",
                    ""Description"": ""Multibyte string functions"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": true,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_openssl"",
                    ""DisplayName"": ""Enable OpenSSL"",
                    ""Description"": ""OpenSSL cryptographic functions"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": true,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_curl"",
                    ""DisplayName"": ""Enable cURL"",
                    ""Description"": ""Client URL library"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": true,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_zip"",
                    ""DisplayName"": ""Enable ZIP"",
                    ""Description"": ""ZIP archive handling"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": true,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_xml"",
                    ""DisplayName"": ""Enable XML"",
                    ""Description"": ""XML parsing functions"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": true,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_json"",
                    ""DisplayName"": ""Enable JSON"",
                    ""Description"": ""JSON encoding/decoding"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": true,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_pdo_mysql"",
                    ""DisplayName"": ""Enable PDO MySQL"",
                    ""Description"": ""PDO MySQL driver"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": true,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_mysqli"",
                    ""DisplayName"": ""Enable MySQLi"",
                    ""Description"": ""MySQL improved extension"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": true,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_gd"",
                    ""DisplayName"": ""Enable GD"",
                    ""Description"": ""Image processing library"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": true,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_opcache"",
                    ""DisplayName"": ""Enable OPcache"",
                    ""Description"": ""Opcode cache for improved performance"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": true,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_xdebug"",
                    ""DisplayName"": ""Enable Xdebug"",
                    ""Description"": ""Debugging and profiling extension"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": false,
                    ""Options"": [],
                    ""IsRequired"": false
                },
                {
                    ""Key"": ""enable_ioncube"",
                    ""DisplayName"": ""Enable ionCube Loader"",
                    ""Description"": ""Loader for ionCube encoded PHP files"",
                    ""Type"": ""boolean"",
                    ""DefaultValue"": false,
                    ""Options"": [],
                    ""IsRequired"": false
                }
            ]";

            // Update the PHP package configuration
            await connection.ExecuteAsync(@"
                UPDATE PackageConfigurations 
                SET Configuration = @Configuration 
                WHERE PackageId = @PackageId",
                new { 
                    Configuration = newConfiguration, 
                    PackageId = phpPackageId 
                });

            LogService.Info($"✅ Applied migration {Id}: Updated PHP package configuration with {17} extension options");
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Failed to apply migration {Id}: {Description}", ex);
            throw;
        }
    }
}
