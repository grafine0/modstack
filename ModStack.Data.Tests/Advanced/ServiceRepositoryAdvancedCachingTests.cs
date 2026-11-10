using ModStack.Data.Repositories;
using ModStack.Core.Models;
using ModStack.Common;
using Microsoft.Data.Sqlite;
using System.Data;
using Xunit;
using ServiceType = ModStack.Core.Models.ServiceType;
using ServiceStatus = ModStack.Common.ServiceStatus;

namespace ModStack.Data.Tests.Advanced;

/// <summary>
/// Advanced tests for ServiceRepository caching and performance features
/// </summary>
public class ServiceRepositoryAdvancedCachingTests : IDisposable
{
    private readonly ServiceRepository _repository;
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public ServiceRepositoryAdvancedCachingTests()
    {
        // Create in-memory SQLite database for testing
        _connectionString = "Data Source=:memory:";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        
        _repository = new ServiceRepository(_connectionString);
        
        // Initialize test database
        InitializeTestDatabase();
        
        // Reset cache statistics for clean testing
        ServiceRepository.ResetCacheStatistics();
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
    public async Task CacheHitRate_ShouldTrackCorrectly()
    {
        // Arrange: Create a service
        var service = new ServiceDefinition
        {
            Name = "CacheTestService",
            DisplayName = "Cache Test Service",
            ServiceType = ServiceType.Apache, // Should be cached (critical service)
            DefaultPort = 8080,
            DefaultPath = "/cachetest",
            DefaultVersion = "1.0.0",
            IsInstalled = true,
            IsEnabled = true,
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "cache-test-001"
        };

        var createdService = await _repository.CreateAsync(service);
        Assert.NotNull(createdService);

        // Act: First access (cache miss)
        var service1 = await _repository.GetByIdAsync(createdService.Id);
        Assert.NotNull(service1);

        // Act: Second access (cache hit)
        var service2 = await _repository.GetByIdAsync(createdService.Id);
        Assert.NotNull(service2);

        // Assert: Cache statistics should show 1 hit, 1 miss
        var stats = ServiceRepository.GetCacheStatistics();
        Assert.Equal(1, stats.hits);
        Assert.Equal(1, stats.misses);
        Assert.Equal(50.0, stats.hitRate, 1); // 50% hit rate
    }

    [Fact]
    public async Task SelectiveCaching_ShouldOnlyCacheImportantServices()
    {
        // Arrange: Create services with different types
        var apacheService = new ServiceDefinition
        {
            Name = "ApacheService",
            DisplayName = "Apache HTTP Server",
            ServiceType = ServiceType.Apache, // Should be cached
            DefaultPort = 80,
            DefaultPath = "/apache",
            DefaultVersion = "2.4.54",
            IsInstalled = true,
            IsEnabled = true,
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "apache-001"
        };

        var unimportantService = new ServiceDefinition
        {
            Name = "UnimportantService",
            DisplayName = "Unimportant Service",
            ServiceType = ServiceType.Nginx, // Should not be cached (not in ShouldCacheService logic)
            DefaultPort = 8080,
            DefaultPath = "/unimportant",
            DefaultVersion = "1.0.0",
            IsInstalled = false,
            IsEnabled = false,
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "unimportant-001"
        };

        var apacheCreated = await _repository.CreateAsync(apacheService);
        var unimportantCreated = await _repository.CreateAsync(unimportantService);

        // Act: Access both services
        var apacheRetrieved = await _repository.GetByIdAsync(apacheCreated.Id);
        var unimportantRetrieved = await _repository.GetByIdAsync(unimportantCreated.Id);

        // Assert: Both should be retrieved, but only Apache should be cached
        Assert.NotNull(apacheRetrieved);
        Assert.NotNull(unimportantRetrieved);

        // Second access to Apache should hit cache
        var apacheRetrieved2 = await _repository.GetByIdAsync(apacheCreated.Id);
        Assert.NotNull(apacheRetrieved2);

        // Second access to unimportant should still miss cache
        var unimportantRetrieved2 = await _repository.GetByIdAsync(unimportantCreated.Id);
        Assert.NotNull(unimportantRetrieved2);

        // Cache statistics should show selective caching
        var stats = ServiceRepository.GetCacheStatistics();
        Assert.True(stats.hits > 0); // Apache should have cache hits
    }

    [Fact]
    public async Task CacheInvalidation_ShouldWorkCorrectly()
    {
        // Arrange: Create and cache a service
        var service = new ServiceDefinition
        {
            Name = "InvalidationTestService",
            DisplayName = "Invalidation Test Service",
            ServiceType = ServiceType.MySQL, // Should be cached
            DefaultPort = 3306,
            DefaultPath = "/mysql",
            DefaultVersion = "8.0.35",
            IsInstalled = true,
            IsEnabled = true,
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "invalidation-test-001"
        };

        var createdService = await _repository.CreateAsync(service);
        
        // First access to populate cache
        var service1 = await _repository.GetByIdAsync(createdService.Id);
        Assert.NotNull(service1);

        // Act: Update the service (should invalidate cache)
        service1.DisplayName = "Updated Invalidation Test Service";
        var updateResult = await _repository.UpdateAsync(service1);
        Assert.True(updateResult);

        // Act: Access again (should be cache miss due to invalidation)
        var service2 = await _repository.GetByIdAsync(createdService.Id);
        Assert.NotNull(service2);
        Assert.Equal("Updated Invalidation Test Service", service2.DisplayName);

        // Assert: Cache should have been invalidated
        var stats = ServiceRepository.GetCacheStatistics();
        Assert.True(stats.misses > 0);
    }

    [Fact]
    public async Task GetServicesPaginatedAsync_ShouldReturnCorrectPage()
    {
        // Arrange: Create multiple services
        var services = new List<ServiceDefinition>();
        for (int i = 0; i < 10; i++)
        {
            var service = new ServiceDefinition
            {
                Name = $"PaginatedService{i:000}",
                DisplayName = $"Paginated Service {i:000}",
                ServiceType = ServiceType.Apache,
                DefaultPort = 8000 + i,
                DefaultPath = $"/paginated{i:000}",
                DefaultVersion = "1.0.0",
                Configuration = new Dictionary<string, object>(),
                Dependencies = new List<string>(),
                UniqueId = $"paginated-service-{i:000}"
            };
            
            var createdService = await _repository.CreateAsync(service);
            services.Add(createdService);
        }

        // Act: Get first page (limit 5, offset 0)
        var firstPage = await _repository.GetServicesPaginatedAsync(5, 0);
        
        // Act: Get second page (limit 5, offset 5)
        var secondPage = await _repository.GetServicesPaginatedAsync(5, 5);

        // Assert: Should return correct number of services
        Assert.Equal(5, firstPage.Count);
        Assert.Equal(5, secondPage.Count);
        
        // Assert: Pages should have different services
        var firstPageIds = firstPage.Select(s => s.Id).ToHashSet();
        var secondPageIds = secondPage.Select(s => s.Id).ToHashSet();
        Assert.False(firstPageIds.Intersect(secondPageIds).Any()); // No overlap
    }

    [Fact]
    public async Task GetMultipleServicesAsync_WithPagination_ShouldWorkCorrectly()
    {
        // Arrange: Create multiple services
        var serviceIds = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var service = new ServiceDefinition
            {
                Name = $"BatchService{i:000}",
                DisplayName = $"Batch Service {i:000}",
                ServiceType = ServiceType.MySQL,
                DefaultPort = 9000 + i,
                DefaultPath = $"/batch{i:000}",
                DefaultVersion = "1.0.0",
                Configuration = new Dictionary<string, object>(),
                Dependencies = new List<string>(),
                UniqueId = $"batch-service-{i:000}"
            };
            
            var createdService = await _repository.CreateAsync(service);
            serviceIds.Add(createdService.Id);
        }

        // Act: Get first 5 services
        var firstBatch = await _repository.GetMultipleServicesAsync(serviceIds, 5, 0);
        
        // Act: Get next 5 services
        var secondBatch = await _repository.GetMultipleServicesAsync(serviceIds, 5, 5);

        // Assert: Should return correct number of services
        Assert.Equal(5, firstBatch.Count);
        Assert.Equal(5, secondBatch.Count);
        
        // Assert: All returned services should be in the requested IDs
        var allReturnedIds = firstBatch.Concat(secondBatch).Select(s => s.Id).ToHashSet();
        Assert.True(allReturnedIds.IsSubsetOf(serviceIds.ToHashSet()));
    }

