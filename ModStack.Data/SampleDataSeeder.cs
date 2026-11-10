using Microsoft.Extensions.Logging;
using ModStack.Common;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;

namespace ModStack.Data;

/// <summary>
/// Seeds the database with sample data for testing
/// </summary>
public class SampleDataSeeder(
    IServiceRepository serviceRepository,
    IStackRepository stackRepository,
    IStackServiceRepository stackServiceRepository,
    IProjectRepository projectRepository,
    IVirtualHostRepository virtualHostRepository,
    ILogger<SampleDataSeeder>? logger = null)
{
    private readonly IServiceRepository _serviceRepository = serviceRepository;
    private readonly IStackRepository _stackRepository = stackRepository;
    private readonly IStackServiceRepository _stackServiceRepository = stackServiceRepository;
    private readonly IProjectRepository _projectRepository = projectRepository;
    private readonly IVirtualHostRepository _virtualHostRepository = virtualHostRepository;
    private readonly ILogger<SampleDataSeeder>? _logger = logger;

    public async Task SeedAsync()
    {
        try
        {
            _logger?.LogInformation("Seeding sample data...");

            await SeedSampleServicesAsync();
            await SeedSampleStacksAsync();

            _logger?.LogInformation("Sample data seeded successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to seed sample data");
            throw;
        }
    }

    private async Task SeedSampleServicesAsync()
    {
        var sampleServices = new[]
        {
            new ServiceDefinition
            {
                Name = "apache",
                DisplayName = "Apache HTTP Server",
                ServiceType = ServiceType.Apache,
                Version = "2.4.58",
                BinaryPath = @"C:\Apache24",
                DefaultPort = 80,
                IsInstalled = false,
                IsEnabled = true
            },
            new ServiceDefinition
            {
                Name = "nginx",
                DisplayName = "Nginx Web Server",
                ServiceType = ServiceType.Nginx,
                Version = "1.24.0",
                BinaryPath = @"C:\nginx",
                DefaultPort = 80,
                IsInstalled = false,
                IsEnabled = true
            },
            new ServiceDefinition
            {
                Name = "php",
                DisplayName = "PHP FastCGI Process Manager",
                ServiceType = ServiceType.PHP,
                Version = "8.3.0",
                BinaryPath = @"C:\PHP",
                DefaultPort = 9000,
                IsInstalled = false,
                IsEnabled = true
            },
            new ServiceDefinition
            {
                Name = "php",
                DisplayName = "PHP FastCGI Process Manager",
                ServiceType = ServiceType.PHP,
                Version = "8.2.0",
                BinaryPath = @"C:\PHP82",
                DefaultPort = 9001,
                IsInstalled = false,
                IsEnabled = true
            },
            new ServiceDefinition
            {
                Name = "mysql",
                DisplayName = "MySQL Database Server",
                ServiceType = ServiceType.MySQL,
                Version = "8.0.35",
                BinaryPath = @"C:\MySQL",
                DefaultPort = 3306,
                IsInstalled = false,
                IsEnabled = true
            },
            new ServiceDefinition
            {
                Name = "mariadb",
                DisplayName = "MariaDB Database Server",
                ServiceType = ServiceType.MariaDB,
                Version = "11.1.2",
                BinaryPath = @"C:\MariaDB",
                DefaultPort = 3307,
                IsInstalled = false,
                IsEnabled = true
            },
            new ServiceDefinition
            {
                Name = "postgresql",
                DisplayName = "PostgreSQL Database Server",
                ServiceType = ServiceType.PostgreSQL,
                Version = "16.0",
                BinaryPath = @"C:\PostgreSQL",
                DefaultPort = 5432,
                IsInstalled = false,
                IsEnabled = true
            },
            new ServiceDefinition
            {
                Name = "redis",
                DisplayName = "Redis In-Memory Database",
                ServiceType = ServiceType.Redis,
                Version = "7.2.3",
                BinaryPath = @"C:\Redis",
                DefaultPort = 6379,
                IsInstalled = false,
                IsEnabled = true
            }
        };

        foreach (var service in sampleServices)
        {
            if (string.IsNullOrEmpty(service.Version))
            {
                _logger?.LogWarning("Skipping service {ServiceName} - Version is null or empty", service.Name);
                continue;
            }
            
            var existing = await _serviceRepository.GetByNameAndVersionAsync(service.Name, service.Version);
            if (existing == null)
            {
                await _serviceRepository.CreateAsync(service);
                _logger?.LogInformation("Created sample service: {ServiceName} v{Version}", service.Name, service.Version);
            }
        }
    }

    private async Task SeedSampleStacksAsync()
    {
        var sampleStacks = new[]
        {
            new { Name = "lamp-dev", DisplayName = "LAMP Development Stack", Description = "Apache + PHP 8.3 + MySQL for development", Services = new[] { ("apache", "2.4.58", 80, 3), ("php", "8.3.0", 9000, 2), ("mysql", "8.0.35", 3306, 1) } },
            new { Name = "lamp-prod", DisplayName = "LAMP Production Stack", Description = "Apache + PHP 8.2 + MySQL for production", Services = new[] { ("apache", "2.4.58", 8080, 3), ("php", "8.2.0", 9001, 2), ("mysql", "8.0.35", 3307, 1) } },
            new { Name = "nginx-php", DisplayName = "Nginx + PHP Stack", Description = "Nginx + PHP + Redis for high performance", Services = new[] { ("nginx", "1.24.0", 8081, 3), ("php", "8.3.0", 9002, 2), ("redis", "7.2.3", 6379, 1) } },
            new { Name = "database-only", DisplayName = "Database Services", Description = "Multiple database servers", Services = new[] { ("mysql", "8.0.35", 3308, 1), ("postgresql", "16.0", 5432, 2), ("redis", "7.2.3", 6380, 3) } }
        };

        foreach (var stackData in sampleStacks)
        {
            var existing = await _stackRepository.GetByNameAsync(stackData.Name);
            if (existing == null)
            {
                // Create stack
                var stack = new Stack
                {
                    Name = stackData.Name,
                    DisplayName = stackData.DisplayName,
                    Description = stackData.Description,
                    IsActive = false,
                    IsDefault = stackData.Name == "lamp-dev"
                };

                var createdStack = await _stackRepository.CreateAsync(stack);
                _logger?.LogInformation("Created sample stack: {StackName}", stackData.Name);

                // Add services to stack
                foreach (var (serviceName, version, port, priority) in stackData.Services)
                {
                    var service = await _serviceRepository.GetByNameAndVersionAsync(serviceName, version);
                    if (service != null)
                    {
                        var stackService = new StackService
                        {
                            StackId = createdStack.Id,
                            ServiceId = service.Id,
                            Port = port,
                            Priority = priority,
                            IsEnabled = true,
                            Service = service
                        };

                        await _stackServiceRepository.CreateAsync(stackService);
                        _logger?.LogInformation("Added service {ServiceName} to stack {StackName}", serviceName, stackData.Name);
                    }
                }
            }
        }
    }
}
