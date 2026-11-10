using Microsoft.Data.Sqlite;
using ModStack.Data;
using ModStack.Data.Interfaces;
using ModStack.Data.Repositories;
using ModStack.Data.Migrations;
using ModStack.GUI.Services.SeedData;
using ModStack.Common.Models;
using ModStack.Logging;
using System.IO;
using System.Text.Json;
using System.Linq;
using Dapper;

namespace ModStack.GUI.Services;

/// <summary>
/// Result of database initialization operation
/// </summary>
public record DatabaseInitResult(
    bool Success,
    int MigratedProjects,
    int SeededPackages,
    int SkippedCount,
    string? ErrorMessage = null
);

/// <summary>
/// Result of package seeding operation
/// </summary>
public record PackageSeedResult(int Created, int Skipped);

/// <summary>
/// Result of sample data seeding operation
/// </summary>
public record SampleDataSeedResult(int SeededPackages, int SkippedCount);

/// <summary>
/// Service responsible for initializing the database and migrating data
/// 
/// Logging Emoji Convention:
/// - Info Level: ✅ (success), ⚠️ (warning), ❌ (error) - High-level lifecycle events
/// - Debug Level: 🔧 (technical steps), 📦 (packages), 🌱 (seeding) - Technical operations
/// - Trace Level: 🔍 (diagnostics), 📊 (data) - Detailed diagnostics
/// </summary>
public class DatabaseInitializationService
{
    private const string PackagesSeedVersion = "1.1.0";
    private const string StacksSeedVersion = "1.0.0";
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    private readonly string _connectionString;
    private readonly Func<string> _getDatabasePath;

    public DatabaseInitializationService(Func<string>? getDatabasePath = null)
    {
        _getDatabasePath = getDatabasePath ?? GetDatabaseConnectionString;
        _connectionString = _getDatabasePath();
    }

    public async Task<DatabaseInitResult> InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var isInitialized = await IsDatabaseInitializedAsync(cancellationToken).ConfigureAwait(false);
            int seededPackages = 0;
            int skippedPackages = 0;
            int migratedProjects = 0;

            if (!isInitialized)
            {
                LogService.Info("🔄 DatabaseInitializationService: First-time setup");
                LogService.Info("🔄 DatabaseInitializationService: Starting first-time database setup...");

            // Initialize database schema
                LogService.Info("🔧 DatabaseInitializationService: Creating database schema...");
            var initializer = new DatabaseInitializer(_connectionString);
            await initializer.InitializeAsync().ConfigureAwait(false);
                LogService.Info("✅ DatabaseInitializationService: Database schema created successfully");

            // Seed sample data if database is empty
                LogService.Info("🌱 DatabaseInitializationService: Starting sample data seeding...");
                var seedingResult = await SeedSampleDataIfEmptyAsync(cancellationToken).ConfigureAwait(false);
                seededPackages = seedingResult.SeededPackages;
                skippedPackages = seedingResult.SkippedCount;

            // Migrate existing file-based data
                LogService.Info("📦 DatabaseInitializationService: Starting file-based data migration...");
                migratedProjects = await MigrateFileBasedDataAsync(cancellationToken).ConfigureAwait(false);
                LogService.Info("✅ DatabaseInitializationService: Data migration completed");
            }
            else
            {
                LogService.Info("✅ DatabaseInitializationService: Database already initialized, skipping seeding");
            }

            // Always run migrations (safe to run repeatedly)
            LogService.Info("🔄 DatabaseInitializationService: Running database migrations...");
            await RunMigrationsAsync(cancellationToken).ConfigureAwait(false);
            
            // Clear seed version cache after migrations in case SeedVersions table was modified
            ClearSeedVersionCache();

            // Always log diagnostics
            LogService.Info("🔍 DatabaseInitializationService: Running final database diagnostics...");
            await LogCurrentPackagesAsync(cancellationToken).ConfigureAwait(false);

