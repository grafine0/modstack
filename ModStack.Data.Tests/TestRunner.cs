using ModStack.Data.Repositories;
using ModStack.Core.Models;
using ModStack.Common;
using Microsoft.Data.Sqlite;
using System.Data;
using Xunit;
using ServiceType = ModStack.Core.Models.ServiceType;
using ServiceStatus = ModStack.Common.ServiceStatus;

namespace ModStack.Data.Tests;

/// <summary>
/// Test runner for running all ServiceRepository tests
/// </summary>
public class TestRunner
{
    [Fact]
    public async Task RunAllServiceRepositoryTests()
    {
        Console.WriteLine("🧪 Starting Comprehensive ServiceRepository Test Suite...");
        
        // Test basic CRUD operations
        await TestBasicCrudOperations();
        
        // Test NULL handling
        await TestNullHandling();
        
        // Test caching
        await TestCaching();
        
        // Test transactions
        await TestTransactions();
        
        // Test type handlers
        await TestTypeHandlers();
        
        // Test performance scenarios
        await TestPerformanceScenarios();
        
        // Test edge cases
        await TestEdgeCases();
        
        Console.WriteLine("✅ All ServiceRepository tests completed successfully!");
    }

    private async Task TestBasicCrudOperations()
    {
        Console.WriteLine("📝 Testing basic CRUD operations...");
        
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        
        var repository = new ServiceRepository("Data Source=:memory:");
        await InitializeTestDatabase(connection);
        
        // Test Create
        var service = new ServiceDefinition
        {
            Name = "TestService",
            DisplayName = "Test Service",
            ServiceType = ServiceType.Apache,
            DefaultPort = 8080,
            DefaultPath = "/test",
            DefaultVersion = "1.0.0",
            IsInstalled = true,
            IsEnabled = true,
            Status = ServiceStatus.Running,
            AutoStart = true,
            Configuration = new Dictionary<string, object> { { "port", 8080 } },
            Dependencies = new List<string> { "libssl" },
            UniqueId = "test-001"
        };

        var createdService = await repository.CreateAsync(service);
        Assert.NotNull(createdService);
        Assert.True(createdService.Id > 0);
        
        // Test Read
        var retrievedService = await repository.GetByIdAsync(createdService.Id);
        Assert.NotNull(retrievedService);
        Assert.Equal(service.Name, retrievedService.Name);
        
        // Test Update
        retrievedService.DisplayName = "Updated Test Service";
        var updateResult = await repository.UpdateAsync(retrievedService);
        Assert.True(updateResult);
        
        // Test Delete
        var deleteResult = await repository.DeleteAsync(createdService.Id);
        Assert.True(deleteResult);
        
        var deletedService = await repository.GetByIdAsync(createdService.Id);
        Assert.Null(deletedService);
        
        Console.WriteLine("✅ Basic CRUD operations test passed");
    }

    private async Task TestNullHandling()
    {
        Console.WriteLine("🛡️ Testing NULL handling...");
        
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        
        var repository = new ServiceRepository("Data Source=:memory:");
        await InitializeTestDatabase(connection);
        
        // Test service with null Configuration and Dependencies
        var service = new ServiceDefinition
        {
            Name = "NullTestService",
            DisplayName = "NULL Test Service",
            ServiceType = ServiceType.Apache,
            DefaultPort = 8080,
            DefaultPath = "/nulltest",
            DefaultVersion = "1.0.0",
            Configuration = null,
            Dependencies = null
        };

        var createdService = await repository.CreateAsync(service);
        Assert.NotNull(createdService);
        Assert.NotNull(createdService.Configuration);
        Assert.NotNull(createdService.Dependencies);
        Assert.Empty(createdService.Configuration);
        Assert.Empty(createdService.Dependencies);
        
        Console.WriteLine("✅ NULL handling test passed");
    }

    private async Task TestCaching()
    {
        Console.WriteLine("💾 Testing caching functionality...");
        
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        
        var repository = new ServiceRepository("Data Source=:memory:");
        await InitializeTestDatabase(connection);
        
        // Create a service
        var service = new ServiceDefinition
        {
            Name = "CacheTestService",
            DisplayName = "Cache Test Service",
            ServiceType = ServiceType.Apache,
            DefaultPort = 8080,
            DefaultPath = "/cachetest",
            DefaultVersion = "1.0.0",
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "cache-001"
        };

        var createdService = await repository.CreateAsync(service);
        Assert.NotNull(createdService);
        
        // First call should hit database
        var service1 = await repository.GetByIdAsync(createdService.Id);
        Assert.NotNull(service1);
        
        // Second call should hit cache
        var service2 = await repository.GetByIdAsync(createdService.Id);
        Assert.NotNull(service2);
        Assert.Same(service1, service2);
        
        Console.WriteLine("✅ Caching functionality test passed");
    }

    private async Task TestTransactions()
    {
        Console.WriteLine("🔄 Testing transaction handling...");
        
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        
        var repository = new ServiceRepository("Data Source=:memory:");
        await InitializeTestDatabase(connection);
        
        // Test successful transaction
        var service = new ServiceDefinition
        {
            Name = "TransactionTestService",
            DisplayName = "Transaction Test Service",
            ServiceType = ServiceType.Apache,
            DefaultPort = 8080,
            DefaultPath = "/transactiontest",
            DefaultVersion = "1.0.0",
            Configuration = new Dictionary<string, object>(),
            Dependencies = new List<string>(),
            UniqueId = "transaction-001"
        };

        var createdService = await repository.CreateAsync(service);
        Assert.NotNull(createdService);
        
        // Verify service was committed
        var retrievedService = await repository.GetByIdAsync(createdService.Id);
        Assert.NotNull(retrievedService);
        Assert.Equal(service.Name, retrievedService.Name);
        
        Console.WriteLine("✅ Transaction handling test passed");
    }

