using Dapper;
using Microsoft.Data.Sqlite;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Logging;
using System.Globalization;
using System.Text.Json;

namespace ModStack.Data.Repositories;

/// <summary>
/// Repository for managing PHP extensions
/// </summary>
public class PHPExtensionRepository(string connectionString) : IPHPExtensionRepository
{
    private readonly string _connectionString = connectionString;

    #region Available Extensions

    public async Task<List<PHPExtension>> GetAvailableExtensionsAsync()
    {
        const string sql = @"
            SELECT Id, Name, DisplayName, Description, Category, IsBuiltIn, IsZendExtension, 
                   DllName, SupportedPHPVersions, Dependencies, DownloadUrl, 
                   ConfigurationTemplate, IsPopular, CreatedAt, UpdatedAt 
            FROM PHPExtensions 
            ORDER BY IsPopular DESC, DisplayName";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var extensions = await connection.QueryAsync<dynamic>(sql);
        
        return [.. extensions.Select(ConvertToPHPExtension)];
    }

    public async Task<List<PHPExtension>> GetExtensionsByCategoryAsync(string category)
    {
        const string sql = @"
            SELECT Id, Name, DisplayName, Description, Category, IsBuiltIn, IsZendExtension, 
                   DllName, SupportedPHPVersions, Dependencies, DownloadUrl, 
                   ConfigurationTemplate, IsPopular, CreatedAt, UpdatedAt 
            FROM PHPExtensions 
            WHERE Category = @Category
            ORDER BY IsPopular DESC, DisplayName";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var extensions = await connection.QueryAsync<dynamic>(sql, new { Category = category });
        
        return [.. extensions.Select(ConvertToPHPExtension)];
    }

    public async Task<List<PHPExtension>> GetPopularExtensionsAsync()
    {
        const string sql = @"
            SELECT Id, Name, DisplayName, Description, Category, IsBuiltIn, IsZendExtension, 
                   DllName, SupportedPHPVersions, Dependencies, DownloadUrl, 
                   ConfigurationTemplate, IsPopular, CreatedAt, UpdatedAt 
            FROM PHPExtensions 
            WHERE IsPopular = 1
            ORDER BY DisplayName";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var extensions = await connection.QueryAsync<dynamic>(sql);
        
        return [.. extensions.Select(ConvertToPHPExtension)];
    }

    public async Task<List<PHPExtension>> GetExtensionsForPHPVersionAsync(string phpVersion)
    {
        const string sql = @"
            SELECT Id, Name, DisplayName, Description, Category, IsBuiltIn, IsZendExtension, 
                   DllName, SupportedPHPVersions, Dependencies, DownloadUrl, 
                   ConfigurationTemplate, IsPopular, CreatedAt, UpdatedAt 
            FROM PHPExtensions 
            WHERE SupportedPHPVersions LIKE @PHPVersion
            ORDER BY IsPopular DESC, DisplayName";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var extensions = await connection.QueryAsync<dynamic>(sql, new { PHPVersion = $"%{phpVersion}%" });
        
        return [.. extensions.Select(ConvertToPHPExtension)];
    }

    public async Task<PHPExtension?> GetExtensionByNameAsync(string name)
    {
        const string sql = @"
            SELECT Id, Name, DisplayName, Description, Category, IsBuiltIn, IsZendExtension, 
                   DllName, SupportedPHPVersions, Dependencies, DownloadUrl, 
                   ConfigurationTemplate, IsPopular, CreatedAt, UpdatedAt 
            FROM PHPExtensions 
            WHERE Name = @Name";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var extension = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { Name = name });
        
        return extension != null ? ConvertToPHPExtension(extension) : null;
    }

    public async Task<PHPExtension?> GetExtensionByIdAsync(long id)
    {
        const string sql = @"
            SELECT Id, Name, DisplayName, Description, Category, IsBuiltIn, IsZendExtension, 
                   DllName, SupportedPHPVersions, Dependencies, DownloadUrl, 
                   ConfigurationTemplate, IsPopular, CreatedAt, UpdatedAt 
            FROM PHPExtensions 
            WHERE Id = @Id";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var extension = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { Id = id });
        
        return extension != null ? ConvertToPHPExtension(extension) : null;
    }

