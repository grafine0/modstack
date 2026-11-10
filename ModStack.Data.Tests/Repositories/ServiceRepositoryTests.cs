using ModStack.Data.Repositories;
using ModStack.Core.Models;
using ModStack.Common;
using Microsoft.Data.Sqlite;
using System.Data;
using Xunit;
using System.Text.Json;
using ServiceType = ModStack.Core.Models.ServiceType;
using ServiceStatus = ModStack.Common.ServiceStatus;

namespace ModStack.Data.Tests.Repositories;

public class ServiceRepositoryTests : IDisposable
{
    private readonly ServiceRepository _repository;
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public ServiceRepositoryTests()
    {
        // Create in-memory SQLite database for testing
        _connectionString = "Data Source=:memory:";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        
        _repository = new ServiceRepository(_connectionString);
        
        // Initialize test database
        InitializeTestDatabase();
    }

    private void InitializeTestDatabase()
    {
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS Services (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Description TEXT,
                ServiceType INTEGER NOT NULL,
                Version TEXT NOT NULL,
                BinaryPath TEXT NOT NULL,
                ConfigPath TEXT,
                DefaultPort INTEGER,
                IsInstalled INTEGER NOT NULL DEFAULT 0,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                InstallDate DATETIME,
                AutoStart INTEGER NOT NULL DEFAULT 0,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            )";

        using var command = new SqliteCommand(createTableSql, _connection);
        command.ExecuteNonQuery();

        // Insert test data
        InsertTestData();
    }

