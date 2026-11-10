using System;
using System.Data.SQLite;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var connectionString = "Data Source=C:\\Users\\zouaz\\AppData\\Roaming\\ModStack\\Data\\modstack.db;Version=3;";
        
        using var connection = new SQLiteConnection(connectionString);
        await connection.OpenAsync();
        
        Console.WriteLine("=== PHP Extensions Check ===");
        
        // Check if PHPExtensions table exists
        var checkTableCmd = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='PHPExtensions';", connection);
        var tableExists = await checkTableCmd.ExecuteScalarAsync();
        
        if (tableExists == null)
        {
            Console.WriteLine("❌ PHPExtensions table does not exist!");
            return;
        }
        
        Console.WriteLine("✅ PHPExtensions table exists");
        
        // Count extensions
        var countCmd = new SQLiteCommand("SELECT COUNT(*) FROM PHPExtensions;", connection);
        var count = await countCmd.ExecuteScalarAsync();
        Console.WriteLine($"📊 Found {count} extensions in database");
        
        // List first 10 extensions
        var listCmd = new SQLiteCommand("SELECT Id, Name, Description FROM PHPExtensions LIMIT 10;", connection);
        using var reader = await listCmd.ExecuteReaderAsync();
        
        Console.WriteLine("\n=== First 10 Extensions ===");
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt64(0);
            var name = reader.GetString(1);
            var description = reader.IsDBNull(2) ? "No description" : reader.GetString(2);
            Console.WriteLine($"ID: {id}, Name: {name}, Description: {description}");
        }
        
        // Check for mysqli specifically
        var mysqliCmd = new SQLiteCommand("SELECT Id, Name FROM PHPExtensions WHERE Name = 'mysqli';", connection);
        using var mysqliReader = await mysqliCmd.ExecuteReaderAsync();
        
        Console.WriteLine("\n=== mysqli Extension ===");
        if (await mysqliReader.ReadAsync())
        {
            var id = mysqliReader.GetInt64(0);
            var name = mysqliReader.GetString(1);
            Console.WriteLine($"✅ Found mysqli: ID={id}, Name={name}");
        }
        else
        {
            Console.WriteLine("❌ mysqli extension not found in database");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}