    private async Task TestTypeHandlers()
    {
        Console.WriteLine("🔧 Testing type handlers...");
        
        // Test ConfigurationTypeHandler
        var configHandler = new ConfigurationTypeHandler();
        var config = new Dictionary<string, object> { { "port", 8080 }, { "host", "localhost" } };
        
        var parameter = new SqliteParameter();
        configHandler.SetValue(parameter, config);
        Assert.NotNull(parameter.Value);
        
        var parsedConfig = configHandler.Parse(parameter.Value.ToString());
        Assert.NotNull(parsedConfig);
        Assert.Equal(8080, parsedConfig["port"]);
        Assert.Equal("localhost", parsedConfig["host"]);
        
        // Test DependenciesTypeHandler
        var depsHandler = new DependenciesTypeHandler();
        var dependencies = new List<string> { "libssl", "libcrypto" };
        
        var depsParameter = new SqliteParameter();
        depsHandler.SetValue(depsParameter, dependencies);
        Assert.NotNull(depsParameter.Value);
        
        var parsedDeps = depsHandler.Parse(depsParameter.Value.ToString());
        Assert.NotNull(parsedDeps);
        Assert.Equal(2, parsedDeps.Count);
        Assert.Contains("libssl", parsedDeps);
        Assert.Contains("libcrypto", parsedDeps);
        
        Console.WriteLine("✅ Type handlers test passed");
    }

    private async Task TestPerformanceScenarios()
    {
        Console.WriteLine("⚡ Testing performance scenarios...");
        
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        
        var repository = new ServiceRepository("Data Source=:memory:");
        await InitializeTestDatabase(connection);
        
        // Test large dataset performance
        var startTime = DateTime.UtcNow;
        
        // Create 50 services quickly
        for (int i = 0; i < 50; i++)
        {
            var service = new ServiceDefinition
            {
                Name = $"PerfService{i:0000}",
                DisplayName = $"Performance Service {i:0000}",
                ServiceType = ServiceType.Apache,
                DefaultPort = 13000 + i,
                DefaultPath = $"/perf{i:0000}",
                DefaultVersion = "1.0.0",
                Configuration = new Dictionary<string, object> { { "perf_index", i } },
                Dependencies = new List<string> { $"perf_dep{i:0000}" },
                UniqueId = $"perf-service-{i:0000}"
            };
            
            await repository.CreateAsync(service);
        }
        
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;
        
        Console.WriteLine($"Created 50 services in {duration.TotalMilliseconds}ms");
        Assert.True(duration.TotalMilliseconds < 5000); // Should complete within 5 seconds
        
        // Test retrieval performance
        startTime = DateTime.UtcNow;
        var services = await repository.GetAllAsync();
        endTime = DateTime.UtcNow;
        duration = endTime - startTime;
        
        Console.WriteLine($"Retrieved {services.Count} services in {duration.TotalMilliseconds}ms");
        Assert.True(duration.TotalMilliseconds < 1000); // Should complete within 1 second
        
        Console.WriteLine("✅ Performance scenarios test passed");
    }

    private async Task TestEdgeCases()
    {
        Console.WriteLine("🔍 Testing edge cases...");
        
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        
        var repository = new ServiceRepository("Data Source=:memory:");
        await InitializeTestDatabase(connection);
        
        // Test with very long strings
        var longName = new string('A', 1000);
        var longDisplayName = new string('B', 1000);
        var longPath = new string('C', 1000);
        
        var serviceWithLongStrings = new ServiceDefinition
        {
            Name = longName,
            DisplayName = longDisplayName,
            ServiceType = ServiceType.Apache,
            DefaultPort = 8080,
            DefaultPath = longPath,
            DefaultVersion = "1.0.0",
            Configuration = new Dictionary<string, object> { { "long_key", new string('D', 500) } },
            Dependencies = new List<string> { new string('E', 200) },
            UniqueId = "long-strings-test"
        };
        
        var createdService = await repository.CreateAsync(serviceWithLongStrings);
        Assert.NotNull(createdService);
        Assert.Equal(longName, createdService.Name);
        Assert.Equal(longDisplayName, createdService.DisplayName);
        
        // Test with special characters
        var specialService = new ServiceDefinition
        {
            Name = "SpecialChars!@#$%^&*()",
            DisplayName = "Special Chars: <>?[]{}|\\",
            ServiceType = ServiceType.MySQL,
            DefaultPort = 3306,
            DefaultPath = "/path/with/special/chars",
            DefaultVersion = "1.0.0",
            Configuration = new Dictionary<string, object> 
            { 
                { "special_key", "Value with \"quotes\" and 'apostrophes'" },
                { "unicode_key", "测试中文" },
                { "emoji_key", "🚀🔥💻" }
            },
            Dependencies = new List<string> 
            { 
                "lib-with-special-chars",
                "dependency@version",
                "package>=1.0.0"
            },
            UniqueId = "special-chars-test"
        };
        
        var createdSpecialService = await repository.CreateAsync(specialService);
        Assert.NotNull(createdSpecialService);
        Assert.Equal("SpecialChars!@#$%^&*()", createdSpecialService.Name);
        Assert.Contains("测试中文", createdSpecialService.Configuration.Values);
        Assert.Contains("🚀🔥💻", createdSpecialService.Configuration.Values);
        
        Console.WriteLine("✅ Edge cases test passed");
    }

    private async Task InitializeTestDatabase(SqliteConnection connection)
    {
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS Services (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                ServiceType INTEGER NOT NULL,
                DefaultPort INTEGER NOT NULL,
                DefaultVersion TEXT NOT NULL,
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

        using var command = new SqliteCommand(createTableSql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
