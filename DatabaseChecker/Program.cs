using System;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;
using Dapper;
using DatabaseChecker;

class Program
{
    static async Task Main()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dbPath = Path.Combine(appDataPath, "ModStack", "Data", "modstack.db");
            var connectionString = $"Data Source={dbPath};";
            
            Console.WriteLine("=== ModStack Database Package Checker ===");
            Console.WriteLine("This tool will check and initialize packages in the database.");
            Console.WriteLine($"Database path: {dbPath}");
            Console.WriteLine();
            
            // CheckPackages removed - no longer needed
            
            // InspectDatabase removed - no longer needed

            // NULL ordinal tester removed - no longer needed after Services table migration
            
            Console.WriteLine("\n=== Database Status Check ===");
            
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            
            Console.WriteLine($"Connection State: {connection.State}");
            
            // Check Services table
            var servicesCount = connection.QuerySingle<long>("SELECT COUNT(*) FROM Services");
            Console.WriteLine($"✅ Services table: {servicesCount} records");
            
            // Check PHPExtensions table
            var phpExtensionsCount = connection.QuerySingle<long>("SELECT COUNT(*) FROM PHPExtensions");
            Console.WriteLine($"✅ PHPExtensions table: {phpExtensionsCount} records");
            
            if (phpExtensionsCount > 0)
            {
                var phpExtensions = connection.Query("SELECT Id, Name, Description FROM PHPExtensions LIMIT 10");
                Console.WriteLine("\n=== PHP Extensions ===");
                foreach (var ext in phpExtensions)
                {
                    Console.WriteLine($"ID: {ext.Id}, Name: {ext.Name}, Description: {ext.Description ?? "No description"}");
                }
                
                // Check for mysqli specifically
                var mysqli = connection.QueryFirstOrDefault("SELECT Id, Name FROM PHPExtensions WHERE Name = 'mysqli'");
                if (mysqli != null)
                {
                    Console.WriteLine($"✅ Found mysqli: ID={mysqli.Id}, Name={mysqli.Name}");
                }
                else
                {
                    Console.WriteLine("❌ mysqli extension not found in database");
                }
            }
            
            if (servicesCount > 0)
            {
                var services = connection.Query("SELECT Id, Name, DisplayName, ServiceType, DefaultPort FROM Services");
                Console.WriteLine("\n=== Services ===");
                foreach (var service in services)
                {
                    Console.WriteLine($"ID: {service.Id}, Name: {service.Name}, Display: {service.DisplayName}, Type: {service.ServiceType}, Port: {service.DefaultPort}");
                }
            }
            
            // Check if VirtualHosts table exists
            var tableExists = connection.QuerySingle<long>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VirtualHosts'");
            Console.WriteLine($"\nVirtualHosts table exists: {tableExists > 0}");
            
            if (tableExists > 0)
            {
                // Count virtual hosts
                var count = connection.QuerySingle<long>("SELECT COUNT(*) FROM VirtualHosts");
                Console.WriteLine($"Total Virtual Hosts: {count}");
                
                if (count > 0)
                {
                    // Get all virtual hosts
                    var virtualHosts = connection.Query(
                        "SELECT Id, ServerName, Port, DocumentRoot, IsEnabled, CreatedAt FROM VirtualHosts ORDER BY Id");
                    
                    Console.WriteLine("\n=== Virtual Hosts in Database ===");
                    foreach (var vh in virtualHosts)
                    {
                        Console.WriteLine($"ID: {vh.Id}, Server: {vh.ServerName}, Port: {vh.Port}, Root: {vh.DocumentRoot}, Enabled: {vh.IsEnabled}, Created: {vh.CreatedAt}");
                    }
                }
            }
            
            // Check Services table (legacy)
            var servicesExists = connection.QuerySingle<long>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Services'");
            Console.WriteLine($"\nLegacy Services table exists: {servicesExists > 0}");
            
            if (servicesExists > 0)
            {
                var servicesCount = connection.QuerySingle<long>("SELECT COUNT(*) FROM Services");
                Console.WriteLine($"Total Services (legacy): {servicesCount}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
