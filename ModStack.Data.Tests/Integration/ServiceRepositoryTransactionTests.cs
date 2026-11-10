using ModStack.Data.Repositories;
using ModStack.Core.Models;
using ModStack.Common;
using Microsoft.Data.Sqlite;
using System.Data;
using Xunit;
using ServiceType = ModStack.Core.Models.ServiceType;
using ServiceStatus = ModStack.Common.ServiceStatus;

namespace ModStack.Data.Tests.Integration;

public class ServiceRepositoryTransactionTests : IDisposable
{
    private readonly ServiceRepository _repository;
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public ServiceRepositoryTransactionTests()
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
                ServiceType INTEGER NOT NULL,
                DefaultPort INTEGER NOT NULL,
                IsInstalled INTEGER NOT NULL DEFAULT 0,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                Status INTEGER NOT NULL DEFAULT 0,
                AutoStart INTEGER NOT NULL DEFAULT 0,
                Configuration TEXT,
                Dependencies TEXT,
                UniqueId TEXT,
                WindowsServiceName TEXT,
                InstallDate DATETIME,
                CreatedAt DATETIME NOT NULL,
                UpdatedAt DATETIME NOT NULL,
                InstalledAt DATETIME,
                Metadata TEXT
            )";

        using var command = new SqliteCommand(createTableSql, _connection);
        command.ExecuteNonQuery();
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCommitTransaction()
    {
        // Arrange
        var service = new ServiceDefinition
        {
            Name = "Apache",
            DisplayName = "Apache HTTP Server",
            ServiceType = ServiceType.Apache,
            DefaultPort = 80,
            DefaultPath = "/apache",
            DefaultVersion = "2.4.54",
            IsInstalled = true,
            IsEnabled = true,
            Status = ServiceStatus.Running,
            AutoStart = true,
            Configuration = new Dictionary<string, object> { { "port", 80 } },
            Dependencies = new List<string> { "libssl" },
            UniqueId = "apache-001"
        };

        // Act
        var createdService = await _repository.CreateAsync(service);

        // Assert
        Assert.NotNull(createdService);
        Assert.True(createdService.Id > 0);
        
        // Verify service was actually committed to database
        var retrievedService = await _repository.GetByIdAsync(createdService.Id);
        Assert.NotNull(retrievedService);
        Assert.Equal(service.Name, retrievedService.Name);
        Assert.Equal(service.DisplayName, retrievedService.DisplayName);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldCommitTransaction()
    {
        // Arrange - Create a service first
        var service = new ServiceDefinition
        {
            Name = "MySQL",
            DisplayName = "MySQL Database",
            ServiceType = ServiceType.MySQL,
            DefaultPort = 3306,
            DefaultPath = "/mysql",
            DefaultVersion = "8.0.35",
            IsInstalled = true,
            IsEnabled = true,
            Status = ServiceStatus.Running,
            AutoStart = true,
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "mysql-001"
        };

        var createdService = await _repository.CreateAsync(service);
        Assert.NotNull(createdService);

        // Act - Update the service
        createdService.DisplayName = "Updated MySQL Database";
        createdService.IsInstalled = false;
        createdService.Status = ServiceStatus.Stopped;

        var updateResult = await _repository.UpdateAsync(createdService);

        // Assert
        Assert.True(updateResult);
        
        // Verify changes were committed to database
        var updatedService = await _repository.GetByIdAsync(createdService.Id);
        Assert.NotNull(updatedService);
        Assert.Equal("Updated MySQL Database", updatedService.DisplayName);
        Assert.False(updatedService.IsInstalled);
        Assert.Equal(ServiceStatus.Stopped, updatedService.Status);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentService_ShouldRollbackTransaction()
    {
        // Arrange
        var nonExistentService = new ServiceDefinition
        {
            Id = 999,
            Name = "NonExistent",
            DisplayName = "Non Existent Service",
            ServiceType = ServiceType.Apache,
            DefaultPort = 8080,
            DefaultPath = "/nonexistent",
            DefaultVersion = "1.0.0",
            IsInstalled = false,
            IsEnabled = false,
            Status = ServiceStatus.Stopped,
            AutoStart = false,
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "nonexistent-001"
        };

        // Act
        var updateResult = await _repository.UpdateAsync(nonExistentService);

        // Assert
        Assert.False(updateResult);
        
        // Verify no service was created or modified
        var service = await _repository.GetByIdAsync(999);
        Assert.Null(service);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateUniqueId_ShouldRollbackTransaction()
    {
        // Arrange - Create first service
        var service1 = new ServiceDefinition
        {
            Name = "Apache",
            DisplayName = "Apache HTTP Server",
            ServiceType = ServiceType.Apache,
            DefaultPort = 80,
            DefaultPath = "/apache",
            DefaultVersion = "2.4.54",
            IsInstalled = true,
            IsEnabled = true,
            Status = ServiceStatus.Running,
            AutoStart = true,
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "apache-001"
        };

        var createdService1 = await _repository.CreateAsync(service1);
        Assert.NotNull(createdService1);

        // Act - Try to create second service with same UniqueId
        var service2 = new ServiceDefinition
        {
            Name = "Apache2",
            DisplayName = "Apache HTTP Server 2",
            ServiceType = ServiceType.Apache,
            DefaultPort = 8080,
            DefaultPath = "/apache2",
            DefaultVersion = "2.4.55",
            IsInstalled = false,
            IsEnabled = true,
            Status = ServiceStatus.Stopped,
            AutoStart = false,
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "apache-001" // Same UniqueId
        };

        // This should throw an exception due to unique constraint violation
        await Assert.ThrowsAsync<Exception>(() => _repository.CreateAsync(service2));

        // Assert - Verify first service is still intact
        var retrievedService1 = await _repository.GetByIdAsync(createdService1.Id);
        Assert.NotNull(retrievedService1);
        Assert.Equal("Apache", retrievedService1.Name);
        Assert.Equal("apache-001", retrievedService1.UniqueId);
    }

    [Fact]
    public async Task ConcurrentUpdates_ShouldMaintainDataConsistency()
    {
        // Arrange - Create a service
        var service = new ServiceDefinition
        {
            Name = "Nginx",
            DisplayName = "Nginx Web Server",
            ServiceType = ServiceType.Apache,
            DefaultPort = 80,
            DefaultPath = "/nginx",
            DefaultVersion = "1.24.0",
            IsInstalled = true,
            IsEnabled = true,
            Status = ServiceStatus.Running,
            AutoStart = true,
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "nginx-001"
        };

        var createdService = await _repository.CreateAsync(service);
        Assert.NotNull(createdService);

        // Act - Perform concurrent updates
        var tasks = new List<Task<bool>>();
        
        for (int i = 0; i < 5; i++)
        {
            var serviceCopy = new ServiceDefinition
            {
                Id = createdService.Id,
                Name = createdService.Name,
                DisplayName = $"Nginx Web Server {i}",
                ServiceType = createdService.ServiceType,
                DefaultPort = createdService.DefaultPort,
                DefaultPath = createdService.DefaultPath,
                DefaultVersion = createdService.DefaultVersion,
                IsInstalled = createdService.IsInstalled,
                IsEnabled = createdService.IsEnabled,
                Status = createdService.Status,
                AutoStart = createdService.AutoStart,
                Configuration = createdService.Configuration,
                Dependencies = createdService.Dependencies,
                UniqueId = createdService.UniqueId,
                WindowsServiceName = createdService.WindowsServiceName,
                InstallDate = createdService.InstallDate,
                CreatedAt = createdService.CreatedAt,
                UpdatedAt = createdService.UpdatedAt,
                InstalledAt = createdService.InstalledAt,
                Metadata = createdService.Metadata
            };

            tasks.Add(_repository.UpdateAsync(serviceCopy));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Only one update should succeed, others should fail
        var successCount = results.Count(r => r);
        Assert.Equal(1, successCount);
        
        // Verify final state is consistent
        var finalService = await _repository.GetByIdAsync(createdService.Id);
        Assert.NotNull(finalService);
        Assert.True(finalService.DisplayName.StartsWith("Nginx Web Server"));
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