            return new DatabaseInitResult(true, migratedProjects, seededPackages, skippedPackages);
        }
        catch (Exception ex)
        {
            LogService.LogException("CRITICAL: Database initialization failed", ex);
            return new DatabaseInitResult(false, 0, 0, 0, ex.Message);
        }
    }

    private async Task<int> MigrateFileBasedDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var projectsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "ModStack", "projects.txt");

            if (!File.Exists(projectsFile))
            {
                LogService.Info("No existing projects file found, skipping migration");
                return 0;
            }

            LogService.Info("Migrating file-based projects to database...");

            // Create repositories
            var projectRepo = new ProjectRepository(_connectionString);
            var stackRepo = new StackRepository(_connectionString);

            // Read existing projects
            var lines = await File.ReadAllLinesAsync(projectsFile, cancellationToken).ConfigureAwait(false);
            int migratedCount = 0;

            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    try
                    {
                        DateTime? parsedDate = null;
                        if (parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]))
                        {
                            // Try multiple DateTime parsing formats to handle various formats including microseconds
                            var dateString = parts[3].Trim();
                            if (DateTime.TryParse(dateString, out var date))
                            {
                                parsedDate = date;
                            }
                            else if (DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm:ss.fffffff", null, System.Globalization.DateTimeStyles.None, out var dateExact))
                            {
                                parsedDate = dateExact;
                            }
                            else if (DateTime.TryParseExact(dateString, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var dateSimple))
                            {
                                parsedDate = dateSimple;
                            }
                        }
                        
                        var project = new ModStack.Common.Models.Project
                        {
                            Name = parts[0],
                            Path = parts[1],
                            Domain = parts[2],
                            CreatedAt = parsedDate ?? DateTime.UtcNow,
                            IsActive = Directory.Exists(parts[1])
                        };

                        // Try to find or create a default stack
                        var stacks = await stackRepo.GetAllAsync(cancellationToken).ConfigureAwait(false);
                        var defaultStack = stacks.FirstOrDefault();
                        
                        if (defaultStack == null)
                        {
                            // Create a default stack
                            defaultStack = new ModStack.Common.Models.Stack
                            {
                                DisplayName = "Default Stack",
                                Description = "Default development stack",
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow
                            };
                            await stackRepo.CreateAsync(defaultStack, cancellationToken).ConfigureAwait(false);
                        }

                        project.StackId = defaultStack.Id;
                        await projectRepo.CreateAsync(project, cancellationToken).ConfigureAwait(false);
                        migratedCount++;
                    }
                    catch (Exception ex)
                    {
                        LogService.Warning($"⚠️ Failed to migrate project: {line} - {ex.Message}");
                    }
                }
            }

            LogService.Info($"Migrated {migratedCount} projects from file-based storage");

            // Backup the original file
            var backupFile = projectsFile + ".backup." + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            File.Move(projectsFile, backupFile, overwrite: true);
            LogService.Info($"Original projects file backed up to: {backupFile}");
            
            return migratedCount;
        }
        catch (Exception ex)
        {
            LogService.LogException("Failed to migrate file-based data", ex);
            throw;
        }
    }

    private async Task<SampleDataSeedResult> SeedSampleDataIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            LogService.Info("🔍 Checking if database needs sample data seeding...");
            
            // Check if packages table is empty using direct repository creation
            var packageRepository = new PackageRepository(_connectionString);
            
            LogService.Info("📊 Attempting to query existing packages...");
            var existingPackages = await packageRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            
            LogService.Info($"📊 Found {existingPackages.Count} existing packages in database");
            
            // Check if we should reseed based on version for packages domain
            var shouldReseed = await ShouldReseedAsync("packages", PackagesSeedVersion, cancellationToken).ConfigureAwait(false);
            
            if (existingPackages.Count == 0 || shouldReseed)
            {
                var reason = existingPackages.Count == 0 ? "empty database" : "version update";
                LogService.Info($"📋 Database needs seeding: {reason}");
                
                // Atomic seeding across all domains
                var (packagesSeeded, packagesSkipped, stacksSeeded) = await SeedAllDomainsAtomicallyAsync(cancellationToken).ConfigureAwait(false);
                
                LogService.Info($"✅ Sample data seeding completed: Created={packagesSeeded}, Skipped={packagesSkipped}");
                
                // Verify seeding worked
                var newCount = await packageRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
                LogService.Info($"📊 After seeding: {newCount.Count} packages in database");
                
                return new SampleDataSeedResult(packagesSeeded, packagesSkipped);
            }
            else
            {
                LogService.Info($"📋 Database already contains data, skipping package seeding: Skipped={existingPackages.Count} (already present)");
                
                // Always seed stacks (they have their own versioning)
                await SeedDefaultStacksAsync(cancellationToken).ConfigureAwait(false);
                return new SampleDataSeedResult(0, existingPackages.Count); // No new packages seeded, existing packages count as "skipped"
            }
        }
        catch (Exception ex)
        {
            LogService.LogException("Failed to seed sample data", ex);
            LogService.Info("⚠️ Sample data seeding failed but application startup will continue");
            // Don't throw here as this is not critical for app startup
            return new SampleDataSeedResult(0, 0);
        }
    }

    private static string GetDatabaseConnectionString()
    {
        // Allow override via environment variable for dev/testing
        var customDbPath = Environment.GetEnvironmentVariable("MODSTACK_DB_PATH");
        
        string dbPath;
        if (!string.IsNullOrEmpty(customDbPath))
        {
            dbPath = customDbPath;
        }
        else
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            dbPath = Path.Combine(appDataPath, "ModStack", "Data", "modstack.db");
        }
        
        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        
        // Add SQLite-specific settings to help with locking issues
        // Note: Microsoft.Data.Sqlite uses different syntax than System.Data.SQLite
        return $"Data Source={dbPath};Cache=Shared;Default Timeout=30;";
    }
    
    /// <summary>
    /// Configure SQLite connection with optimal settings for concurrency and performance
    /// </summary>
    private static async Task ConfigureSqliteConnectionAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        try
        {
            // Set SQLite pragmas for better concurrency and performance (batched for efficiency)
            const string batchedPragmas = @"
                PRAGMA journal_mode=WAL;           -- Write-Ahead Logging for better concurrency
                PRAGMA synchronous=NORMAL;          -- Balanced sync mode
                PRAGMA busy_timeout=30000;          -- 30 second timeout for locked database
                PRAGMA cache_size=10000;           -- Increase cache size for better performance
                PRAGMA temp_store=MEMORY;           -- Store temp tables in memory
                PRAGMA foreign_keys=ON;             -- Enable foreign key constraints";
            
            using var command = connection.CreateCommand();
            command.CommandText = batchedPragmas;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log but don't throw - pragma failures shouldn't prevent database operations
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to configure SQLite pragmas: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Seed all domains atomically to ensure consistency
    /// </summary>
    private async Task<(int PackagesSeeded, int PackagesSkipped, int StacksSeeded)> SeedAllDomainsAtomicallyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            LogService.Info("🔧 Starting atomic seeding across all domains...");
            
            var packagesSeeded = 0;
            var packagesSkipped = 0;
            var stacksSeeded = 0;
            
            // Use single transaction for true atomicity
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ConfigureSqliteConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
            
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            
            try
            {
                // Seed packages domain
                var shouldReseedPackages = await ShouldReseedAsync("packages", PackagesSeedVersion, cancellationToken).ConfigureAwait(false);
                if (shouldReseedPackages)
                {
                    var packageResult = await CreateDefaultPackagesAsync(connection, (SqliteTransaction)transaction, cancellationToken).ConfigureAwait(false);
                    packagesSeeded = packageResult.Created;
                    packagesSkipped = packageResult.Skipped;
                    await RecordSeedVersionInTransactionAsync(connection, (SqliteTransaction)transaction, "packages", PackagesSeedVersion, cancellationToken).ConfigureAwait(false);
                    LogService.Info($"📦 Packages domain seeded successfully: {packageResult.Created} created, {packageResult.Skipped} skipped");
                }
                
                // Seed stacks domain
                var shouldReseedStacks = await ShouldReseedAsync("stacks", StacksSeedVersion, cancellationToken).ConfigureAwait(false);
                if (shouldReseedStacks)
                {
                    stacksSeeded = await CreateDefaultStacksInTransactionAsync(connection, (SqliteTransaction)transaction, cancellationToken).ConfigureAwait(false);
                    await RecordSeedVersionInTransactionAsync(connection, (SqliteTransaction)transaction, "stacks", StacksSeedVersion, cancellationToken).ConfigureAwait(false);
                    LogService.Info($"📚 Stacks domain seeded successfully: {stacksSeeded} stacks created");
                }
                
                // Commit the transaction if all seeding succeeded
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                
                LogService.Info($"🎯 Atomic seeding completed: Packages={packagesSeeded} created, {packagesSkipped} skipped, Stacks={stacksSeeded}");
                
                return (packagesSeeded, packagesSkipped, stacksSeeded);
            }
            catch (Exception ex)
            {
                // Rollback the transaction on any error
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                LogService.LogException("Atomic seeding transaction rolled back", ex);
                throw;
            }
        }
        catch (Exception ex)
        {
            LogService.LogException("Atomic seeding failed", ex);
            throw;
        }
    }
    
    /// <summary>
    /// Create default packages from JSON seed data file
    /// Can work standalone (creates own connection/transaction) or within existing transaction
    /// </summary>
    private async Task<PackageSeedResult> CreateDefaultPackagesAsync(SqliteConnection? connection = null, SqliteTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        try
        {
            LogService.Info("🔧 Creating default packages from JSON seed data...");
            
            // Load seed data from JSON file
            var seedData = await LoadSeedDataFromJsonAsync(cancellationToken).ConfigureAwait(false);
            if (seedData?.Packages == null || seedData.Packages.Count == 0)
            {
                LogService.Warning("⚠️ No seed data found in JSON file");
                return new PackageSeedResult(0, 0);
            }
            
            LogService.Info($"📋 Loaded {seedData.Packages.Count} packages from seed data file");
            
            // Handle standalone mode (create own connection/transaction) or transaction mode
            var isStandalone = connection == null;
            SqliteConnection? localConnection = null;
            SqliteTransaction? localTransaction = null;
            
            try
            {
                if (isStandalone)
                {
                    // Create own connection and transaction for standalone mode
                    localConnection = new SqliteConnection(_connectionString);
                    await localConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    localTransaction = (SqliteTransaction)await localConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                    connection = localConnection;
                    transaction = localTransaction;
                }
                
                var packagesCreated = 0;
                var packagesSkipped = 0;
            
            foreach (var packageData in seedData.Packages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                LogService.Info($"📦 Creating package: {packageData.DisplayName}");
                
                // Check if package already exists (thread-safe)
                var existsSql = "SELECT COUNT(*) FROM Packages WHERE Name = @Name";
                var exists = await connection!.QuerySingleAsync<int>(
                    new CommandDefinition(existsSql, new { packageData.Name }, transaction, cancellationToken: cancellationToken));
                
                if (exists > 0)
                {
                    LogService.Info($"⏭️ Package {packageData.Name} already exists, skipping");
                    packagesSkipped++;
                    continue;
                }

                // Insert package with thread-safe operation
                const string insertPackageSql = @"
                    INSERT OR IGNORE INTO Packages (Name, DisplayName, Description, Category, Icon, IsPopular, 
                                        DownloadSize, Tags, Homepage, Documentation, License, 
                                        LastUpdated, IsUserAdded, PackageType)
                    VALUES (@Name, @DisplayName, @Description, @Category, @Icon, @IsPopular,
                            @DownloadSize, @Tags, @Homepage, @Documentation, @License,
                            @LastUpdated, @IsUserAdded, @PackageType);
                    SELECT last_insert_rowid();";

                var packageId = await connection!.QuerySingleAsync<long>(
                    new CommandDefinition(insertPackageSql, new
                {
                    packageData.Name,
                    packageData.DisplayName,
                    Description = packageData.Description ?? string.Empty,
                    packageData.Category,
                    packageData.Icon,
                    packageData.IsPopular,
                        DownloadSize = 0L, // Default size
                        Tags = JsonSerializer.Serialize(packageData.Tags),
                        Homepage = string.Empty,
                        Documentation = string.Empty,
                        License = string.Empty,
                        LastUpdated = DateTime.UtcNow,
                        IsUserAdded = false, // Seed data packages are not user-added
                        PackageType = (int)packageData.PackageType
                    }, transaction, cancellationToken: cancellationToken));

                packagesCreated++;
                LogService.Info($"✅ Created package: {packageData.DisplayName} (ID: {packageId})");

                // Insert package versions
                foreach (var versionData in packageData.Versions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    const string insertVersionSql = @"
                        INSERT INTO PackageVersions (PackageId, Version, DisplayName, ServiceName, 
                                                  ReleaseDate, IsStable, IsRecommended, IsLTS, 
                                                  Size, DownloadUrls)
                        VALUES (@PackageId, @Version, @DisplayName, @ServiceName, 
                               @ReleaseDate, @IsStable, @IsRecommended, @IsLTS, 
                               @Size, @DownloadUrls)";

                    await connection!.ExecuteAsync(
                        new CommandDefinition(insertVersionSql, new
                        {
                            PackageId = packageId,
                        versionData.Version,
                        versionData.DisplayName,
                        versionData.ServiceName,
                        versionData.ReleaseDate,
                        versionData.IsStable,
                        versionData.IsRecommended,
                        versionData.IsLTS,
                        versionData.Size,
                            DownloadUrls = JsonSerializer.Serialize(versionData.DownloadUrls)
                        }, transaction, cancellationToken: cancellationToken));

                    LogService.Info($"  📦 Added version: {versionData.DisplayName}");
                }
            }
            
            // Commit transaction if in standalone mode
            if (isStandalone && localTransaction != null)
            {
                await localTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            
            LogService.Info($"🎯 Package seeding completed: {packagesCreated} created, {packagesSkipped} skipped");
            return new PackageSeedResult(packagesCreated, packagesSkipped);
        }
        catch (Exception ex)
        {
                // Rollback transaction if in standalone mode
                if (isStandalone && localTransaction != null)
                {
                    await localTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                }
                LogService.LogException("Failed to create default packages from JSON", ex);
                throw;
            }
            finally
            {
                // Dispose local resources if in standalone mode
                if (isStandalone)
                {
                    localTransaction?.Dispose();
                    localConnection?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            LogService.LogException("Failed to create default packages from JSON", ex);
            throw;
        }
    }
    
    
    /// <summary>
    /// Create default stacks within a transaction
    /// </summary>
    private static async Task<int> CreateDefaultStacksInTransactionAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            LogService.Info("🔧 Creating default stacks...");
            
            // Check if any stacks exist
            var existsSql = "SELECT COUNT(*) FROM Stacks";
            var existingCount = await connection.QuerySingleAsync<int>(
                new CommandDefinition(existsSql, transaction, cancellationToken: cancellationToken));
            
            if (existingCount > 0)
            {
                LogService.Info("📋 Stacks already exist, skipping seeding");
                return 0;
            }
            
            // Create default development stack
            const string insertStackSql = @"
                INSERT INTO Stacks (DisplayName, Description, IsActive, CreatedAt)
                VALUES (@DisplayName, @Description, @IsActive, @CreatedAt);
                SELECT last_insert_rowid();";
            
            var stackId = await connection.QuerySingleAsync<long>(
                new CommandDefinition(insertStackSql, new
                {
                    DisplayName = "Default Development Stack",
                    Description = "Default development environment with Apache, MySQL, and PHP",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }, transaction, cancellationToken: cancellationToken));
            
            LogService.Info($"✅ Created default stack: Default Development Stack (ID: {stackId})");
            return 1; // Return count of stacks created
        }
        catch (Exception ex)
        {
            LogService.LogException("Failed to create default stacks", ex);
            throw;
        }
    }
    
    /// <summary>
    /// Record seed version within a transaction
    /// </summary>
    private static async Task RecordSeedVersionInTransactionAsync(SqliteConnection connection, SqliteTransaction transaction, string domain, string version, CancellationToken cancellationToken = default)
    {
        try
        {
            const string insertSql = "INSERT INTO SeedVersions (Domain, Version, AppliedAt) VALUES (@Domain, @Version, @AppliedAt)";
            var parameters = new { Domain = domain, Version = version, AppliedAt = DateTime.UtcNow };

            await connection.ExecuteAsync(
                new CommandDefinition(insertSql, parameters, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            // Update cache with the new version
            _cachedSeedVersions[domain] = version;
            LogService.Info($"📝 Recorded seed version for {domain}: {version} at {DateTime.UtcNow} (cache updated)");
        }
        catch (Exception ex)
        {
            LogService.LogException("Failed to record seed version", ex);
            throw;
        }
    }
    
    /// <summary>
    /// Create default stacks from seed data
    /// </summary>
    private async Task<int> SeedDefaultStacksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            LogService.Info("🔧 Creating default stacks...");
            
            // Check if we should reseed stacks based on version
            var shouldReseed = await ShouldReseedAsync("stacks", StacksSeedVersion, cancellationToken).ConfigureAwait(false);
            
            if (!shouldReseed)
            {
                LogService.Info("📋 Stacks already up to date, skipping seeding");
                return 0;
            }
            
            // Create default stacks
            var stackRepository = new StackRepository(_connectionString);
            
            // Check if any stacks exist
            var existingStacks = await stackRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            
            if (existingStacks.Count > 0)
            {
                LogService.Info($"📋 Stacks already exist, skipping seeding: Found {existingStacks.Count} stacks");
                return existingStacks.Count;
            }
            
            // Create default development stack
            var defaultStack = new ModStack.Common.Models.Stack
            {
                DisplayName = "Default Development Stack",
                Description = "Default development environment with Apache, MySQL, and PHP",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            
            await stackRepository.CreateAsync(defaultStack, cancellationToken).ConfigureAwait(false);
            
            // Record seed version for stacks domain
            await RecordSeedVersionAsync("stacks", StacksSeedVersion, cancellationToken).ConfigureAwait(false);
            
            // Verify seeding worked
            var newCount = await stackRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            LogService.Info($"✅ Default stacks seeded successfully: Created 1 stack, Total stacks: {newCount.Count}");
            
            return newCount.Count;
        }
        catch (Exception ex)
        {
            LogService.LogException("Failed to seed default stacks", ex);
            LogService.Info("⚠️ Default stacks seeding failed but application startup will continue");
            return 0;
        }
    }
    
    /// <summary>
    /// Load seed data from JSON file using strongly typed DTOs
    /// </summary>
    private static async Task<SeedDataFile?> LoadSeedDataFromJsonAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var seedDataPath = Environment.GetEnvironmentVariable("MODSTACK_SEED_FILE") 
                               ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Services", "SeedData", "DefaultPackages.json");
            
            if (!File.Exists(seedDataPath))
            {
                LogService.Warning($"⚠️ Seed data file not found at: {seedDataPath}");
                return null;
            }
            
            var jsonContent = await File.ReadAllTextAsync(seedDataPath, cancellationToken).ConfigureAwait(false);
            var seedData = JsonSerializer.Deserialize<SeedDataFile>(jsonContent, JsonOptions);
            
            LogService.Info($"📄 Successfully loaded seed data from: {seedDataPath}");
            return seedData;
        }
        catch (Exception ex)
        {
            LogService.LogException("Failed to load seed data from JSON", ex);
            return null;
        }
    }
    
    /// <summary>
    /// Debug method to log current packages in the database
    /// </summary>
    private async Task LogCurrentPackagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var packageRepository = new PackageRepository(_connectionString);
            var allPackages = await packageRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            
            LogService.Info($"📊 Database Packages Summary:");
            LogService.Info($"   • Total packages: {allPackages.Count}");
            
            if (allPackages.Count > 0)
            {
                LogService.Info("📋 All Packages in Database:");
                foreach (var package in allPackages)
                {
                    if (package.AvailableVersions?.Count > 0)
                    {
                        foreach (var version in package.AvailableVersions.Take(3)) // Show first 3 versions
                        {
                            var recommended = version.IsRecommended ? "⭐ Recommended" : "";
                            var stable = version.IsStable ? "🔒 Stable" : "⚠️ Unstable";
                            LogService.Info($"       - {version.DisplayName} ({version.Version}) {recommended} {stable}");
                        }
                        if (package.AvailableVersions.Count > 3)
                        {
                            LogService.Info($"       ... and {package.AvailableVersions.Count - 3} more versions");
                        }
                    }
                }
            }
            else
            {
                LogService.Warning("⚠️ No packages found in database!");
            }
        }
        catch (Exception ex)
        {
            LogService.LogException("Failed to query database packages", ex);
            LogService.Error("❌ This suggests the database schema or connection has issues");
        }
    }

    /// <summary>
    /// Run database migrations with retry logic for database lock issues
    /// </summary>
    private async Task RunMigrationsAsync(CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 1000;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
                LogService.Info($"🔄 Running database migrations... (attempt {attempt}/{maxRetries})");
                
                // Auto-discover all migration classes using reflection across all assemblies
                var migrationTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IMigration).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            var migrations = migrationTypes
                .Select(t => (IMigration)Activator.CreateInstance(t)!)
                    .OrderBy(m => ExtractMigrationOrder(m.Id))
                    .ThenBy(m => m.Id)
                .ToList();

                LogService.Info($"🔍 Auto-discovered {migrations.Count} migrations: {string.Join(", ", migrations.Select(m => m.Id))}");

            // Create and run migration runner
            var migrationRunner = new MigrationRunner(migrations);
                await migrationRunner.RunAsync(_connectionString, cancellationToken).ConfigureAwait(false);
            
            LogService.Info("✅ Database migrations completed successfully");
                return; // Success, exit retry loop
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5 && attempt < maxRetries) // SQLITE_BUSY
            {
                LogService.Warning($"⚠️ Database is locked (attempt {attempt}/{maxRetries}), retrying in {retryDelayMs}ms...");
                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
                LogService.LogException("Failed to run database migrations", ex);
            throw;
        }
        }
        
        // If we get here, all retries failed
        throw new InvalidOperationException($"Database migrations failed after {maxRetries} attempts due to database lock");
    }

    /// <summary>
    /// Check if the database has already been initialized by looking for existing packages
    /// </summary>
    private async Task<bool> IsDatabaseInitializedAsync(CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 500;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await ConfigureSqliteConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

                // Check if core tables exist and have any data
                var checkTablesSql = @"
                    SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type='table' AND name IN ('Packages', 'Stacks', '__Migrations'))";
            
            using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = checkTablesSql;
                var tablesExistResult = await checkCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            
                if (tablesExistResult == null || !Convert.ToBoolean(tablesExistResult))
            {
                    LogService.Info("🔍 DatabaseInitializationService: Core tables do not exist");
                return false;
            }

            // Check if Packages table has any data
            var countSql = "SELECT COUNT(*) FROM Packages";
            using var countCommand = connection.CreateCommand();
            countCommand.CommandText = countSql;
                var packageCount = await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            
            var hasPackages = Convert.ToInt32(packageCount) > 0;
                LogService.Info($"🔍 DatabaseInitializationService: Found {packageCount ?? 0} packages in database");
                
                // Short-circuit: if packages exist, database is initialized
                if (hasPackages)
                {
                    LogService.Info("🔍 DatabaseInitializationService: Database initialized = true (packages found)");
                    return true;
                }
                
                // Only check migrations if no packages found
                var hasMigrations = false;
                if (Convert.ToBoolean(tablesExistResult))
                {
                    var migrationCountSql = "SELECT COUNT(*) FROM __Migrations";
                    using var migrationCountCommand = connection.CreateCommand();
                    migrationCountCommand.CommandText = migrationCountSql;
                    var migrationCount = await migrationCountCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    
                    hasMigrations = Convert.ToInt32(migrationCount) > 0;
                    LogService.Info($"🔍 DatabaseInitializationService: Found {migrationCount ?? 0} applied migrations");
                }
                else
                {
                    LogService.Info("🔍 DatabaseInitializationService: __Migrations table does not exist");
                }
                
                // Database is considered initialized if it has packages OR applied migrations
                // This handles cases where packages might be deleted but migrations remain
                var isInitialized = hasPackages || hasMigrations;
                LogService.Info($"🔍 DatabaseInitializationService: Database initialized = {isInitialized} (packages: {hasPackages}, migrations: {hasMigrations})");
                
                return isInitialized;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5 && attempt < maxRetries) // SQLITE_BUSY
            {
                LogService.Warning($"⚠️ Database is locked during initialization check (attempt {attempt}/{maxRetries}), retrying in {retryDelayMs}ms...");
                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
                LogService.LogException("DatabaseInitializationService: Error checking initialization status", ex);
                LogService.Error($"Error checking database initialization status: {ex.Message}");
            return false; // If we can't check, assume not initialized
        }
        }
        
        // If we get here, all retries failed
        LogService.Warning($"⚠️ Database initialization check failed after {maxRetries} attempts due to database lock");
        return false; // Assume not initialized if we can't check
    }


    /// <summary>
    /// Cache for seed versions per domain to avoid repeated database queries
    /// </summary>
    private static readonly Dictionary<string, string> _cachedSeedVersions = [];

    /// <summary>
    /// Clear the seed version cache (useful for testing or manual cache invalidation)
    /// </summary>
    public static void ClearSeedVersionCache()
    {
        _cachedSeedVersions.Clear();
        System.Diagnostics.Debug.WriteLine("🧹 Cleared seed version cache");
    }

    /// <summary>
    /// Check if seed data needs to be updated based on version for a specific domain
    /// </summary>
    private async Task<bool> ShouldReseedAsync(string domain, string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check cache first
            if (_cachedSeedVersions.TryGetValue(domain, out var cachedVersion))
            {
                LogService.Info($"🔍 Using cached version for domain {domain}: {cachedVersion}");
                return string.Compare(cachedVersion, currentVersion, StringComparison.OrdinalIgnoreCase) < 0;
            }

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ConfigureSqliteConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

            // Check if SeedVersions table exists
            var checkTableSql = "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type='table' AND name='SeedVersions')";
            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = checkTableSql;
            var tableExists = await checkCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            
            if (tableExists == null || !Convert.ToBoolean(tableExists))
            {
                // Table doesn't exist, need to seed
                LogService.Info($"🔍 SeedVersions table doesn't exist, need to seed domain {domain}");
                return true;
            }

            // Check current seed version for domain
            const string versionSql = "SELECT Version FROM SeedVersions WHERE Domain = @Domain ORDER BY AppliedAt DESC LIMIT 1";
            using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = versionSql;
            versionCommand.Parameters.AddWithValue("@Domain", domain);
            var currentVersionResult = await versionCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            
            // Cache the result (even if null)
            var versionToCache = currentVersionResult?.ToString() ?? string.Empty;
            _cachedSeedVersions[domain] = versionToCache;
            LogService.Info($"🔍 Cached version for domain {domain}: {versionToCache}");
            
            // Reseed if no version is recorded or if current version is older
            return currentVersionResult == null || 
                   string.Compare(currentVersionResult.ToString(), currentVersion, StringComparison.OrdinalIgnoreCase) < 0;
        }
        catch (Exception ex)
        {
            LogService.LogException("Failed to check seed version", ex);
            return true; // If we can't check, assume we need to seed
        }
    }

    /// <summary>
    /// Record seed version after successful seeding for a specific domain
    /// </summary>
    private async Task RecordSeedVersionAsync(string domain, string version, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ConfigureSqliteConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

            const string insertSql = "INSERT INTO SeedVersions (Domain, Version, AppliedAt) VALUES (@Domain, @Version, @AppliedAt)";
            var parameters = new { Domain = domain, Version = version, AppliedAt = DateTime.UtcNow };

            await connection.ExecuteAsync(
                new CommandDefinition(insertSql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);

            // Update cache with the new version
            _cachedSeedVersions[domain] = version;
            LogService.Info($"📝 Recorded seed version for {domain}: {version} at {DateTime.UtcNow} (cache updated)");
        }
        catch (Exception ex)
        {
            LogService.LogException("Failed to record seed version", ex);
        }
    }

    /// <summary>
    /// Extract migration order number from migration ID for safe ordering
    /// Enforces naming convention: NNN_Description (e.g., "001_AddTable", "002_AddIndex")
    /// </summary>
    private static int ExtractMigrationOrder(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Migration ID cannot be null or empty", nameof(id));
        }

        var parts = id.Split('_');
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Migration ID '{id}' does not follow the required format 'NNN_Description'. Expected format: three-digit number followed by underscore and description (e.g., '001_AddTable')", nameof(id));
        }

        var numericPart = parts[0];
        if (numericPart.Length != 3 || !numericPart.All(char.IsDigit))
        {
            throw new ArgumentException($"Migration ID '{id}' does not start with a valid 3-digit number. Expected format: 'NNN_Description' where NNN is exactly 3 digits (e.g., '001_AddTable')", nameof(id));
        }

        if (!int.TryParse(numericPart, out var num))
        {
            throw new ArgumentException($"Migration ID '{id}' numeric prefix '{numericPart}' could not be parsed as an integer", nameof(id));
        }

        return num;
    }

    /// <summary>
    /// Get database status for health checks and diagnostics
    /// </summary>
    public async Task<DatabaseStatus> GetDatabaseStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ConfigureSqliteConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

            var status = new DatabaseStatus
            {
                TablesExist = [],
                SeedVersions = [],
                AppliedMigrationIds = []
            };

            // Check if main tables exist
            var tablesSql = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Packages', 'PackageVersions', 'Stacks', 'SeedVersions', '__Migrations')";
            var tables = await connection.QueryAsync<string>(tablesSql).ConfigureAwait(false);
            var tablesExist = tables.ToHashSet();

            // Get package count
            var packageCount = 0;
            if (tablesExist.Contains("Packages"))
            {
                var packageCountSql = "SELECT COUNT(*) FROM Packages";
                packageCount = await connection.QuerySingleAsync<int>(packageCountSql).ConfigureAwait(false);
            }

            // Get stack count
            var stackCount = 0;
            if (tablesExist.Contains("Stacks"))
            {
                var stackCountSql = "SELECT COUNT(*) FROM Stacks";
                stackCount = await connection.QuerySingleAsync<int>(stackCountSql).ConfigureAwait(false);
            }

            // Get seed versions by domain
            var seedVersions = new Dictionary<string, string>();
            if (tablesExist.Contains("SeedVersions"))
            {
                const string seedVersionSql = "SELECT Domain, Version FROM SeedVersions ORDER BY AppliedAt DESC";
                var seedVersionResults = await connection.QueryAsync<(string Domain, string Version)>(seedVersionSql).ConfigureAwait(false);
                seedVersions = seedVersionResults.ToDictionary(sv => sv.Domain, sv => sv.Version);
            }

            // Get migration count and IDs
            var appliedMigrations = 0;
            var appliedMigrationIds = new List<string>();
            if (tablesExist.Contains("__Migrations"))
            {
                var migrationCountSql = "SELECT COUNT(*) FROM __Migrations";
                appliedMigrations = await connection.QuerySingleAsync<int>(migrationCountSql).ConfigureAwait(false);
                
                var migrationIdsSql = "SELECT Id FROM __Migrations ORDER BY Id";
                var migrationIds = await connection.QueryAsync<string>(migrationIdsSql).ConfigureAwait(false);
                appliedMigrationIds = [.. migrationIds];
            }

            // Check if database is initialized using same logic as IsDatabaseInitializedAsync
            // Database is considered initialized if it has packages OR applied migrations
            var hasPackages = packageCount > 0;
            var hasMigrations = appliedMigrations > 0;
            var isInitialized = hasPackages || hasMigrations;

            // Create the final status object
            status = new DatabaseStatus
            {
                IsInitialized = isInitialized,
                TablesExist = tablesExist,
                PackageCount = packageCount,
                StackCount = stackCount,
                SeedVersions = seedVersions,
                AppliedMigrations = appliedMigrations,
                AppliedMigrationIds = appliedMigrationIds
            };

            return status;
        }
        catch (Exception ex)
        {
            LogService.LogException("Failed to get database status", ex);
            return new DatabaseStatus { IsInitialized = false };
        }
    }
}

/// <summary>
/// Database status information for health checks
/// </summary>
public record DatabaseStatus
{
    public bool IsInitialized { get; init; }
    public HashSet<string> TablesExist { get; init; } = [];
    public int PackageCount { get; init; }
    public int StackCount { get; init; }
    public Dictionary<string, string> SeedVersions { get; init; } = [];
    public int AppliedMigrations { get; init; }
    public List<string> AppliedMigrationIds { get; init; } = [];
}
