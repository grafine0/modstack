using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace ModStack.Data;

/// <summary>
/// Initializes the SQLite database and creates tables
/// </summary>
public class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseInitializer>? _logger;

    public DatabaseInitializer(string connectionString, ILogger<DatabaseInitializer>? logger = null)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger?.LogInformation("Initializing database...");

            // Ensure directory exists
            var connectionStringBuilder = new SqliteConnectionStringBuilder(_connectionString);
            var databasePath = connectionStringBuilder.DataSource;
            var directory = Path.GetDirectoryName(databasePath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger?.LogInformation("Created database directory: {Directory}", directory);
            }

            // Create database and tables
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Read and execute schema
            var schema = await GetEmbeddedSchemaAsync();
            if (!string.IsNullOrEmpty(schema))
            {
                try
                {
                    // Split schema into individual statements and execute them
                    var statements = SplitSqlStatements(schema);
                    _logger?.LogInformation("Found {StatementCount} SQL statements to execute", statements.Count);
                    
                    foreach (var statement in statements)
                    {
                        if (!string.IsNullOrWhiteSpace(statement))
                        {
                            try
                            {
                                using var command = connection.CreateCommand();
                                command.CommandText = statement.Trim();
                                await command.ExecuteNonQueryAsync();
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Failed to execute SQL statement: {Statement}", statement.Substring(0, Math.Min(100, statement.Length)));
                                throw;
                            }
                        }
                    }
                    
                    _logger?.LogInformation("Database schema created successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to execute embedded schema, falling back to basic tables");
                    await CreateBasicTablesAsync(connection);
                }
            }
            else
            {
                _logger?.LogWarning("Schema file not found, creating basic tables");
                await CreateBasicTablesAsync(connection);
            }

            // Run migrations for existing databases
            await RunMigrationsAsync(connection);

            _logger?.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize database");
            throw;
        }
    }

    public async Task<bool> DatabaseExistsAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Services'";
            var result = await command.ExecuteScalarAsync();
            
            return result != null;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GetEmbeddedSchemaAsync()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "ModStack.Data.Database.Schema.sql";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                _logger?.LogWarning("Embedded schema resource not found: {ResourceName}", resourceName);
                return string.Empty;
            }
            
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read embedded schema");
            return string.Empty;
        }
    }

    private static List<string> SplitSqlStatements(string sql)
    {
        var statements = new List<string>();
        var currentStatement = new System.Text.StringBuilder();
        var lines = sql.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var inCreateTrigger = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip empty lines and pure comments
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("--"))
                continue;

            // Handle CREATE TRIGGER statements specially (they have embedded semicolons)
            if (trimmedLine.ToUpper().StartsWith("CREATE TRIGGER"))
            {
                inCreateTrigger = true;
            }

            currentStatement.AppendLine(line);

            // Check if statement ends with semicolon
            if (trimmedLine.EndsWith(";"))
            {
                if (inCreateTrigger && trimmedLine.ToUpper().Contains("END;"))
                {
                    inCreateTrigger = false;
                    statements.Add(currentStatement.ToString().Trim());
                    currentStatement.Clear();
                }
                else if (!inCreateTrigger)
                {
                    statements.Add(currentStatement.ToString().Trim());
                    currentStatement.Clear();
                }
                // If we're in a trigger but it's not the END; statement, continue building
            }
        }

        // Add any remaining statement
        if (currentStatement.Length > 0)
        {
            var remaining = currentStatement.ToString().Trim();
            if (!string.IsNullOrEmpty(remaining))
            {
                statements.Add(remaining);
            }
        }

        return statements.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    private async Task CreateBasicTablesAsync(SqliteConnection connection)
    {
        var createTablesSQL = @"
            CREATE TABLE IF NOT EXISTS Services (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Description TEXT,
                ServiceType TEXT NOT NULL,
                Version TEXT NOT NULL,
                BinaryPath TEXT NOT NULL,
                ConfigPath TEXT,
                DefaultPort INTEGER,
                IsInstalled BOOLEAN DEFAULT FALSE,
                IsEnabled BOOLEAN DEFAULT TRUE,
                InstallDate DATETIME,
                AutoStart BOOLEAN DEFAULT FALSE,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(Name, Version)
            );

            CREATE TABLE IF NOT EXISTS Stacks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                Description TEXT,
                IsActive BOOLEAN DEFAULT FALSE,
                IsDefault BOOLEAN DEFAULT FALSE,
                Status TEXT DEFAULT 'Inactive',
                Configuration TEXT DEFAULT '{}',
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS StackServices (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StackId INTEGER NOT NULL,
                ServiceId INTEGER NOT NULL,
                Port INTEGER,
                Priority INTEGER DEFAULT 0,
                IsEnabled BOOLEAN DEFAULT TRUE,
                Configuration TEXT,
                FOREIGN KEY (StackId) REFERENCES Stacks(Id) ON DELETE CASCADE,
                FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE CASCADE,
                UNIQUE(StackId, ServiceId)
            );

            CREATE TABLE IF NOT EXISTS PortAllocations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Port INTEGER NOT NULL UNIQUE,
                ServiceId INTEGER,
                StackId INTEGER,
                Protocol TEXT DEFAULT 'TCP',
                IsReserved BOOLEAN DEFAULT FALSE,
                Description TEXT,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE SET NULL,
                FOREIGN KEY (StackId) REFERENCES Stacks(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS ServiceLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ServiceId INTEGER NOT NULL,
                LogLevel TEXT NOT NULL,
                Message TEXT NOT NULL,
                Details TEXT,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE CASCADE
            );";

        using var command = connection.CreateCommand();
        command.CommandText = createTablesSQL;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Runs database migrations for existing databases
    /// </summary>
    private async Task RunMigrationsAsync(SqliteConnection connection)
    {
        try
        {
            _logger?.LogInformation("Running database migrations...");

            // Migration 1: Using Services table from Schema.sql

            // Migration 4: Add Configuration column to Stacks table
            await AddConfigurationColumnToStacksAsync(connection);

            // Migration 5: Add Status column to Stacks table
            await AddStatusColumnToStacksAsync(connection);


            // Migration 7: Add package-related tables
            await AddPackageTablesAsync(connection);

            // Migration 6: Add PHP extension tables
            await AddPHPExtensionTablesAsync(connection);

            // Migration 7: Add EnableSSL column to Projects table
            await AddEnableSSLColumnToProjectsAsync(connection);
            
            // Migration 8: Ensure Projects and ProjectServices tables exist
            await EnsureProjectsTableAsync(connection);
            await EnsureProjectServicesTableAsync(connection);

            // Migration 9: Ensure InterceptedEmails table exists
            await EnsureInterceptedEmailsTableAsync(connection);

            _logger?.LogInformation("Database migrations completed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to run database migrations");
            throw;
        }
    }

    // Using Services table from Schema.sql


    /// <summary>
    /// Adds Configuration column to Stacks table if it doesn't exist
    /// </summary>
    private async Task AddConfigurationColumnToStacksAsync(SqliteConnection connection)
    {
        try
        {
            // Check if Configuration column exists
            var checkColumnSQL = @"
                SELECT COUNT(*) 
                FROM pragma_table_info('Stacks') 
                WHERE name = 'Configuration'";

            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = checkColumnSQL;
            var columnExists = await checkCommand.ExecuteScalarAsync();
            var count = Convert.ToInt32(columnExists);

            if (count == 0)
            {
                _logger?.LogInformation("Adding Configuration column to Stacks table...");

                // Add the Configuration column
                var addColumnSQL = @"
                    ALTER TABLE Stacks 
                    ADD COLUMN Configuration TEXT DEFAULT '{}'";

                using var addCommand = connection.CreateCommand();
                addCommand.CommandText = addColumnSQL;
                await addCommand.ExecuteNonQueryAsync();

                _logger?.LogInformation("Configuration column added to Stacks table successfully");
            }
            else
            {
                _logger?.LogInformation("Configuration column already exists in Stacks table");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add Configuration column to Stacks table");
            throw;
        }
    }

    /// <summary>
    /// Adds Status column to Stacks table if it doesn't exist
    /// </summary>
    private async Task AddStatusColumnToStacksAsync(SqliteConnection connection)
    {
        try
        {
            // Check if Status column exists
            var checkColumnSQL = @"
                SELECT COUNT(*) 
                FROM pragma_table_info('Stacks') 
                WHERE name = 'Status'";

            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = checkColumnSQL;
            var columnExists = await checkCommand.ExecuteScalarAsync();
            var count = Convert.ToInt32(columnExists);

            if (count == 0)
            {
                _logger?.LogInformation("Adding Status column to Stacks table...");
                System.Diagnostics.Debug.WriteLine("🔧 Adding Status column to Stacks table...");

                // Add the Status column
                var addColumnSQL = @"
                    ALTER TABLE Stacks 
                    ADD COLUMN Status TEXT DEFAULT 'Inactive'";

                using var addCommand = connection.CreateCommand();
                addCommand.CommandText = addColumnSQL;
                await addCommand.ExecuteNonQueryAsync();

                // Update existing stacks to have proper Status based on IsActive
                var updateStatusSQL = @"
                    UPDATE Stacks 
                    SET Status = CASE 
                        WHEN IsActive = 1 THEN 'Active' 
                        ELSE 'Inactive' 
                    END";

                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = updateStatusSQL;
                var rowsUpdated = await updateCommand.ExecuteNonQueryAsync();

                _logger?.LogInformation("Status column added to Stacks table successfully");
                System.Diagnostics.Debug.WriteLine($"✅ Status column added and {rowsUpdated} stack(s) updated");
            }
            else
            {
                _logger?.LogInformation("Status column already exists in Stacks table");
                System.Diagnostics.Debug.WriteLine("✅ Status column already exists in Stacks table");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add Status column to Stacks table");
            System.Diagnostics.Debug.WriteLine($"❌ Failed to add Status column: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Adds package-related tables if they don't exist
    /// </summary>
    private async Task AddPackageTablesAsync(SqliteConnection connection)
    {
        try
        {
            // Check if Packages table exists
            var checkTableSQL = @"
                SELECT COUNT(*) 
                FROM sqlite_master 
                WHERE type='table' AND name='Packages'";

            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = checkTableSQL;
            var tableExists = await checkCommand.ExecuteScalarAsync();
            var count = Convert.ToInt32(tableExists);

            if (count == 0)
            {
                _logger?.LogInformation("Adding package-related tables...");

                // Create Packages table
                var createPackagesTableSQL = @"
                    CREATE TABLE Packages (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL UNIQUE,
                        DisplayName TEXT NOT NULL,
                        Description TEXT,
                        Category TEXT NOT NULL,
                        Icon TEXT,
                        IsPopular BOOLEAN DEFAULT FALSE,
                        DownloadSize INTEGER DEFAULT 0,
                        Tags TEXT,
                        Homepage TEXT,
                        Documentation TEXT,
                        License TEXT,
                        LastUpdated DATETIME DEFAULT CURRENT_TIMESTAMP,
                        IsUserAdded BOOLEAN DEFAULT FALSE,
                        PackageType INTEGER DEFAULT 1,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";

                using var createPackagesCommand = connection.CreateCommand();
                createPackagesCommand.CommandText = createPackagesTableSQL;
                await createPackagesCommand.ExecuteNonQueryAsync();

                // Create PackageVersions table
                var createVersionsTableSQL = @"
                    CREATE TABLE PackageVersions (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        PackageId INTEGER NOT NULL,
                        Version TEXT NOT NULL,
                        DisplayName TEXT NOT NULL,
                        ReleaseDate DATETIME,
                        IsStable BOOLEAN DEFAULT TRUE,
                        IsRecommended BOOLEAN DEFAULT FALSE,
                        IsLTS BOOLEAN DEFAULT FALSE,
                        Size INTEGER DEFAULT 0,
                        DownloadUrls TEXT,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (PackageId) REFERENCES Packages(Id) ON DELETE CASCADE,
                        UNIQUE(PackageId, Version)
                    )";

                using var createVersionsCommand = connection.CreateCommand();
                createVersionsCommand.CommandText = createVersionsTableSQL;
                await createVersionsCommand.ExecuteNonQueryAsync();

                // Create PackageConfigurations table
                var createConfigsTableSQL = @"
                    CREATE TABLE PackageConfigurations (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        PackageId INTEGER NOT NULL,
                        Port INTEGER DEFAULT 0,
                        InstallPath TEXT,
                        AutoStart BOOLEAN DEFAULT FALSE,
                        RegisterAsService BOOLEAN DEFAULT FALSE,
                        Configuration TEXT,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (PackageId) REFERENCES Packages(Id) ON DELETE CASCADE
                    )";

                using var createConfigsCommand = connection.CreateCommand();
                createConfigsCommand.CommandText = createConfigsTableSQL;
                await createConfigsCommand.ExecuteNonQueryAsync();

                // Create indexes
                var createIndexesSQL = @"
                    CREATE INDEX idx_packages_category ON Packages(Category);
                    CREATE INDEX idx_packages_popular ON Packages(IsPopular);
                    CREATE INDEX idx_packages_user_added ON Packages(IsUserAdded);
                    CREATE INDEX idx_package_versions_package ON PackageVersions(PackageId);
                    CREATE INDEX idx_package_versions_stable ON PackageVersions(IsStable);
                    CREATE INDEX idx_package_configurations_package ON PackageConfigurations(PackageId)";

                using var createIndexesCommand = connection.CreateCommand();
                createIndexesCommand.CommandText = createIndexesSQL;
                await createIndexesCommand.ExecuteNonQueryAsync();

                // Create triggers
                var createTriggersSQL = @"
                    CREATE TRIGGER update_packages_timestamp 
                    AFTER UPDATE ON Packages 
                    BEGIN 
                        UPDATE Packages SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id; 
                    END;

                    CREATE TRIGGER update_package_configurations_timestamp 
                    AFTER UPDATE ON PackageConfigurations 
                    BEGIN 
                        UPDATE PackageConfigurations SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id; 
                    END";

                using var createTriggersCommand = connection.CreateCommand();
                createTriggersCommand.CommandText = createTriggersSQL;
                await createTriggersCommand.ExecuteNonQueryAsync();

                _logger?.LogInformation("Package-related tables created successfully");
            }
            else
            {
                _logger?.LogInformation("Package-related tables already exist");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add package-related tables");
            throw;
        }
    }

    /// <summary>
    /// Adds PHP extension tables to the database
    /// </summary>
    private async Task AddPHPExtensionTablesAsync(SqliteConnection connection)
    {
        try
        {
            // Check if PHPExtensions table exists
            var checkTableSQL = @"
                SELECT COUNT(*) 
                FROM sqlite_master 
                WHERE type='table' AND name='PHPExtensions'";

            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = checkTableSQL;
            var tableExists = await checkCommand.ExecuteScalarAsync();
            var count = Convert.ToInt32(tableExists);

            if (count == 0)
            {
                _logger?.LogInformation("Adding PHP extension tables...");

                var createExtensionTablesSQL = @"
                    CREATE TABLE IF NOT EXISTS PHPExtensions (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL UNIQUE,
                        DisplayName TEXT NOT NULL,
                        Description TEXT,
                        Category TEXT NOT NULL,
                        IsBuiltIn BOOLEAN DEFAULT FALSE,
                        IsZendExtension BOOLEAN DEFAULT FALSE,
                        DllName TEXT,
                        SupportedPHPVersions TEXT NOT NULL, -- JSON array
                        Dependencies TEXT DEFAULT '[]', -- JSON array
                        DownloadUrl TEXT,
                        ConfigurationTemplate TEXT,
                        IsPopular BOOLEAN DEFAULT FALSE,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS InstalledPHPExtensions (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ServiceId INTEGER NOT NULL,
                        ExtensionId INTEGER NOT NULL,
                        PHPVersion TEXT NOT NULL,
                        InstallPath TEXT NOT NULL,
                        IsEnabled BOOLEAN DEFAULT TRUE,
                        Configuration TEXT, -- JSON configuration
                        InstalledAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE CASCADE,
                        FOREIGN KEY (ExtensionId) REFERENCES PHPExtensions(Id) ON DELETE CASCADE,
                        UNIQUE(ServiceId, ExtensionId)
                    );

                    CREATE INDEX IF NOT EXISTS idx_php_extensions_category ON PHPExtensions(Category);
                    CREATE INDEX IF NOT EXISTS idx_php_extensions_popular ON PHPExtensions(IsPopular);
                    CREATE INDEX IF NOT EXISTS idx_installed_extensions_service ON InstalledPHPExtensions(ServiceId);
                    CREATE INDEX IF NOT EXISTS idx_installed_extensions_version ON InstalledPHPExtensions(PHPVersion);
                ";

                using var command = connection.CreateCommand();
                command.CommandText = createExtensionTablesSQL;
                await command.ExecuteNonQueryAsync();

                _logger?.LogInformation("PHP extension tables created successfully");
            }
            else
            {
                _logger?.LogInformation("PHP extension tables already exist");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add PHP extension tables");
            throw;
        }
    }

    /// <summary>
    /// Adds EnableSSL column to Projects table if it doesn't exist
    /// </summary>
    private async Task AddEnableSSLColumnToProjectsAsync(SqliteConnection connection)
    {
        try
        {
            // Check if Projects table exists
            var checkTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='Projects'";
            using var checkTableCommand = connection.CreateCommand();
            checkTableCommand.CommandText = checkTableSql;
            var tableExists = await checkTableCommand.ExecuteScalarAsync() != null;

            if (!tableExists)
            {
                _logger?.LogInformation("Projects table does not exist, skipping EnableSSL column migration");
                return;
            }

            // Check if EnableSSL column already exists
            var checkColumnSql = "PRAGMA table_info(Projects)";
            using var checkColumnCommand = connection.CreateCommand();
            checkColumnCommand.CommandText = checkColumnSql;
            using var reader = await checkColumnCommand.ExecuteReaderAsync();
            
            bool columnExists = false;
            while (await reader.ReadAsync())
            {
                var columnName = reader["name"].ToString();
                if (columnName == "EnableSSL")
                {
                    columnExists = true;
                    break;
                }
            }
            await reader.CloseAsync();

            if (columnExists)
            {
                _logger?.LogInformation("EnableSSL column already exists in Projects table");
                return;
            }

            // Add EnableSSL column
            _logger?.LogInformation("Adding EnableSSL column to Projects table...");
            var addColumnSql = "ALTER TABLE Projects ADD COLUMN EnableSSL BOOLEAN DEFAULT FALSE";
            using var addColumnCommand = connection.CreateCommand();
            addColumnCommand.CommandText = addColumnSql;
            await addColumnCommand.ExecuteNonQueryAsync();

            _logger?.LogInformation("EnableSSL column added to Projects table successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add EnableSSL column to Projects table");
            throw;
        }
    }

    /// <summary>
    /// Ensures Projects table exists
    /// </summary>
    private async Task EnsureProjectsTableAsync(SqliteConnection connection)
    {
        try
        {
            // Check if Projects table exists
            var checkTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='Projects'";
            using var checkTableCommand = connection.CreateCommand();
            checkTableCommand.CommandText = checkTableSql;
            var tableExists = await checkTableCommand.ExecuteScalarAsync();

            if (tableExists != null)
            {
                _logger?.LogInformation("Projects table already exists");
                return;
            }

            // Create Projects table
            _logger?.LogInformation("Creating Projects table...");
            var createTableSql = @"
                CREATE TABLE Projects (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    DisplayName TEXT NOT NULL,
                    Path TEXT NOT NULL,
                    Domain TEXT,
                    StackId INTEGER,
                    DocumentRoot TEXT,
                    IsActive BOOLEAN DEFAULT TRUE,
                    EnableSSL BOOLEAN DEFAULT FALSE,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    Configuration TEXT,
                    FOREIGN KEY (StackId) REFERENCES Stacks(Id) ON DELETE SET NULL
                );";
            
            using var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = createTableSql;
            await createTableCommand.ExecuteNonQueryAsync();

            _logger?.LogInformation("Projects table created successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to ensure Projects table exists");
            throw;
        }
    }

    /// <summary>
    /// Ensures ProjectServices table exists
    /// </summary>
    private async Task EnsureProjectServicesTableAsync(SqliteConnection connection)
    {
        try
        {
            // Check if ProjectServices table exists
            var checkTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='ProjectServices'";
            using var checkTableCommand = connection.CreateCommand();
            checkTableCommand.CommandText = checkTableSql;
            var tableExists = await checkTableCommand.ExecuteScalarAsync();

            if (tableExists != null)
            {
                _logger?.LogInformation("ProjectServices table already exists");
                return;
            }

            // Create ProjectServices table
            _logger?.LogInformation("Creating ProjectServices table...");
            var createTableSql = @"
                CREATE TABLE ProjectServices (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProjectId INTEGER NOT NULL,
                    ServiceId INTEGER NOT NULL,
                    IsEnabled BOOLEAN DEFAULT TRUE,
                    Configuration TEXT,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
                    FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE CASCADE,
                    UNIQUE(ProjectId, ServiceId)
                );";
            
            using var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = createTableSql;
            await createTableCommand.ExecuteNonQueryAsync();

            _logger?.LogInformation("ProjectServices table created successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to ensure ProjectServices table exists");
            throw;
        }
    }

    /// <summary>
    /// Ensures InterceptedEmails table exists for the Mail Catcher feature
    /// </summary>
    private async Task EnsureInterceptedEmailsTableAsync(SqliteConnection connection)
    {
        try
        {
            // Check if InterceptedEmails table exists
            var checkTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='InterceptedEmails'";
            using var checkTableCommand = connection.CreateCommand();
            checkTableCommand.CommandText = checkTableSql;
            var tableExists = await checkTableCommand.ExecuteScalarAsync();

            if (tableExists != null)
            {
                _logger?.LogInformation("InterceptedEmails table already exists");
                return;
            }

            // Create InterceptedEmails table
            _logger?.LogInformation("Creating InterceptedEmails table...");
            var createTableSql = @"
                CREATE TABLE InterceptedEmails (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    MessageId TEXT,
                    Sender TEXT NOT NULL,
                    Recipient TEXT NOT NULL,
                    Subject TEXT,
                    BodyHtml TEXT,
                    BodyText TEXT,
                    RawHeaders TEXT,
                    ReceivedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    IsRead BOOLEAN DEFAULT FALSE
                );";
            
            using var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = createTableSql;
            await createTableCommand.ExecuteNonQueryAsync();

            _logger?.LogInformation("InterceptedEmails table created successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to ensure InterceptedEmails table exists");
            throw;
        }
    }
}