    #endregion

    #region Installed Extensions

    public async Task<List<InstalledPHPExtension>> GetInstalledExtensionsAsync(long serviceId)
    {
        const string sql = @"
            SELECT ie.Id, ie.ServiceId, ie.ExtensionId, ie.PHPVersion, ie.InstallPath, 
                   ie.IsEnabled, ie.Configuration, ie.InstalledAt,
                   e.Name, e.DisplayName, e.Description, e.Category, e.IsBuiltIn, 
                   e.IsZendExtension, e.DllName, e.IsPopular
            FROM InstalledPHPExtensions ie
            INNER JOIN PHPExtensions e ON ie.ExtensionId = e.Id
            WHERE ie.ServiceId = @ServiceId
            ORDER BY e.DisplayName";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var results = await connection.QueryAsync<dynamic>(sql, new { ServiceId = serviceId });
        
        return [.. results.Select(ConvertToInstalledPHPExtension)];
    }

    public async Task<List<InstalledPHPExtension>> GetInstalledExtensionsForVersionAsync(long serviceId, string phpVersion)
    {
        const string sql = @"
            SELECT ie.Id, ie.ServiceId, ie.ExtensionId, ie.PHPVersion, ie.InstallPath, 
                   ie.IsEnabled, ie.Configuration, ie.InstalledAt,
                   e.Name, e.DisplayName, e.Description, e.Category, e.IsBuiltIn, 
                   e.IsZendExtension, e.DllName, e.IsPopular
            FROM InstalledPHPExtensions ie
            INNER JOIN PHPExtensions e ON ie.ExtensionId = e.Id
            WHERE ie.ServiceId = @ServiceId AND ie.PHPVersion = @PHPVersion
            ORDER BY e.DisplayName";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var results = await connection.QueryAsync<dynamic>(sql, new { ServiceId = serviceId, PHPVersion = phpVersion });
        
        return [.. results.Select(ConvertToInstalledPHPExtension)];
    }

    public async Task<InstalledPHPExtension?> GetInstalledExtensionAsync(long serviceId, long extensionId)
    {
        const string sql = @"
            SELECT ie.Id, ie.ServiceId, ie.ExtensionId, ie.PHPVersion, ie.InstallPath, 
                   ie.IsEnabled, ie.Configuration, ie.InstalledAt,
                   e.Name, e.DisplayName, e.Description, e.Category, e.IsBuiltIn, 
                   e.IsZendExtension, e.DllName, e.IsPopular
            FROM InstalledPHPExtensions ie
            INNER JOIN PHPExtensions e ON ie.ExtensionId = e.Id
            WHERE ie.ServiceId = @ServiceId AND ie.ExtensionId = @ExtensionId";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { ServiceId = serviceId, ExtensionId = extensionId });
        
        return result != null ? ConvertToInstalledPHPExtension(result) : null;
    }

    public async Task<bool> IsExtensionInstalledAsync(long serviceId, long extensionId)
    {
        // First check database record
        const string sql = @"
            SELECT COUNT(1) FROM InstalledPHPExtensions 
            WHERE ServiceId = @ServiceId AND ExtensionId = @ExtensionId";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var count = await connection.QuerySingleAsync<long>(sql, new { ServiceId = serviceId, ExtensionId = extensionId });
        
        return count > 0;
    }


    #endregion

    #region Extension Management