    [Fact]
    public async Task CacheStatistics_ShouldBeAccurate()
    {
        // Arrange: Reset statistics
        ServiceRepository.ResetCacheStatistics();
        
        var initialStats = ServiceRepository.GetCacheStatistics();
        Assert.Equal(0, initialStats.hits);
        Assert.Equal(0, initialStats.misses);
        Assert.Equal(0.0, initialStats.hitRate);

        // Act: Create and access services multiple times
        var service = new ServiceDefinition
        {
            Name = "StatsTestService",
            DisplayName = "Stats Test Service",
            ServiceType = ServiceType.Apache,
            DefaultPort = 8080,
            DefaultPath = "/statstest",
            DefaultVersion = "1.0.0",
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "stats-test-001"
        };

        var createdService = await _repository.CreateAsync(service);
        
        // First access (miss)
        await _repository.GetByIdAsync(createdService.Id);
        
        // Second access (hit)
        await _repository.GetByIdAsync(createdService.Id);
        
        // Third access (hit)
        await _repository.GetByIdAsync(createdService.Id);

        // Assert: Statistics should be accurate
        var finalStats = ServiceRepository.GetCacheStatistics();
        Assert.Equal(2, finalStats.hits);
        Assert.Equal(1, finalStats.misses);
        Assert.Equal(66.7, finalStats.hitRate, 1); // 2/3 = 66.7%
    }

    [Fact]
    public async Task ForceCacheCleanup_ShouldWork()
    {
        // Arrange: Create and cache a service
        var service = new ServiceDefinition
        {
            Name = "CleanupTestService",
            DisplayName = "Cleanup Test Service",
            ServiceType = ServiceType.MySQL,
            DefaultPort = 3306,
            DefaultPath = "/cleanuptest",
            DefaultVersion = "1.0.0",
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "cleanup-test-001"
        };

        var createdService = await _repository.CreateAsync(service);
        
        // Access to populate cache
        await _repository.GetByIdAsync(createdService.Id);

        // Act: Force cache cleanup
        ServiceRepository.ForceCacheCleanup();

        // Assert: Cache should be cleared
        var stats = ServiceRepository.GetCacheStatistics();
        Assert.Equal(0, stats.cacheSize); // Cache should be empty
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
