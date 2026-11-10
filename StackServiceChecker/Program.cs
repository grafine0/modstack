using Microsoft.Data.Sqlite;

class Program
{
    static async Task Main(string[] args)
    {
        var connectionString = @"Data Source=C:\Users\zouaz\AppData\Roaming\ModStack\Data\modstack.db";
        
        try
        {
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            
            Console.WriteLine("🔍 Checking Service Installation Status...\n");
            
            // Check Services IsInstalled status
            Console.WriteLine("=== Services Table ===");
            var serviceCommand = connection.CreateCommand();
            serviceCommand.CommandText = @"
                SELECT Id, Name, DisplayName, IsInstalled, BinaryPath 
                FROM Services 
                ORDER BY Id";
            
            var serviceReader = await serviceCommand.ExecuteReaderAsync();
            Console.WriteLine("ID | Name      | DisplayName | IsInstalled | BinaryPath");
            Console.WriteLine("---|-----------|-------------|-------------|------------------");
            
            while (await serviceReader.ReadAsync())
            {
                var id = serviceReader.GetInt32(0);
                var name = serviceReader.GetString(1);
                var displayName = serviceReader.IsDBNull(2) ? "NULL" : serviceReader.GetString(2);
                var isInstalled = serviceReader.GetBoolean(3);
                var binaryPath = serviceReader.IsDBNull(4) ? "NULL" : serviceReader.GetString(4);
                Console.WriteLine($"{id,2} | {name,-9} | {displayName,-11} | {isInstalled,-11} | {binaryPath}");
            }
            serviceReader.Close();
            
            Console.WriteLine("\n=== StackServices with Service Details ===");
            var stackServiceCommand = connection.CreateCommand();
            stackServiceCommand.CommandText = @"
                SELECT ss.Id, ss.StackId, ss.ServiceId, ss.IsEnabled,
                       srv.Name as ServiceName, srv.IsInstalled as ServiceIsInstalled,
                       s.Name as StackName
                FROM StackServices ss
                JOIN Services srv ON ss.ServiceId = srv.Id
                JOIN Stacks s ON ss.StackId = s.Id
                ORDER BY ss.StackId, ss.ServiceId";
            
            var stackReader = await stackServiceCommand.ExecuteReaderAsync();
            Console.WriteLine("SS_ID | StackId | ServiceId | IsEnabled | ServiceName | ServiceInstalled | StackName");
            Console.WriteLine("------|---------|-----------|-----------|-------------|------------------|----------");
            
            while (await stackReader.ReadAsync())
            {
                var ssId = stackReader.GetInt32(0);
                var stackId = stackReader.GetInt32(1);
                var serviceId = stackReader.GetInt32(2);
                var isEnabled = stackReader.GetBoolean(3);
                var serviceName = stackReader.GetString(4);
                var serviceInstalled = stackReader.GetBoolean(5);
                var stackName = stackReader.GetString(6);
                
                Console.WriteLine($"{ssId,5} | {stackId,7} | {serviceId,9} | {isEnabled,9} | {serviceName,-11} | {serviceInstalled,-16} | {stackName}");
            }
            stackReader.Close();
            
            Console.WriteLine("\n🔍 Analysis:");
            Console.WriteLine("If ServiceInstalled shows 'False', that's why stack start is failing!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}