    public async Task<InstalledPHPExtension> InstallExtensionAsync(InstallPHPExtensionRequest request)
    {
        LogService.Info($"🔧 Starting InstallExtensionAsync - ServiceId: {request.ServiceId}, ExtensionId: {request.ExtensionId}, PHPVersion: {request.PHPVersion}");
        
        // Validate foreign key constraints before insertion
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        LogService.Info($"📡 Database connection opened successfully");
        
        // Check if ServiceId exists
        LogService.Info($"🔍 Checking if ServiceId {request.ServiceId} exists in Services table");
        var serviceExists = await connection.QuerySingleAsync<bool>(
            "SELECT COUNT(1) > 0 FROM Services WHERE Id = @ServiceId", 
            new { request.ServiceId });
        
        LogService.Info($"✅ ServiceId {request.ServiceId} exists: {serviceExists}");
        if (!serviceExists)
        {
            LogService.Error($"❌ Service with ID {request.ServiceId} does not exist in Services table");
            throw new InvalidOperationException($"Service with ID {request.ServiceId} does not exist");
        }
        
        // Check if ExtensionId exists
        LogService.Info($"🔍 Checking if ExtensionId {request.ExtensionId} exists in PHPExtensions table");
        var extensionExists = await connection.QuerySingleAsync<bool>(
            "SELECT COUNT(1) > 0 FROM PHPExtensions WHERE Id = @ExtensionId", 
            new { request.ExtensionId });
        
        LogService.Info($"✅ ExtensionId {request.ExtensionId} exists: {extensionExists}");
        if (!extensionExists)
        {
            LogService.Error($"❌ Extension with ID {request.ExtensionId} does not exist in PHPExtensions table");
            throw new InvalidOperationException($"Extension with ID {request.ExtensionId} does not exist");
        }

        LogService.Info($"📝 Preparing to insert into InstalledPHPExtensions table");
        const string sql = @"
            INSERT INTO InstalledPHPExtensions (ServiceId, ExtensionId, PHPVersion, InstallPath, IsEnabled, Configuration, InstalledAt)
            VALUES (@ServiceId, @ExtensionId, @PHPVersion, @InstallPath, @IsEnabled, @Configuration, @InstalledAt)
            RETURNING Id";
        
        var installedExtension = new InstalledPHPExtension
        {
            ServiceId = request.ServiceId,
            ExtensionId = request.ExtensionId,
            PHPVersion = request.PHPVersion,
            InstallPath = "", // Will be set by the extension manager
            IsEnabled = request.EnableAfterInstall,
            Configuration = JsonSerializer.Serialize(request.Configuration),
            InstalledAt = DateTime.UtcNow
        };

        LogService.Info($"📊 Extension data prepared - ServiceId: {installedExtension.ServiceId}, ExtensionId: {installedExtension.ExtensionId}, IsEnabled: {installedExtension.IsEnabled}");
        LogService.Info($"🔧 Configuration: {installedExtension.Configuration}");
        
        try
        {
            LogService.Info($"💾 Executing INSERT statement into InstalledPHPExtensions");
            
            // Debug: Check what tables exist in the database
            var tables = await connection.QueryAsync<string>("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name");
            LogService.Info($"📋 Available tables: {string.Join(", ", tables)}");
            
            // Debug: Check foreign key constraints on InstalledPHPExtensions table
            var foreignKeys = await connection.QueryAsync<dynamic>("PRAGMA foreign_key_list(InstalledPHPExtensions)");
            LogService.Info($"🔗 Foreign key constraints on InstalledPHPExtensions: {string.Join(", ", foreignKeys.Select(fk => $"{fk.table}.{fk.from} -> {fk.to}"))}");
            
            var id = await connection.QuerySingleAsync<long>(sql, installedExtension);
            installedExtension.Id = id;
            
            LogService.Info($"✅ Successfully inserted InstalledPHPExtension with ID: {id}");
            LogService.Info($"🎉 InstallExtensionAsync completed successfully for ServiceId: {request.ServiceId}, ExtensionId: {request.ExtensionId}");
            
            return installedExtension;
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Failed to insert into InstalledPHPExtensions: {ex.Message}");
            LogService.Error($"🔍 SQL: {sql}");
            LogService.Error($"📊 Parameters - ServiceId: {installedExtension.ServiceId}, ExtensionId: {installedExtension.ExtensionId}, PHPVersion: {installedExtension.PHPVersion}");
            LogService.Error($"📊 Parameters - InstallPath: '{installedExtension.InstallPath}', IsEnabled: {installedExtension.IsEnabled}");
            LogService.Error($"📊 Parameters - Configuration: '{installedExtension.Configuration}', InstalledAt: {installedExtension.InstalledAt}");
            throw;
        }
    }

