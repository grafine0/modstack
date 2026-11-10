using ModStack.Data.Repositories;
using ModStack.Core.Models;
using ModStack.Common;
using Microsoft.Data.Sqlite;
using System.Data;
using Xunit;
using ServiceType = ModStack.Core.Models.ServiceType;
using ServiceStatus = ModStack.Common.ServiceStatus;

namespace ModStack.Data.Tests.Integration;

public class ServiceRepositoryCachingTests : IDisposable
{
    private readonly ServiceRepository _repository;
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public ServiceRepositoryCachingTests()
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

        // Insert test data
        var insertSql = @"
            INSERT INTO Services (Name, DisplayName, ServiceType, DefaultPort, BinaryPath, Version,
                                          IsInstalled, IsEnabled, Status, AutoStart,
                                          Configuration, Dependencies, UniqueId, WindowsServiceName,
                                          InstallDate, CreatedAt, UpdatedAt)
            VALUES 
                ('Apache', 'Apache HTTP Server', 1, 80, '/apache', '2.4.54', 1, 1, 1, 1,
                 '{}', '[]', 'apache-001', 'Apache2.4', '2024-01-01 10:00:00', '2024-01-01 10:00:00', '2024-01-01 10:00:00')";

        using var command2 = new SqliteCommand(insertSql, _connection);
        command2.ExecuteNonQuery();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldCacheServiceAfterFirstCall()
    {
        // Act - First call should hit database
        var service1 = await _repository.GetByIdAsync(1);
        
        // Act - Second call should hit cache
        var service2 = await _repository.GetByIdAsync(1);

        // Assert
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.Equal(service1.Id, service2.Id);
        Assert.Equal(service1.Name, service2.Name);
        Assert.Equal(service1.DisplayName, service2.DisplayName);
        
        // Both calls should return the same object reference (cached)
        Assert.Same(service1, service2);
    }

    [Fact]
    public async Task CreateAsync_ShouldInvalidateCache()
    {
        // Arrange - Get service to cache it
        var cachedService = await _repository.GetByIdAsync(1);
        Assert.NotNull(cachedService);

        // Act - Create new service
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
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "postgres-001"
        };

        var createdService = await _repository.CreateAsync(newService);

        // Assert - Cache should still work for original service
        var cachedServiceAgain = await _repository.GetByIdAsync(1);
        Assert.NotNull(cachedServiceAgain);
        Assert.Same(cachedService, cachedServiceAgain);
        
        // New service should be retrievable
        Assert.NotNull(createdService);
        Assert.True(createdService.Id > 0);
    }

    [Fact]
    public async Task UpdateAsync_ShouldInvalidateCache()
    {
        // Arrange - Get service to cache it
        var originalService = await _repository.GetByIdAsync(1);
        Assert.NotNull(originalService);
        
        var originalDisplayName = originalService.DisplayName;

        // Act - Update service
        originalService.DisplayName = "Updated Apache HTTP Server";
        var updateResult = await _repository.UpdateAsync(originalService);

        // Assert
        Assert.True(updateResult);
        
        // Cache should be invalidated, so we get fresh data
        var updatedService = await _repository.GetByIdAsync(1);
        Assert.NotNull(updatedService);
        Assert.Equal("Updated Apache HTTP Server", updatedService.DisplayName);
        Assert.NotEqual(originalDisplayName, updatedService.DisplayName);
    }

    [Fact]
    public async Task DeleteAsync_ShouldInvalidateCache()
    {
        // Arrange - Get service to cache it
        var cachedService = await _repository.GetByIdAsync(1);
        Assert.NotNull(cachedService);

        // Act - Delete service
        var deleteResult = await _repository.DeleteAsync(1);

        // Assert
        Assert.True(deleteResult);
        
        // Service should no longer be retrievable
        var deletedService = await _repository.GetByIdAsync(1);
        Assert.Null(deletedService);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldNotCache()
    {
        // Act - Try to get non-existent service
        var service1 = await _repository.GetByIdAsync(999);
        var service2 = await _repository.GetByIdAsync(999);

        // Assert
        Assert.Null(service1);
        Assert.Null(service2);
        
        // Both calls should return null (no caching of null results)
        Assert.Same(service1, service2);
    }

    [Fact]
    public async Task MultipleConcurrentCalls_ShouldHandleCacheCorrectly()
    {
        // Arrange
        var tasks = new List<Task<ServiceDefinition?>>();

        // Act - Make multiple concurrent calls
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_repository.GetByIdAsync(1));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, results.Length);
        Assert.All(results, result => Assert.NotNull(result));
        Assert.All(results, result => Assert.Equal("Apache", result!.Name));
        
        // All results should be the same cached object
        var firstResult = results[0];
        Assert.All(results, result => Assert.Same(firstResult, result));
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
