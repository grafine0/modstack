using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ModStack.Common.Models;
using ModStack.Common.Enums;
using ModStack.Data.Interfaces;
using ModStack.Common.Interfaces;
using Microsoft.Data.Sqlite;

namespace ModStack.Data.Repositories
{
    /// <summary>
    /// Repository implementation for package management operations
    /// </summary>
    public class PackageRepository : IPackageRepository
    {
        private readonly string _connectionString;

        public PackageRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<AvailablePackage>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT p.*, 
                       pc.Port, pc.InstallPath, pc.AutoStart, pc.RegisterAsService, pc.Configuration as ConfigJson
                FROM Packages p
                LEFT JOIN PackageConfigurations pc ON p.Id = pc.PackageId
                ORDER BY p.IsPopular DESC, p.DisplayName";

            using var connection = new SqliteConnection(_connectionString);
            var packages = await connection.QueryAsync<dynamic>(new CommandDefinition(sql, cancellationToken: cancellationToken));
            
            var result = new List<AvailablePackage>();
            var packageGroups = packages.GroupBy(p => p.Id);

            foreach (var group in packageGroups)
            {
                var packageData = group.First();
                var package = MapToAvailablePackage(packageData);
                
                // Get versions
                package.AvailableVersions = await GetVersionsAsync(package.Id, cancellationToken);
                
                result.Add(package);
            }

            return result;
        }

        public async Task<AvailablePackage?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT p.*, 
                       pc.Port, pc.InstallPath, pc.AutoStart, pc.RegisterAsService, pc.Configuration as ConfigJson
                FROM Packages p
                LEFT JOIN PackageConfigurations pc ON p.Id = pc.PackageId
                WHERE p.Id = @Id";

            using var connection = new SqliteConnection(_connectionString);
            var packageData = await connection.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
            if (packageData == null) return null;

            var package = MapToAvailablePackage(packageData);
            package.AvailableVersions = await GetVersionsAsync(id, cancellationToken);
            