    public async Task<bool> UninstallExtensionAsync(long serviceId, long extensionId)
    {
        const string sql = @"
            DELETE FROM InstalledPHPExtensions 
            WHERE ServiceId = @ServiceId AND ExtensionId = @ExtensionId";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { ServiceId = serviceId, ExtensionId = extensionId });
        
        return rowsAffected > 0;
    }

    public async Task<bool> EnableExtensionAsync(long serviceId, long extensionId)
    {
        const string sql = @"
            UPDATE InstalledPHPExtensions 
            SET IsEnabled = 1 
            WHERE ServiceId = @ServiceId AND ExtensionId = @ExtensionId";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { ServiceId = serviceId, ExtensionId = extensionId });
        
        return rowsAffected > 0;
    }

    public async Task<bool> DisableExtensionAsync(long serviceId, long extensionId)
    {
        const string sql = @"
            UPDATE InstalledPHPExtensions 
            SET IsEnabled = 0 
            WHERE ServiceId = @ServiceId AND ExtensionId = @ExtensionId";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { ServiceId = serviceId, ExtensionId = extensionId });
        
        return rowsAffected > 0;
    }

    public async Task<bool> UpdateExtensionConfigurationAsync(long serviceId, long extensionId, string configuration)
    {
        const string sql = @"
            UPDATE InstalledPHPExtensions 
            SET Configuration = @Configuration 
            WHERE ServiceId = @ServiceId AND ExtensionId = @ExtensionId";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { ServiceId = serviceId, ExtensionId = extensionId, Configuration = configuration });
        
        return rowsAffected > 0;
    }

    #endregion

    #region Extension Data Management

    public async Task<PHPExtension> CreateExtensionAsync(PHPExtension extension)
    {
        const string sql = @"
            INSERT INTO PHPExtensions (Name, DisplayName, Description, Category, IsBuiltIn, IsZendExtension, 
                                     DllName, SupportedPHPVersions, Dependencies, DownloadUrl, 
                                     ConfigurationTemplate, IsPopular, CreatedAt, UpdatedAt)
            VALUES (@Name, @DisplayName, @Description, @Category, @IsBuiltIn, @IsZendExtension, 
                    @DllName, @SupportedPHPVersions, @Dependencies, @DownloadUrl, 
                    @ConfigurationTemplate, @IsPopular, @CreatedAt, @UpdatedAt)
            RETURNING Id";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        extension.CreatedAt = DateTime.UtcNow;
        extension.UpdatedAt = DateTime.UtcNow;
        
        var parameters = new
        {
            extension.Name,
            extension.DisplayName,
            extension.Description,
            extension.Category,
            extension.IsBuiltIn,
            extension.IsZendExtension,
            extension.DllName,
            SupportedPHPVersions = JsonSerializer.Serialize(extension.SupportedPHPVersions),
            Dependencies = JsonSerializer.Serialize(extension.Dependencies),
            extension.DownloadUrl,
            extension.ConfigurationTemplate,
            extension.IsPopular,
            extension.CreatedAt,
            extension.UpdatedAt
        };

        var id = await connection.QuerySingleAsync<long>(sql, parameters);
        extension.Id = id;
        
        return extension;
    }