    private void InsertTestData()
    {
        var insertSql = @"
            INSERT INTO Services (Name, DisplayName, Description, ServiceType, Version, BinaryPath, ConfigPath, DefaultPort,
                                 IsInstalled, IsEnabled, InstallDate, AutoStart, CreatedAt, UpdatedAt)
            VALUES 
                ('Apache', 'Apache HTTP Server', 'Apache HTTP Server', 1, '2.4.54', '/usr/local/apache', '/usr/local/apache/conf', 80, 1, 1, '2024-01-01 10:00:00', 1, '2024-01-01 10:00:00', '2024-01-01 10:00:00'),
                ('MySQL', 'MySQL Database', 'MySQL Database Server', 2, '8.0.35', '/usr/local/mysql', '/usr/local/mysql/my.cnf', 3306, 1, 1, '2024-01-01 10:00:00', 1, '2024-01-01 10:00:00', '2024-01-01 10:00:00'),
                ('PHP', 'PHP Runtime', 'PHP Runtime Environment', 3, '8.3.0', '/usr/local/php', '/usr/local/php/php.ini', 9000, 1, 1, '2024-01-01 10:00:00', 1, '2024-01-01 10:00:00', '2024-01-01 10:00:00'),
                ('Nginx', 'Nginx Web Server', 'Nginx Web Server', 1, '1.24.0', '/usr/local/nginx', '/usr/local/nginx/nginx.conf', 8080, 0, 0, null, 0, '2024-01-01 10:00:00', '2024-01-01 10:00:00')";

        using var command = new SqliteCommand(insertSql, _connection);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Resets the database to a clean state for test isolation
    /// </summary>
    private void ResetDatabase()
    {
        var resetSql = "DELETE FROM Services";
        using var command = new SqliteCommand(resetSql, _connection);
        command.ExecuteNonQuery();
        
        // Reset auto-increment counter
        var resetAutoIncrementSql = "DELETE FROM sqlite_sequence WHERE name='Services'";
        using var resetCommand = new SqliteCommand(resetAutoIncrementSql, _connection);
        resetCommand.ExecuteNonQuery();
    }

    /// <summary>
    /// Sets up clean database state before each test
    /// </summary>
    private void SetupCleanDatabase()
    {
        ResetDatabase();
        InsertTestData();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllServices()
    {
        // Act
        var services = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(services);
        Assert.Equal(4, services.Count);
        
        var apacheService = services.FirstOrDefault(s => s.Name == "Apache");
        Assert.NotNull(apacheService);
        Assert.Equal("Apache HTTP Server", apacheService.DisplayName);
        Assert.Equal(ServiceType.Apache, apacheService.ServiceType);
        Assert.Equal(80, apacheService.DefaultPort);
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnService()
    {
        // Act
        var service = await _repository.GetByIdAsync(1);

        // Assert
        Assert.NotNull(service);
        Assert.Equal("Apache", service.Name);
        Assert.Equal("Apache HTTP Server", service.DisplayName);
        Assert.Equal(ServiceType.Apache, service.ServiceType);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var service = await _repository.GetByIdAsync(999);

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public async Task GetByNameAndVersionAsync_WithValidData_ShouldReturnService()
    {
        // Act
        var service = await _repository.GetByNameAndVersionAsync("Apache", "2.4.54");

        // Assert
        Assert.NotNull(service);
        Assert.Equal("Apache", service.Name);
        Assert.Equal("2.4.54", service.DefaultVersion);
    }

    [Fact]
    public async Task GetByNameAndVersionAsync_WithInvalidData_ShouldReturnNull()
    {
        // Act
        var service = await _repository.GetByNameAndVersionAsync("NonExistent", "1.0.0");

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateNewService()
    {
        // Arrange
        var newService = new ServiceDefinition
        {
            Name = "PostgreSQL",
            DisplayName = "PostgreSQL Database",
            ServiceType = ServiceType.MySQL,
            DefaultPort = 5432,
            DefaultPath = "/postgresql",
            DefaultVersion = "15.4",
            IsInstalled = false,
            IsEnabled = true,
            Status = ServiceStatus.Stopped,
            AutoStart = false,
            Configuration = new Dictionary<string, object> { { "port", 5432 } },
            Dependencies = new List<string> { "libpq" },
            UniqueId = "postgres-001"
        };

        // Act
        var createdService = await _repository.CreateAsync(newService);

        // Assert
        Assert.NotNull(createdService);
        Assert.True(createdService.Id > 0);
        Assert.Equal("PostgreSQL", createdService.Name);
        Assert.Equal("PostgreSQL Database", createdService.DisplayName);
        Assert.Equal(ServiceType.MySQL, createdService.ServiceType);
        Assert.Equal(5432, createdService.DefaultPort);
        Assert.NotNull(createdService.CreatedAt);
        Assert.NotNull(createdService.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_WithValidService_ShouldUpdateService()
    {
        // Arrange
        var service = await _repository.GetByIdAsync(1);
        Assert.NotNull(service);
        
        service.DisplayName = "Updated Apache HTTP Server";
        service.IsInstalled = false;
        service.Status = ServiceStatus.Stopped;

        // Act
        var result = await _repository.UpdateAsync(service);

        // Assert
        Assert.True(result);
        
        var updatedService = await _repository.GetByIdAsync(1);
        Assert.NotNull(updatedService);
        Assert.Equal("Updated Apache HTTP Server", updatedService.DisplayName);
        Assert.False(updatedService.IsInstalled);
        Assert.Equal(ServiceStatus.Stopped, updatedService.Status);
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidService_ShouldReturnFalse()
    {
        // Arrange
        var invalidService = new ServiceDefinition
        {
            Id = 999,
            Name = "NonExistent",
            DisplayName = "Non Existent Service"
        };

        // Act
        var result = await _repository.UpdateAsync(invalidService);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_WithValidId_ShouldDeleteService()
    {
        // Act
        var result = await _repository.DeleteAsync(4); // Nginx service

        // Assert
        Assert.True(result);
        
        var deletedService = await _repository.GetByIdAsync(4);
        Assert.Null(deletedService);
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.DeleteAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetByTypeAsync_ShouldReturnServicesOfSpecificType()
    {
        // Act
        var webServers = await _repository.GetByTypeAsync(ServiceType.Apache);

        // Assert
        Assert.NotNull(webServers);
        Assert.Equal(2, webServers.Count); // Apache and Nginx
        Assert.All(webServers, s => Assert.Equal(ServiceType.Apache, s.ServiceType));
    }

    [Fact]
    public async Task GetInstalledAsync_ShouldReturnOnlyInstalledServices()
    {
        // Act
        var installedServices = await _repository.GetInstalledAsync();

        // Assert
        Assert.NotNull(installedServices);
        Assert.Equal(3, installedServices.Count); // Apache, MySQL, PHP
        Assert.All(installedServices, s => Assert.True(s.IsInstalled));
    }

    [Fact]
    public async Task GetEnabledAsync_ShouldReturnOnlyEnabledServices()
    {
        // Act
        var enabledServices = await _repository.GetEnabledAsync();

        // Assert
        Assert.NotNull(enabledServices);
        Assert.Equal(3, enabledServices.Count); // Apache, MySQL, PHP
        Assert.All(enabledServices, s => Assert.True(s.IsEnabled));
    }

    [Fact]
    public async Task GetMultipleServicesAsync_WithValidIds_ShouldReturnServices()
    {
        // Act
        var services = await _repository.GetMultipleServicesAsync(new List<long> { 1, 2, 3 });

        // Assert
        Assert.NotNull(services);
        Assert.Equal(3, services.Count);
        Assert.Contains(services, s => s.Name == "Apache");
        Assert.Contains(services, s => s.Name == "MySQL");
        Assert.Contains(services, s => s.Name == "PHP");
    }

    [Fact]
    public async Task GetMultipleServicesAsync_WithEmptyList_ShouldReturnEmptyList()
    {
        // Act
        var services = await _repository.GetMultipleServicesAsync(new List<long>());

        // Assert
        Assert.NotNull(services);
        Assert.Empty(services);
    }

    [Fact]
    public async Task GetMultipleServicesAsync_WithInvalidIds_ShouldReturnEmptyList()
    {
        // Act
        var services = await _repository.GetMultipleServicesAsync(new List<long> { 999, 998 });

        // Assert
        Assert.NotNull(services);
        Assert.Empty(services);
    }

    [Fact]
    public async Task CreateAsync_WithNullConfiguration_ShouldHandleGracefully()
    {
        // Arrange
        var service = new ServiceDefinition
        {
            Name = "TestService",
            DisplayName = "Test Service",
            ServiceType = ServiceType.Apache,
            DefaultPort = 8080,
            DefaultPath = "/test",
            DefaultVersion = "1.0.0",
            Configuration = null,
            Dependencies = null
        };

        // Act
        var createdService = await _repository.CreateAsync(service);

        // Assert
        Assert.NotNull(createdService);
        Assert.NotNull(createdService.Configuration);
        Assert.NotNull(createdService.Dependencies);
        Assert.Empty(createdService.Configuration);
        Assert.Empty(createdService.Dependencies);
    }

    [Fact]
    public async Task GetAllAsync_ShouldHandleNullValuesGracefully()
    {
        // Act
        var services = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(services);
        
        var nginxService = services.FirstOrDefault(s => s.Name == "Nginx");
        Assert.NotNull(nginxService);
        Assert.NotNull(nginxService.Configuration); // Should be empty dictionary, not null
        Assert.NotNull(nginxService.Dependencies); // Should be empty list, not null
        Assert.Empty(nginxService.Configuration);
        Assert.Empty(nginxService.Dependencies);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenDataInvalid()
    {
        // Arrange: Provide invalid data (missing required fields)
        var invalidService = new ServiceDefinition
        {
            Name = null, // Invalid name - should cause constraint violation
            DisplayName = "Invalid Service",
            ServiceType = ServiceType.Apache,
            DefaultPort = 8080,
            DefaultPath = "/test",
            DefaultVersion = "1.0.0"
        };

        // Act & Assert
        await Assert.ThrowsAsync<SqliteException>(() => _repository.CreateAsync(invalidService));
    }

    [Fact]
    public async Task CreateAsync_WithComplexDependencies_ShouldStoreDataCorrectly()
    {
        // Arrange: Create a service with complex dependencies
        var serviceWithComplexDependencies = new ServiceDefinition
        {
            Name = "TestServiceWithDependencies",
            DisplayName = "Test Service With Complex Dependencies",
            ServiceType = ServiceType.MySQL,
            DefaultPort = 3306,
            DefaultPath = "/test",
            DefaultVersion = "5.7",
            Dependencies = new List<string> { "libmysqlclient", "openssl", "zlib", "libssl", "libcrypto" },
            Configuration = new Dictionary<string, object> 
            { 
                { "host", "localhost" }, 
                { "port", 3306 },
                { "charset", "utf8mb4" },
                { "ssl", true },
                { "timeout", 30 }
            },
            UniqueId = "complex-test-001"
        };

        // Act
        var createdService = await _repository.CreateAsync(serviceWithComplexDependencies);

        // Assert
        Assert.NotNull(createdService);
        Assert.Equal("TestServiceWithDependencies", createdService.Name);
        Assert.Equal(5, createdService.Dependencies.Count);
        Assert.Contains("libmysqlclient", createdService.Dependencies);
        Assert.Contains("openssl", createdService.Dependencies);
        Assert.Contains("zlib", createdService.Dependencies);
        Assert.Contains("libssl", createdService.Dependencies);
        Assert.Contains("libcrypto", createdService.Dependencies);
        
        Assert.Equal(5, createdService.Configuration.Count);
        Assert.Equal("localhost", createdService.Configuration["host"]);
        Assert.Equal(3306, createdService.Configuration["port"]);
        Assert.Equal("utf8mb4", createdService.Configuration["charset"]);
        Assert.Equal(true, createdService.Configuration["ssl"]);
        Assert.Equal(30, createdService.Configuration["timeout"]);
    }

    [Fact]
    public async Task GetAllAsync_ShouldHandleLargeDataSetEfficiently()
    {
        // Arrange: Insert a large number of services
        var servicesToCreate = new List<ServiceDefinition>();
        for (int i = 0; i < 100; i++) // Reduced from 1000 for faster test execution
        {
            servicesToCreate.Add(new ServiceDefinition
            {
                Name = $"Service{i}",
                DisplayName = $"Service {i}",
                ServiceType = ServiceType.Apache,
                DefaultPort = 80 + i,
                DefaultPath = $"/service{i}",
                DefaultVersion = "1.0.0",
                IsInstalled = i % 2 == 0,
                IsEnabled = i % 3 == 0,
                Configuration = new Dictionary<string, object> { { "index", i } },
                Dependencies = new List<string> { $"dep{i}" },
                UniqueId = $"service-{i:000}"
            });
        }

        // Insert all services
        foreach (var service in servicesToCreate)
        {
            await _repository.CreateAsync(service);
        }

        // Act
        var startTime = DateTime.UtcNow;
        var services = await _repository.GetAllAsync();
        var endTime = DateTime.UtcNow;

        // Assert
        Assert.NotNull(services);
        Assert.True(services.Count >= 100); // Should have at least our 100 services plus initial test data
        Assert.True((endTime - startTime).TotalMilliseconds < 1000); // Should complete within 1 second
        
        // Verify some of our created services exist
        var createdServices = services.Where(s => s.Name.StartsWith("Service")).ToList();
        Assert.True(createdServices.Count >= 100);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentService_ShouldReturnFalse()
    {
        // Arrange: Create a service with non-existent ID
        var nonExistentService = new ServiceDefinition
        {
            Id = 99999, // Non-existent ID
            Name = "NonExistent",
            DisplayName = "Non Existent Service",
            ServiceType = ServiceType.Apache,
            DefaultPort = 8080,
            DefaultPath = "/nonexistent",
            DefaultVersion = "1.0.0"
        };

        // Act
        var result = await _repository.UpdateAsync(nonExistentService);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.DeleteAsync(99999); // Non-existent ID

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetByTypeAsync_WithNonExistentType_ShouldReturnEmptyList()
    {
        // Act
        var services = await _repository.GetByTypeAsync((ServiceType)999); // Non-existent enum value

        // Assert
        Assert.NotNull(services);
        Assert.Empty(services);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
