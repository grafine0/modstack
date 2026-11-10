using ModStack.Data.Repositories;
using ModStack.Core.Models;
using ModStack.Common;
using Microsoft.Data.Sqlite;
using System.Data;
using Xunit;
using System.Diagnostics;
using ServiceType = ModStack.Core.Models.ServiceType;
using ServiceStatus = ModStack.Common.ServiceStatus;

namespace ModStack.Data.Tests.Performance;

/// <summary>
/// Performance tests for ServiceRepository to ensure efficient handling of large datasets
/// </summary>
public class ServiceRepositoryPerformanceTests : IDisposable
{
    private readonly ServiceRepository _repository;
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public ServiceRepositoryPerformanceTests()
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
    public async Task GetAllAsync_WithLargeDataset_ShouldCompleteWithinReasonableTime()
    {
        // Arrange: Insert 1000 services
        var servicesToCreate = new List<ServiceDefinition>();
        for (int i = 0; i < 1000; i++)
        {
            servicesToCreate.Add(new ServiceDefinition
            {
                Name = $"PerformanceService{i:0000}",
                DisplayName = $"Performance Service {i:0000}",
                ServiceType = (ServiceType)(i % 4 + 1), // Cycle through service types
                DefaultPort = 8000 + i,
                DefaultPath = $"/service{i:0000}",
                DefaultVersion = "1.0.0",
                IsInstalled = i % 2 == 0,
                IsEnabled = i % 3 == 0,
                Configuration = new Dictionary<string, object> 
                { 
                    { "index", i },
                    { "performance_test", true },
                    { "created_at", DateTime.UtcNow.ToString("O") }
                },
                Dependencies = new List<string> 
                { 
                    $"dep{i:0000}",
                    $"lib{i:0000}",
                    $"util{i:0000}"
                },
                UniqueId = $"perf-service-{i:0000}"
            });
        }

        // Insert all services
        foreach (var service in servicesToCreate)
        {
            await _repository.CreateAsync(service);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var services = await _repository.GetAllAsync();
        stopwatch.Stop();

        // Assert
        Assert.NotNull(services);
        Assert.True(services.Count >= 1000);
        Assert.True(stopwatch.ElapsedMilliseconds < 2000); // Should complete within 2 seconds
        
        Console.WriteLine($"Retrieved {services.Count} services in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetMultipleServicesAsync_WithLargeBatch_ShouldBeEfficient()
    {
        // Arrange: Insert 500 services
        var serviceIds = new List<long>();
        for (int i = 0; i < 500; i++)
        {
            var service = new ServiceDefinition
            {
                Name = $"BatchService{i:0000}",
                DisplayName = $"Batch Service {i:0000}",
                ServiceType = ServiceType.Apache,
                DefaultPort = 9000 + i,
                DefaultPath = $"/batch{i:0000}",
                DefaultVersion = "1.0.0",
                Configuration = new Dictionary<string, object> { { "batch_index", i } },
                Dependencies = new List<string> { $"batch_dep{i:0000}" },
                UniqueId = $"batch-service-{i:0000}"
            };
            
            var createdService = await _repository.CreateAsync(service);
            serviceIds.Add(createdService.Id);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var services = await _repository.GetMultipleServicesAsync(serviceIds);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(services);
        Assert.Equal(500, services.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000); // Should complete within 1 second
        
        Console.WriteLine($"Retrieved {services.Count} services in batch in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task CreateAsync_WithLargeBatch_ShouldBeEfficient()
    {
        // Arrange: Prepare 200 services for creation
        var servicesToCreate = new List<ServiceDefinition>();
        for (int i = 0; i < 200; i++)
        {
            servicesToCreate.Add(new ServiceDefinition
            {
                Name = $"CreateService{i:0000}",
                DisplayName = $"Create Service {i:0000}",
                ServiceType = ServiceType.MySQL,
                DefaultPort = 10000 + i,
                DefaultPath = $"/create{i:0000}",
                DefaultVersion = "1.0.0",
                Configuration = new Dictionary<string, object> 
                { 
                    { "create_index", i },
                    { "complex_config", new Dictionary<string, object> 
                        { 
                            { "nested_key", $"value_{i:0000}" },
                            { "nested_number", i * 10 }
                        }
                    }
                },
                Dependencies = new List<string> 
                { 
                    $"create_dep{i:0000}",
                    $"create_lib{i:0000}"
                },
                UniqueId = $"create-service-{i:0000}"
            });
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var createdServices = new List<ServiceDefinition>();
        
        foreach (var service in servicesToCreate)
        {
            var createdService = await _repository.CreateAsync(service);
            createdServices.Add(createdService);
        }
        
        stopwatch.Stop();

        // Assert
        Assert.Equal(200, createdServices.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 3000); // Should complete within 3 seconds
        
        Console.WriteLine($"Created {createdServices.Count} services in {stopwatch.ElapsedMilliseconds}ms");
        
        // Verify all services were created correctly
        foreach (var service in createdServices)
        {
            Assert.True(service.Id > 0);
            Assert.NotNull(service.Configuration);
            Assert.NotNull(service.Dependencies);
        }
    }

    [Fact]
    public async Task MemoryUsage_ShouldRemainStable_WithLargeOperations()
    {
        // Arrange: Get initial memory usage
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(false);
        Console.WriteLine($"Initial memory: {initialMemory / 1024 / 1024} MB");

        // Act: Perform large operations
        for (int batch = 0; batch < 5; batch++)
        {
            // Create 100 services
            for (int i = 0; i < 100; i++)
            {
                var service = new ServiceDefinition
                {
                    Name = $"MemoryService{batch:000}_{i:0000}",
                    DisplayName = $"Memory Service {batch:000} {i:0000}",
                    ServiceType = ServiceType.PHP,
                    DefaultPort = 11000 + (batch * 100) + i,
                    DefaultPath = $"/memory{batch:000}_{i:0000}",
                    DefaultVersion = "1.0.0",
                    Configuration = new Dictionary<string, object> 
                    { 
                        { "batch", batch },
                        { "index", i },
                        { "large_data", new string('x', 1000) } // 1KB of data per service
                    },
                    Dependencies = new List<string> 
                    { 
                        $"memory_dep{batch:000}_{i:0000}",
                        $"memory_lib{batch:000}_{i:0000}"
                    },
                    UniqueId = $"memory-service-{batch:000}-{i:0000}"
                };
                
                await _repository.CreateAsync(service);
            }
            
            // Retrieve all services
            var services = await _repository.GetAllAsync();
            
            // Check memory usage
            var currentMemory = GC.GetTotalMemory(false);
            Console.WriteLine($"Batch {batch}: Memory usage: {currentMemory / 1024 / 1024} MB, Services: {services.Count}");
        }

        // Assert: Memory should not grow excessively
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseMB = memoryIncrease / 1024 / 1024;
        
        Console.WriteLine($"Final memory: {finalMemory / 1024 / 1024} MB");
        Console.WriteLine($"Memory increase: {memoryIncreaseMB} MB");
        
        // Memory increase should be reasonable (less than 50MB for 500 services with 1KB each)
        Assert.True(memoryIncreaseMB < 50);
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldMaintainDataIntegrity()
    {
        // Arrange: Prepare concurrent operations
        var tasks = new List<Task>();
        var createdServiceIds = new List<long>();
        var lockObject = new object();

        // Act: Run 50 concurrent create operations
        for (int i = 0; i < 50; i++)
        {
            var index = i; // Capture loop variable
            tasks.Add(Task.Run(async () =>
            {
                var service = new ServiceDefinition
                {
                    Name = $"ConcurrentService{index:0000}",
                    DisplayName = $"Concurrent Service {index:0000}",
                    ServiceType = ServiceType.Apache,
                    DefaultPort = 12000 + index,
                    DefaultPath = $"/concurrent{index:0000}",
                    DefaultVersion = "1.0.0",
                    Configuration = new Dictionary<string, object> { { "concurrent_index", index } },
                    Dependencies = new List<string> { $"concurrent_dep{index:0000}" },
                    UniqueId = $"concurrent-service-{index:0000}"
                };
                
                var createdService = await _repository.CreateAsync(service);
                
                lock (lockObject)
                {
                    createdServiceIds.Add(createdService.Id);
                }
            }));
        }

        // Wait for all operations to complete
        await Task.WhenAll(tasks);

        // Assert: All services should be created with unique IDs
        Assert.Equal(50, createdServiceIds.Count);
        Assert.Equal(50, createdServiceIds.Distinct().Count()); // All IDs should be unique

        // Verify all services exist
        var allServices = await _repository.GetAllAsync();
        var concurrentServices = allServices.Where(s => s.Name.StartsWith("ConcurrentService")).ToList();
        Assert.Equal(50, concurrentServices.Count);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