            return package;
        }

        public async Task<AvailablePackage?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT p.*, 
                       pc.Port, pc.InstallPath, pc.AutoStart, pc.RegisterAsService, pc.Configuration as ConfigJson
                FROM Packages p
                LEFT JOIN PackageConfigurations pc ON p.Id = pc.PackageId
                WHERE p.Name = @Name";

            using var connection = new SqliteConnection(_connectionString);
            var packageData = await connection.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(sql, new { Name = name }, cancellationToken: cancellationToken));
            if (packageData == null) return null;

            var package = MapToAvailablePackage(packageData);
            package.AvailableVersions = await GetVersionsAsync(package.Id, cancellationToken);
            
            return package;
        }

        public async Task<List<AvailablePackage>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT p.*, 
                       pc.Port, pc.InstallPath, pc.AutoStart, pc.RegisterAsService, pc.Configuration as ConfigJson
                FROM Packages p
                LEFT JOIN PackageConfigurations pc ON p.Id = pc.PackageId
                WHERE p.Category = @Category
                ORDER BY p.IsPopular DESC, p.DisplayName";

            using var connection = new SqliteConnection(_connectionString);
            var packages = await connection.QueryAsync<dynamic>(new CommandDefinition(sql, new { Category = category }, cancellationToken: cancellationToken));
            
            var result = new List<AvailablePackage>();
            var packageGroups = packages.GroupBy(p => p.Id);

            foreach (var group in packageGroups)
            {
                var packageData = group.First();
                var package = MapToAvailablePackage(packageData);
                package.AvailableVersions = await GetVersionsAsync(package.Id, cancellationToken);
                result.Add(package);
            }

            return result;
        }

        public async Task<List<AvailablePackage>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT p.*, 
                       pc.Port, pc.InstallPath, pc.AutoStart, pc.RegisterAsService, pc.Configuration as ConfigJson
                FROM Packages p
                LEFT JOIN PackageConfigurations pc ON p.Id = pc.PackageId
                WHERE p.Name LIKE @SearchTerm 
                   OR p.DisplayName LIKE @SearchTerm 
                   OR p.Description LIKE @SearchTerm 
                   OR p.Tags LIKE @SearchTerm
                ORDER BY p.IsPopular DESC, p.DisplayName";

            var searchPattern = $"%{searchTerm}%";
            using var connection = new SqliteConnection(_connectionString);
            var packages = await connection.QueryAsync<dynamic>(new CommandDefinition(sql, new { SearchTerm = searchPattern }, cancellationToken: cancellationToken));
            
            var result = new List<AvailablePackage>();
            var packageGroups = packages.GroupBy(p => p.Id);

            foreach (var group in packageGroups)
            {
                var packageData = group.First();
                var package = MapToAvailablePackage(packageData);
                package.AvailableVersions = await GetVersionsAsync(package.Id, cancellationToken);
                result.Add(package);
            }

            return result;
        }

        public async Task<List<AvailablePackage>> GetPopularAsync(CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT p.*, 
                       pc.Port, pc.InstallPath, pc.AutoStart, pc.RegisterAsService, pc.Configuration as ConfigJson
                FROM Packages p
                LEFT JOIN PackageConfigurations pc ON p.Id = pc.PackageId
                WHERE p.IsPopular = 1
                ORDER BY p.DisplayName";

            using var connection = new SqliteConnection(_connectionString);
            var packages = await connection.QueryAsync<dynamic>(new CommandDefinition(sql, cancellationToken: cancellationToken));
            
            var result = new List<AvailablePackage>();
            var packageGroups = packages.GroupBy(p => p.Id);

            foreach (var group in packageGroups)
            {
                var packageData = group.First();
                var package = MapToAvailablePackage(packageData);
                package.AvailableVersions = await GetVersionsAsync(package.Id, cancellationToken);
                result.Add(package);
            }

            return result;
        }

        public async Task<List<AvailablePackage>> GetUserAddedAsync(CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT p.*, 
                       pc.Port, pc.InstallPath, pc.AutoStart, pc.RegisterAsService, pc.Configuration as ConfigJson
                FROM Packages p
                LEFT JOIN PackageConfigurations pc ON p.Id = pc.PackageId
                WHERE p.IsUserAdded = 1
                ORDER BY p.DisplayName";

            using var connection = new SqliteConnection(_connectionString);
            var packages = await connection.QueryAsync<dynamic>(sql);
            
            var result = new List<AvailablePackage>();
            var packageGroups = packages.GroupBy(p => p.Id);

            foreach (var group in packageGroups)
            {
                var packageData = group.First();
                var package = MapToAvailablePackage(packageData);
                package.AvailableVersions = await GetVersionsAsync(package.Id);
                result.Add(package);
            }

            return result;
        }

        public async Task<AvailablePackage> AddAsync(AvailablePackage package, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                INSERT INTO Packages (Name, DisplayName, Description, Category, Icon, IsPopular, 
                                    DownloadSize, Tags, Homepage, Documentation, License, 
                                    LastUpdated, IsUserAdded, PackageType, IsSystemPackage)
                VALUES (@Name, @DisplayName, @Description, @Category, @Icon, @IsPopular,
                        @DownloadSize, @Tags, @Homepage, @Documentation, @License,
                        @LastUpdated, @IsUserAdded, @PackageType, @IsSystemPackage);
                SELECT last_insert_rowid();";

            using var connection = new SqliteConnection(_connectionString);
            var packageId = await connection.QuerySingleAsync<long>(sql, new
            {
                package.Name,
                package.DisplayName,
                package.Description,
                package.Category,
                package.Icon,
                package.IsPopular,
                package.DownloadSize,
                Tags = JsonSerializer.Serialize(package.Tags),
                package.Homepage,
                package.Documentation,
                package.License,
                package.LastUpdated,
                IsUserAdded = true, // All manually added packages are user-added
                PackageType = (int)package.PackageType,
                package.IsSystemPackage
            });

            package.Id = packageId;

            // Add default configuration if provided
            if (package.DefaultConfiguration != null)
            {
                await SetConfigurationAsync(packageId, package.DefaultConfiguration);
            }

            // Add versions if provided
            if (package.AvailableVersions?.Any() == true)
            {
                foreach (var version in package.AvailableVersions)
                {
                    await AddVersionAsync(packageId, version);
                }
            }

            return package;
        }

        public async Task<AvailablePackage> UpdateAsync(AvailablePackage package, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();
            
            try
            {
                // Update main package fields
                const string updatePackageSql = @"
                    UPDATE Packages 
                    SET DisplayName = @DisplayName,
                        Description = @Description,
                        Category = @Category,
                        Icon = @Icon,
                        IsPopular = @IsPopular,
                        DownloadSize = @DownloadSize,
                        Tags = @Tags,
                        Homepage = @Homepage,
                        Documentation = @Documentation,
                        License = @License,
                        LastUpdated = @LastUpdated,
                        PackageType = @PackageType,
                        UpdatedAt = CURRENT_TIMESTAMP
                    WHERE Id = @Id";

                await connection.ExecuteAsync(updatePackageSql, new
                {
                    package.Id,
                    package.DisplayName,
                    package.Description,
                    package.Category,
                    package.Icon,
                    package.IsPopular,
                    package.DownloadSize,
                    Tags = JsonSerializer.Serialize(package.Tags),
                    package.Homepage,
                    package.Documentation,
                    package.License,
                    package.LastUpdated,
                    PackageType = (int)package.PackageType
                }, transaction);

                // Update versions: Delete existing versions and add new ones
                if (package.AvailableVersions != null)
                {
                    // Delete existing versions for this package
                    const string deleteVersionsSql = "DELETE FROM PackageVersions WHERE PackageId = @PackageId";
                    await connection.ExecuteAsync(deleteVersionsSql, new { PackageId = package.Id }, transaction);

                    // Add new versions
                    const string insertVersionSql = @"
                        INSERT INTO PackageVersions (PackageId, Version, DisplayName, ServiceName, ReleaseDate, 
                                                   IsStable, IsRecommended, IsLTS, Size, DownloadUrls, IsSystemVersion)
                        VALUES (@PackageId, @Version, @DisplayName, @ServiceName, @ReleaseDate,
                                @IsStable, @IsRecommended, @IsLTS, @Size, @DownloadUrls, @IsSystemVersion)";

                    foreach (var version in package.AvailableVersions)
                    {
                        await connection.ExecuteAsync(insertVersionSql, new
                        {
                            PackageId = package.Id,
                            version.Version,
                            version.DisplayName,
                            version.ServiceName,
                            version.ReleaseDate,
                            version.IsStable,
                            version.IsRecommended,
                            version.IsLTS,
                            version.Size,
                            DownloadUrls = JsonSerializer.Serialize(version.DownloadUrls ?? new List<string>()),
                            version.IsSystemVersion
                        }, transaction);
                    }
                }

                await transaction.CommitAsync();
                return package;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            const string sql = "DELETE FROM Packages WHERE Id = @Id";
            using var connection = new SqliteConnection(_connectionString);
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT COUNT(1) FROM Packages WHERE Name = @Name";
            using var connection = new SqliteConnection(_connectionString);
            var count = await connection.QuerySingleAsync<long>(new CommandDefinition(sql, new { Name = name }, cancellationToken: cancellationToken));
            return count > 0;
        }

        public async Task<List<PackageVersion>> GetVersionsAsync(long packageId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT Id, PackageId, Version, DisplayName, ServiceName, ReleaseDate, IsStable, 
                       IsRecommended, IsLTS, Size, DownloadUrls, IsSystemVersion
                FROM PackageVersions 
                WHERE PackageId = @PackageId
                ORDER BY IsRecommended DESC, ReleaseDate DESC";

            using var connection = new SqliteConnection(_connectionString);
            var versions = await connection.QueryAsync<dynamic>(new CommandDefinition(sql, new { PackageId = packageId }, cancellationToken: cancellationToken));
            
            return versions.Select(v => new PackageVersion
            {
                Id = v.Id,
                PackageId = v.PackageId,
                Version = v.Version,
                DisplayName = v.DisplayName,
                ServiceName = v.ServiceName ?? string.Empty,
                ReleaseDate = ParseDateTimeSafe(v.ReleaseDate),
                IsStable = Convert.ToBoolean(v.IsStable),
                IsRecommended = Convert.ToBoolean(v.IsRecommended),
                IsLTS = Convert.ToBoolean(v.IsLTS),
                Size = v.Size,
                DownloadUrls = JsonSerializer.Deserialize<List<string>>(v.DownloadUrls ?? "[]"),
                IsSystemVersion = v.IsSystemVersion != null ? Convert.ToBoolean(v.IsSystemVersion) : false
            }).ToList();
        }

        public async Task<PackageVersion> AddVersionAsync(long packageId, PackageVersion version, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                INSERT INTO PackageVersions (PackageId, Version, DisplayName, ServiceName, ReleaseDate, 
                                           IsStable, IsRecommended, IsLTS, Size, DownloadUrls, IsSystemVersion)
                VALUES (@PackageId, @Version, @DisplayName, @ServiceName, @ReleaseDate,
                        @IsStable, @IsRecommended, @IsLTS, @Size, @DownloadUrls, @IsSystemVersion);
                SELECT last_insert_rowid();";

            using var connection = new SqliteConnection(_connectionString);
            var versionId = await connection.QuerySingleAsync<long>(sql, new
            {
                PackageId = packageId,
                version.Version,
                version.DisplayName,
                version.ServiceName,
                version.ReleaseDate,
                version.IsStable,
                version.IsRecommended,
                version.IsLTS,
                version.Size,
                DownloadUrls = JsonSerializer.Serialize(version.DownloadUrls ?? new List<string>()),
                version.IsSystemVersion
            });

            version.Id = versionId;
            version.PackageId = packageId;
            return version;
        }

        public async Task<PackageVersion> UpdateVersionAsync(PackageVersion version, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                UPDATE PackageVersions 
                SET Version = @Version,
                    DisplayName = @DisplayName,
                    ServiceName = @ServiceName,
                    ReleaseDate = @ReleaseDate,
                    IsStable = @IsStable,
                    IsRecommended = @IsRecommended,
                    IsLTS = @IsLTS,
                    Size = @Size,
                    DownloadUrls = @DownloadUrls
                WHERE Id = @Id";

            using var connection = new SqliteConnection(_connectionString);
            await connection.ExecuteAsync(sql, new
            {
                version.Id,
                version.Version,
                version.DisplayName,
                version.ServiceName,
                version.ReleaseDate,
                version.IsStable,
                version.IsRecommended,
                version.IsLTS,
                version.Size,
                DownloadUrls = JsonSerializer.Serialize(version.DownloadUrls ?? new List<string>())
            });

            return version;
        }

        public async Task<bool> DeleteVersionAsync(long versionId, CancellationToken cancellationToken = default)
        {
            const string sql = "DELETE FROM PackageVersions WHERE Id = @Id";
            using var connection = new SqliteConnection(_connectionString);
            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = versionId });
            return rowsAffected > 0;
        }


        public async Task<PackageConfiguration?> GetConfigurationAsync(long packageId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT Port, InstallPath, AutoStart, RegisterAsService, Configuration
                FROM PackageConfigurations 
                WHERE PackageId = @PackageId";

            using var connection = new SqliteConnection(_connectionString);
            var configData = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { PackageId = packageId });
            if (configData == null) return null;

            var config = new PackageConfiguration
            {
                Port = configData.Port != null ? (int)configData.Port : 0,
                InstallPath = configData.InstallPath,
                AutoStart = Convert.ToBoolean(configData.AutoStart),
                RegisterAsService = Convert.ToBoolean(configData.RegisterAsService)
            };

            // Parse configuration options if present
            if (!string.IsNullOrEmpty(configData.Configuration))
            {
                try
                {
                    var configOptions = JsonSerializer.Deserialize<List<ConfigOption>>(configData.Configuration);
                    config.AvailableOptions = configOptions ?? new List<ConfigOption>();
                }
                catch
                {
                    config.AvailableOptions = new List<ConfigOption>();
                }
            }

            return config;
        }

        public async Task<PackageConfiguration> SetConfigurationAsync(long packageId, PackageConfiguration configuration, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                INSERT OR REPLACE INTO PackageConfigurations (PackageId, Port, InstallPath, AutoStart, 
                                                             RegisterAsService, Configuration)
                VALUES (@PackageId, @Port, @InstallPath, @AutoStart, @RegisterAsService, @Configuration)";

            using var connection = new SqliteConnection(_connectionString);
            await connection.ExecuteAsync(sql, new
            {
                PackageId = packageId,
                configuration.Port,
                configuration.InstallPath,
                configuration.AutoStart,
                configuration.RegisterAsService,
                Configuration = JsonSerializer.Serialize(configuration.AvailableOptions ?? new List<ConfigOption>())
            });

            return configuration;
        }

        public async Task InitializeDefaultPackagesAsync(IPathConfigurationService pathConfigurationService, CancellationToken cancellationToken = default)
        {
            // Check if packages already exist
            const string checkSql = "SELECT COUNT(1) FROM Packages";
            using var connection = new SqliteConnection(_connectionString);
            var count = await connection.QuerySingleAsync<long>(checkSql);
            
            if (count > 0) return; // Already initialized

            // Insert default packages (the ones from PackageManagerService)
            var defaultPackages = GetDefaultPackages(pathConfigurationService);
            
            foreach (var package in defaultPackages)
            {
                await AddAsync(package);
            }
        }

        /// <summary>
        /// Safely parses a DateTime string that may contain microseconds
        /// </summary>
        private DateTime ParseDateTimeSafe(object dateValue)
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

        private AvailablePackage MapToAvailablePackage(dynamic data)
        {
            var package = new AvailablePackage
            {
                Id = data.Id,
                Name = data.Name,
                DisplayName = data.DisplayName,
                Description = data.Description,
                Category = data.Category,
                Icon = data.Icon,
                IsPopular = Convert.ToBoolean(data.IsPopular),
                DownloadSize = data.DownloadSize,
                Homepage = data.Homepage,
                Documentation = data.Documentation,
                License = data.License,
                LastUpdated = ParseDateTimeSafe(data.LastUpdated),
                PackageType = (PackageType)(data.PackageType ?? 1), // Default to Service if null
                IsInstalled = false, // This would need to be determined separately
                InstalledVersion = null,
                IsSystemPackage = data.IsSystemPackage != null ? Convert.ToBoolean(data.IsSystemPackage) : false
            };

            // Parse tags
            if (!string.IsNullOrEmpty(data.Tags))
            {
                try
                {
                    package.Tags = JsonSerializer.Deserialize<List<string>>(data.Tags) ?? new List<string>();
                }
                catch
                {
                    package.Tags = new List<string>();
                }
            }

            // Set default configuration if present
            if (data.Port != null || data.InstallPath != null)
            {
                package.DefaultConfiguration = new PackageConfiguration
                {
                    Port = data.Port != null ? (int)data.Port : 0,
                    InstallPath = data.InstallPath,
                    AutoStart = data.AutoStart != null ? Convert.ToBoolean(data.AutoStart) : false,
                    RegisterAsService = data.RegisterAsService != null ? Convert.ToBoolean(data.RegisterAsService) : false,
                    AvailableOptions = new List<ConfigOption>()
                };

                // Parse configuration options if present
                if (!string.IsNullOrEmpty(data.ConfigJson))
                {
                    try
                    {
                        package.DefaultConfiguration.AvailableOptions = 
                            JsonSerializer.Deserialize<List<ConfigOption>>(data.ConfigJson) ?? new List<ConfigOption>();
                    }
                    catch
                    {
                        package.DefaultConfiguration.AvailableOptions = new List<ConfigOption>();
                    }
                }
            }

            return package;
        }

        private List<AvailablePackage> GetDefaultPackages(IPathConfigurationService pathConfigurationService)
        {
            // Get the user's configured services directory
            var servicesDirectory = pathConfigurationService.GetServicesDirectory();
            
            // This contains the same default packages from PackageManagerService
            // but structured for database insertion
            return new List<AvailablePackage>
            {
                new AvailablePackage
                {
                    Name = "apache",
                    DisplayName = "Apache HTTP Server",
                    Description = "The most popular web server software, serving over 40% of websites worldwide.",
                    Category = "Web Server",
                    Icon = "🌐",
                    IsPopular = true,
                    DownloadSize = 15 * 1024 * 1024,
                    Tags = new List<string> { "web-server", "http", "popular" },
                    Homepage = "https://httpd.apache.org/",
                    License = "Apache License 2.0",
                    LastUpdated = DateTime.Now.AddDays(-10),
                    AvailableVersions = new List<PackageVersion>
                    {
                        new PackageVersion
                        {
                            Version = "2.4.65",
                            DisplayName = "2.4.65 (Latest - Recommended)",
                            ReleaseDate = DateTime.Now.AddDays(-2),
                            IsStable = true,
                            IsRecommended = true,
                            Size = 15 * 1024 * 1024,
                            DownloadUrls = new List<string>
                            {
                                "https://www.apachelounge.com/download/VS17/binaries/httpd-2.4.65-250724-Win64-VS17.zip"
                            }
                        }
                    },
                    DefaultConfiguration = new PackageConfiguration
                    {
                        Port = 80,
                        InstallPath = Path.Combine(servicesDirectory, "Apache"),
                        AutoStart = false,
                        RegisterAsService = true,
                        AvailableOptions = new List<ConfigOption>
                        {
                            new ConfigOption { Key = "port", DisplayName = "HTTP Port", Type = "number", DefaultValue = 80, IsRequired = true },
                            new ConfigOption { Key = "ssl_port", DisplayName = "HTTPS Port", Type = "number", DefaultValue = 443 },
                            new ConfigOption { Key = "server_name", DisplayName = "Server Name", Type = "text", DefaultValue = "localhost" },
                            new ConfigOption { Key = "enable_ssl", DisplayName = "Enable SSL", Type = "boolean", DefaultValue = true }
                        }
                    }
                },
                new AvailablePackage
                {
                    Name = "nginx",
                    DisplayName = "Nginx",
                    Description = "High-performance HTTP server and reverse proxy, also a mail proxy server.",
                    Category = "Web Server",
                    Icon = "⚡",
                    IsPopular = true,
                    DownloadSize = 3 * 1024 * 1024,
                    Tags = new List<string> { "web-server", "reverse-proxy", "high-performance" },
                    Homepage = "https://nginx.org/",
                    License = "2-clause BSD",
                    LastUpdated = DateTime.Now.AddDays(-5),
                    AvailableVersions = new List<PackageVersion>
                    {
                        new PackageVersion
                        {
                            Version = "1.24.0",
                            DisplayName = "1.24.0 (Stable)",
                            ReleaseDate = DateTime.Now.AddDays(-5),
                            IsStable = true,
                            IsRecommended = true,
                            Size = 3 * 1024 * 1024
                        }
                    },
                    DefaultConfiguration = new PackageConfiguration
                    {
                        Port = 8080,
                        InstallPath = Path.Combine(servicesDirectory, "Nginx"),
                        AutoStart = false,
                        RegisterAsService = true,
                        AvailableOptions = new List<ConfigOption>
                        {
                            new ConfigOption { Key = "port", DisplayName = "HTTP Port", Type = "number", DefaultValue = 8080, IsRequired = true },
                            new ConfigOption { Key = "worker_processes", DisplayName = "Worker Processes", Type = "select", DefaultValue = "auto", Options = new List<string> { "auto", "1", "2", "4", "8" } }
                        }
                    }
                },
                new AvailablePackage
                {
                    Name = "mysql",
                    DisplayName = "MySQL",
                    Description = "The world's most popular open source database management system.",
                    Category = "Database",
                    Icon = "🗄️",
                    IsPopular = true,
                    DownloadSize = 180 * 1024 * 1024,
                    Tags = new List<string> { "database", "sql", "mysql" },
                    Homepage = "https://www.mysql.com/",
                    License = "GPL v2",
                    LastUpdated = DateTime.Now.AddDays(-20),
                    AvailableVersions = new List<PackageVersion>
                    {
                        new PackageVersion
                        {
                            Version = "8.0.35",
                            DisplayName = "8.0.35 (Latest LTS)",
                            ReleaseDate = DateTime.Now.AddDays(-20),
                            IsStable = true,
                            IsLTS = true,
                            IsRecommended = true,
                            Size = 180 * 1024 * 1024,
                            DownloadUrls = new List<string>
                            {
                                "https://dev.mysql.com/get/Downloads/MySQL-8.0/mysql-8.0.35-winx64.zip"
                            }
                        }
                    },
                    DefaultConfiguration = new PackageConfiguration
                    {
                        Port = 3306,
                        InstallPath = Path.Combine(servicesDirectory, "MySQL"),
                        AutoStart = false,
                        RegisterAsService = true,
                        AvailableOptions = new List<ConfigOption>
                        {
                            new ConfigOption { Key = "port", DisplayName = "MySQL Port", Type = "number", DefaultValue = 3306, IsRequired = true },
                            new ConfigOption { Key = "root_password", DisplayName = "Root Password", Type = "text", DefaultValue = "root", IsRequired = true },
                            new ConfigOption { Key = "innodb_buffer_pool_size", DisplayName = "InnoDB Buffer Pool Size", Type = "text", DefaultValue = "128M" }
                        }
                    }
                },
                new AvailablePackage
                {
                    Name = "php",
                    DisplayName = "PHP",
                    Description = "Popular general-purpose scripting language for web development.",
                    Category = "Runtime",
                    Icon = "🐘",
                    IsPopular = true,
                    DownloadSize = 25 * 1024 * 1024,
                    Tags = new List<string> { "scripting", "web", "runtime", "php", "development" },
                    Homepage = "https://www.php.net/",
                    License = "PHP License",
                    LastUpdated = DateTime.Now.AddDays(-7),
                    AvailableVersions = new List<PackageVersion>
                    {
                        new PackageVersion
                        {
                            Version = "8.3.0",
                            DisplayName = "8.3.0 (Latest - Recommended)",
                            ReleaseDate = DateTime.Now.AddDays(-7),
                            IsStable = true,
                            IsRecommended = true,
                            Size = 25 * 1024 * 1024,
                            DownloadUrls = new List<string>
                            {
                                "https://windows.php.net/downloads/releases/archives/php-8.3.0-Win32-vs16-x64.zip"
                            }
                        },
                        new PackageVersion
                        {
                            Version = "8.5.0",
                            DisplayName = "PHP 8.5.0",
                            ReleaseDate = new DateTime(2025, 11, 20),
                            IsStable = true,
                            IsRecommended = false,
                            Size = 32 * 1024 * 1024,
                            DownloadUrls = new List<string>
                            {
                                "https://windows.php.net/downloads/releases/archives/php-8.5.0-Win32-vs17-x64.zip",
                                "https://windows.php.net/downloads/releases/php-8.5.12-1-Win32-vs17-x64.zip"
                            }
                        }
                    },
                    DefaultConfiguration = new PackageConfiguration
                    {
                        Port = 9000,
                        InstallPath = Path.Combine(servicesDirectory, "PHP"),
                        AutoStart = false,
                        RegisterAsService = false,
                        AvailableOptions = new List<ConfigOption>
                        {
                            new ConfigOption { Key = "memory_limit", DisplayName = "Memory Limit", Type = "text", DefaultValue = "256M" },
                            new ConfigOption { Key = "max_execution_time", DisplayName = "Max Execution Time", Type = "number", DefaultValue = 30 },
                            new ConfigOption { Key = "upload_max_filesize", DisplayName = "Upload Max Filesize", Type = "text", DefaultValue = "2M" },
                            new ConfigOption { Key = "post_max_size", DisplayName = "Post Max Size", Type = "text", DefaultValue = "8M" },
                            
                            // Core Extensions
                            new ConfigOption { Key = "enable_fileinfo", DisplayName = "Enable Fileinfo", Type = "boolean", DefaultValue = true, Description = "File information functions" },
                            new ConfigOption { Key = "enable_mbstring", DisplayName = "Enable MBString", Type = "boolean", DefaultValue = true, Description = "Multibyte string functions" },
                            new ConfigOption { Key = "enable_openssl", DisplayName = "Enable OpenSSL", Type = "boolean", DefaultValue = true, Description = "OpenSSL cryptographic functions" },
                            new ConfigOption { Key = "enable_curl", DisplayName = "Enable cURL", Type = "boolean", DefaultValue = true, Description = "Client URL library" },
                            new ConfigOption { Key = "enable_zip", DisplayName = "Enable ZIP", Type = "boolean", DefaultValue = true, Description = "ZIP archive handling" },
                            new ConfigOption { Key = "enable_xml", DisplayName = "Enable XML", Type = "boolean", DefaultValue = true, Description = "XML parsing functions" },
                            new ConfigOption { Key = "enable_json", DisplayName = "Enable JSON", Type = "boolean", DefaultValue = true, Description = "JSON encoding/decoding" },
                            
                            // Database Extensions
                            new ConfigOption { Key = "enable_pdo_mysql", DisplayName = "Enable PDO MySQL", Type = "boolean", DefaultValue = true, Description = "PDO MySQL driver" },
                            new ConfigOption { Key = "enable_mysqli", DisplayName = "Enable MySQLi", Type = "boolean", DefaultValue = true, Description = "MySQL improved extension" },
                            
                            // Graphics Extensions
                            new ConfigOption { Key = "enable_gd", DisplayName = "Enable GD", Type = "boolean", DefaultValue = true, Description = "Image processing library" },
                            
                            // Performance Extensions
                            new ConfigOption { Key = "enable_opcache", DisplayName = "Enable OPcache", Type = "boolean", DefaultValue = true, Description = "Opcode cache for improved performance" },
                            
                            // Development Extensions
                            new ConfigOption { Key = "enable_xdebug", DisplayName = "Enable Xdebug", Type = "boolean", DefaultValue = false, Description = "Debugging and profiling extension" },
                            
                            // External Extensions
                            new ConfigOption { Key = "enable_ioncube", DisplayName = "Enable ionCube Loader", Type = "boolean", DefaultValue = false, Description = "Loader for ionCube encoded PHP files" }
                        }
                    }
                }
            };
        }
    }
}