    public async Task<bool> UpdateExtensionAsync(PHPExtension extension)
    {
        const string sql = @"
            UPDATE PHPExtensions 
            SET DisplayName = @DisplayName, Description = @Description, Category = @Category, 
                IsBuiltIn = @IsBuiltIn, IsZendExtension = @IsZendExtension, DllName = @DllName, 
                SupportedPHPVersions = @SupportedPHPVersions, Dependencies = @Dependencies, 
                DownloadUrl = @DownloadUrl, ConfigurationTemplate = @ConfigurationTemplate, 
                IsPopular = @IsPopular, UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        extension.UpdatedAt = DateTime.UtcNow;
        
        var parameters = new
        {
            extension.Id,
            extension.DisplayName,
            extension.Description,
            extension.Category,
            extension.IsBuiltIn,
            extension.IsZendExtension,
            extension.DllName,
            SupportedPHPVersions = JsonSerializer.Serialize(extension.SupportedPHPVersions),
            Dependencies = JsonSerializer.Serialize(extension.Dependencies),
            extension.DownloadUrl,
            extension.ConfigurationTemplate,
            extension.IsPopular,
            extension.UpdatedAt
        };

        var rowsAffected = await connection.ExecuteAsync(sql, parameters);
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteExtensionAsync(long id)
    {
        const string sql = "DELETE FROM PHPExtensions WHERE Id = @Id";

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
        
        return rowsAffected > 0;
    }

    #endregion

    #region Initialization

    public async Task InitializeDefaultExtensionsAsync()
    {
        // Ensure each default extension exists (idempotent upsert by Name)
        var defaultExtensions = GetDefaultExtensions();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var extension in defaultExtensions)
        {
            // Check by unique Name
            const string findSql = "SELECT Id, Name, DisplayName, Description, Category, IsBuiltIn, IsZendExtension, DllName, SupportedPHPVersions, Dependencies, DownloadUrl, ConfigurationTemplate, IsPopular, CreatedAt, UpdatedAt FROM PHPExtensions WHERE Name = @Name";
            var existing = await connection.QueryFirstOrDefaultAsync<dynamic>(findSql, new { extension.Name });

            if (existing == null)
            {
                // Insert missing extension
                await CreateExtensionAsync(extension);
                continue;
            }

            // Optionally refresh metadata (keep DB Id, update fields if changed)
            var existingModel = ConvertToPHPExtension(existing);

            bool needsUpdate =
                existingModel.DisplayName != extension.DisplayName ||
                existingModel.Description != extension.Description ||
                existingModel.Category != extension.Category ||
                existingModel.IsBuiltIn != extension.IsBuiltIn ||
                existingModel.IsZendExtension != extension.IsZendExtension ||
                (existingModel.DllName ?? string.Empty) != (extension.DllName ?? string.Empty) ||
                string.Join(',', existingModel.SupportedPHPVersions) != string.Join(',', extension.SupportedPHPVersions) ||
                string.Join(',', existingModel.Dependencies) != string.Join(',', extension.Dependencies) ||
                (existingModel.DownloadUrl ?? string.Empty) != (extension.DownloadUrl ?? string.Empty) ||
                (existingModel.ConfigurationTemplate ?? string.Empty) != (extension.ConfigurationTemplate ?? string.Empty) ||
                existingModel.IsPopular != extension.IsPopular;

            if (needsUpdate)
            {
                extension.Id = existingModel.Id;
                await UpdateExtensionAsync(extension);
            }
        }
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Safely parses a DateTime string that may contain microseconds
    /// </summary>
    private static DateTime ParseDateTimeSafe(object dateValue)
    {
        if (dateValue == null) return DateTime.UtcNow;
        
        if (dateValue is DateTime dt)
            return dt;
        
        var dateString = dateValue.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(dateString))
            return DateTime.UtcNow;
        
        // Try standard DateTime parsing first
        if (DateTime.TryParse(dateString, out var parsedDate))
            return parsedDate;
        
        // Try parsing with microseconds format (7 digits after decimal)
        if (DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm:ss.fffffff", null, DateTimeStyles.None, out var dateExact))
            return dateExact;
        
        // Try parsing with milliseconds format (3 digits after decimal)
        if (DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm:ss.fff", null, DateTimeStyles.None, out var dateMillis))
            return dateMillis;
        
        // Try parsing without fractional seconds
        if (DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm:ss", null, DateTimeStyles.None, out var dateSimple))
            return dateSimple;
        
        // If all parsing fails, return UtcNow as fallback
        return DateTime.UtcNow;
    }

    private PHPExtension ConvertToPHPExtension(dynamic data)
    {
        return new PHPExtension
        {
            Id = Convert.ToInt32(data.Id),
            Name = data.Name?.ToString() ?? string.Empty,
            DisplayName = data.DisplayName?.ToString() ?? string.Empty,
            Description = data.Description?.ToString(),
            Category = data.Category?.ToString() ?? string.Empty,
            IsBuiltIn = Convert.ToBoolean(data.IsBuiltIn),
            IsZendExtension = Convert.ToBoolean(data.IsZendExtension),
            DllName = data.DllName?.ToString(),
            SupportedPHPVersions = JsonSerializer.Deserialize<List<string>>(data.SupportedPHPVersions?.ToString() ?? "[]") ?? new List<string>(),
            Dependencies = JsonSerializer.Deserialize<List<string>>(data.Dependencies?.ToString() ?? "[]") ?? new List<string>(),
            DownloadUrl = data.DownloadUrl?.ToString(),
            ConfigurationTemplate = data.ConfigurationTemplate?.ToString(),
            IsPopular = Convert.ToBoolean(data.IsPopular),
            CreatedAt = ParseDateTimeSafe(data.CreatedAt),
            UpdatedAt = ParseDateTimeSafe(data.UpdatedAt)
        };
    }

    private InstalledPHPExtension ConvertToInstalledPHPExtension(dynamic data)
    {
        var installed = new InstalledPHPExtension
        {
            Id = Convert.ToInt32(data.Id),
            ServiceId = Convert.ToInt32(data.ServiceId),
            ExtensionId = Convert.ToInt32(data.ExtensionId),
            PHPVersion = data.PHPVersion?.ToString() ?? string.Empty,
            InstallPath = data.InstallPath?.ToString() ?? string.Empty,
            IsEnabled = Convert.ToBoolean(data.IsEnabled),
            Configuration = data.Configuration?.ToString(),
            InstalledAt = ParseDateTimeSafe(data.InstalledAt),
            Extension = new PHPExtension
            {
                Id = Convert.ToInt32(data.ExtensionId),
                Name = data.Name?.ToString() ?? string.Empty,
                DisplayName = data.DisplayName?.ToString() ?? string.Empty,
                Description = data.Description?.ToString(),
                Category = data.Category?.ToString() ?? string.Empty,
                IsBuiltIn = Convert.ToBoolean(data.IsBuiltIn),
                IsZendExtension = Convert.ToBoolean(data.IsZendExtension),
                DllName = data.DllName?.ToString(),
                IsPopular = Convert.ToBoolean(data.IsPopular)
            }
        };

        return installed;
    }

    private static List<PHPExtension> GetDefaultExtensions()
    {
        return
        [
            // Core PHP Extensions (Built-in)
            new PHPExtension
            {
                Name = "bcmath",
                DisplayName = "BCMath",
                Description = "Arbitrary precision mathematics",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_bcmath.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },
            new PHPExtension
            {
                Name = "calendar",
                DisplayName = "Calendar",
                Description = "Calendar conversion functions",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_calendar.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },
            new PHPExtension
            {
                Name = "iconv",
                DisplayName = "iconv",
                Description = "Character encoding conversion",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_iconv.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },
            new PHPExtension
            {
                Name = "mysqlnd",
                DisplayName = "MySQL Native Driver",
                Description = "MySQL Native Driver",
                Category = PHPExtensionCategories.Database,
                IsBuiltIn = true,
                DllName = "php_mysqlnd.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },
            new PHPExtension
            {
                Name = "phar",
                DisplayName = "Phar",
                Description = "PHP Archive",
                Category = PHPExtensionCategories.FileSystem,
                IsBuiltIn = true,
                DllName = "php_phar.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },
            new PHPExtension
            {
                Name = "random",
                DisplayName = "Random",
                Description = "Cryptographically secure random number generator",
                Category = PHPExtensionCategories.Encryption,
                IsBuiltIn = true,
                DllName = "php_random.dll",
                SupportedPHPVersions = ["8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },
            new PHPExtension
            {
                Name = "readline",
                DisplayName = "Readline",
                Description = "GNU Readline support",
                Category = PHPExtensionCategories.Development,
                IsBuiltIn = true,
                DllName = "php_readline.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },
            new PHPExtension
            {
                Name = "reflection",
                DisplayName = "Reflection",
                Description = "PHP Reflection API",
                Category = PHPExtensionCategories.Development,
                IsBuiltIn = true,
                DllName = "php_reflection.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },
            new PHPExtension
            {
                Name = "simplexml",
                DisplayName = "SimpleXML",
                Description = "Simple XML parser",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_simplexml.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },
            new PHPExtension
            {
                Name = "spl",
                DisplayName = "SPL",
                Description = "Standard PHP Library",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_spl.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },
            new PHPExtension
            {
                Name = "standard",
                DisplayName = "Standard",
                Description = "Standard PHP functions",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_standard.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },
            new PHPExtension
            {
                Name = "xmlreader",
                DisplayName = "XMLReader",
                Description = "XML pull parser",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_xmlreader.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },
            new PHPExtension
            {
                Name = "xmlwriter",
                DisplayName = "XMLWriter",
                Description = "XML writer",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_xmlwriter.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },

            // Database Extensions
            new PHPExtension
            {
                Name = "mysqli",
                DisplayName = "MySQLi",
                Description = "MySQL Improved Extension - enhanced interface for MySQL databases",
                Category = PHPExtensionCategories.Database,
                IsBuiltIn = true,
                DllName = "php_mysqli.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "pdo_mysql",
                DisplayName = "PDO MySQL",
                Description = "PDO driver for MySQL databases",
                Category = PHPExtensionCategories.Database,
                IsBuiltIn = true,
                DllName = "php_pdo_mysql.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "pgsql",
                DisplayName = "PostgreSQL",
                Description = "PostgreSQL database functions",
                Category = PHPExtensionCategories.Database,
                IsBuiltIn = true,
                DllName = "php_pgsql.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"]
            },

            // Web Services
            new PHPExtension
            {
                Name = "curl",
                DisplayName = "cURL",
                Description = "Client URL Library for HTTP requests",
                Category = PHPExtensionCategories.WebServices,
                IsBuiltIn = true,
                DllName = "php_curl.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "openssl",
                DisplayName = "OpenSSL",
                Description = "OpenSSL cryptographic functions",
                Category = PHPExtensionCategories.Encryption,
                IsBuiltIn = true,
                DllName = "php_openssl.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },

            // Image Processing
            new PHPExtension
            {
                Name = "gd",
                DisplayName = "GD",
                Description = "Image creation and manipulation library",
                Category = PHPExtensionCategories.ImageProcessing,
                IsBuiltIn = true,
                DllName = "php_gd.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "imagick",
                DisplayName = "ImageMagick",
                Description = "ImageMagick image manipulation library",
                Category = PHPExtensionCategories.ImageProcessing,
                IsBuiltIn = false,
                DllName = "php_imagick.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3"],
                DownloadUrl = "https://windows.php.net/downloads/pecl/releases/imagick/"
            },

            // Performance
            new PHPExtension
            {
                Name = "opcache",
                DisplayName = "OPcache",
                Description = "Opcode cache for improved performance",
                Category = PHPExtensionCategories.Performance,
                IsBuiltIn = true,
                IsZendExtension = true,
                DllName = "php_opcache.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true,
                ConfigurationTemplate = @"
zend_extension=opcache
opcache.enable=1
opcache.enable_cli=1
opcache.memory_consumption=128
opcache.interned_strings_buffer=8
opcache.max_accelerated_files=4000
opcache.revalidate_freq=2
opcache.fast_shutdown=1"
            },

            // Development
            new PHPExtension
            {
                Name = "xdebug",
                DisplayName = "Xdebug",
                Description = "Debugging and profiling extension",
                Category = PHPExtensionCategories.Development,
                IsBuiltIn = false,
                IsZendExtension = true,
                DllName = "php_xdebug.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                DownloadUrl = "https://xdebug.org/download",
                ConfigurationTemplate = @"
zend_extension=xdebug
xdebug.mode=develop,debug
xdebug.start_with_request=yes
xdebug.client_host=127.0.0.1
xdebug.client_port=9003"
            },

            // File System
            new PHPExtension
            {
                Name = "zip",
                DisplayName = "ZIP",
                Description = "ZIP archive functions",
                Category = PHPExtensionCategories.FileSystem,
                IsBuiltIn = true,
                DllName = "php_zip.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },

            // Compression
            new PHPExtension
            {
                Name = "zlib",
                DisplayName = "Zlib",
                Description = "Compression library functions",
                Category = PHPExtensionCategories.Compression,
                IsBuiltIn = true,
                DllName = "php_zlib.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },

            // Other Popular Extensions
            new PHPExtension
            {
                Name = "mbstring",
                DisplayName = "Multibyte String",
                Description = "Multibyte string handling functions",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_mbstring.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "json",
                DisplayName = "JSON",
                Description = "JavaScript Object Notation functions",
                Category = PHPExtensionCategories.WebServices,
                IsBuiltIn = true,
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "xml",
                DisplayName = "XML",
                Description = "XML manipulation functions",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_xml.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },

            // Additional Core Extensions
            new PHPExtension
            {
                Name = "ctype",
                DisplayName = "Ctype",
                Description = "Character type checking functions",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_ctype.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "dom",
                DisplayName = "DOM",
                Description = "Document Object Model manipulation functions",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_dom.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "fileinfo",
                DisplayName = "Fileinfo",
                Description = "File information functions",
                Category = PHPExtensionCategories.FileSystem,
                IsBuiltIn = true,
                DllName = "php_fileinfo.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "filter",
                DisplayName = "Filter",
                Description = "Data filtering functions",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_filter.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "hash",
                DisplayName = "Hash",
                Description = "Hashing functions (MD5, SHA1, etc.)",
                Category = PHPExtensionCategories.Encryption,
                IsBuiltIn = true,
                DllName = "php_hash.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "pcre",
                DisplayName = "PCRE",
                Description = "Perl Compatible Regular Expressions",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "pdo",
                DisplayName = "PDO",
                Description = "PHP Data Objects - database abstraction layer",
                Category = PHPExtensionCategories.Database,
                IsBuiltIn = true,
                DllName = "php_pdo.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "session",
                DisplayName = "Session",
                Description = "Session handling functions",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },
            new PHPExtension
            {
                Name = "tokenizer",
                DisplayName = "Tokenizer",
                Description = "PHP tokenizer functions",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = true,
                DllName = "php_tokenizer.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                IsPopular = true
            },

            // External/Optional Extensions
            new PHPExtension
            {
                Name = "memcached",
                DisplayName = "Memcached",
                Description = "High-performance, distributed memory object caching system",
                Category = PHPExtensionCategories.Performance,
                IsBuiltIn = false,
                DllName = "php_memcached.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3"],
                DownloadUrl = "https://windows.php.net/downloads/pecl/releases/memcached/",
                IsPopular = true,
                ConfigurationTemplate = @"
; Memcached Configuration
memcached.sess_locking = 1
memcached.sess_consistent_hash = 1
memcached.sess_binary = 0
memcached.sess_lock_wait = 150000
memcached.sess_prefix = memc.sess.key."
            },
            new PHPExtension
            {
                Name = "ioncube_loader",
                DisplayName = "ionCube Loader",
                Description = "Loader for ionCube encoded PHP files",
                Category = PHPExtensionCategories.Other,
                IsBuiltIn = false,
                IsZendExtension = true,
                DllName = "ioncube_loader_win_8.3.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3"],
                DownloadUrl = "https://www.ioncube.com/loaders.php",
                ConfigurationTemplate = @"
; ionCube Loader
zend_extension=ioncube_loader_win_8.3.dll"
            },
            new PHPExtension
            {
                Name = "redis",
                DisplayName = "Redis",
                Description = "PHP extension for interfacing with Redis key-value store",
                Category = PHPExtensionCategories.Database,
                IsBuiltIn = false,
                DllName = "php_redis.dll",
                SupportedPHPVersions = ["7.4", "8.0", "8.1", "8.2", "8.3", "8.4", "8.5"],
                DownloadUrl = "https://windows.php.net/downloads/pecl/releases/redis/",
                IsPopular = true,
                ConfigurationTemplate = @"
; Redis Configuration
redis.arrays.autorehash = 1
redis.clusters.cache_slots = 1
redis.clusters.timeout = 5
redis.pconnect.pooling_enabled = 1
redis.pconnect.connection_limit = 0"
            }
        ];
    }

    #endregion
}
