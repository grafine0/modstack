using Dapper;
using Microsoft.Data.Sqlite;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using System.Data;
using System.Text.Json;

namespace ModStack.Data.Repositories;

/// <summary>
/// Repository implementation for virtual host data access
/// </summary>
public class VirtualHostRepository : IVirtualHostRepository
{
    private readonly string _connectionString;

    public VirtualHostRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<VirtualHost>> GetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        // First, let's try a simple query to see if the table exists and has data
            var simpleSql = "SELECT COUNT(*) FROM VirtualHosts";
            var count = await connection.QuerySingleAsync<long>(simpleSql);
            // Note: Using Console.WriteLine here since this is a data layer and doesn't have access to LogService
            Console.WriteLine($"VirtualHosts table has {count} records");
        
        if (count == 0)
        {
            Console.WriteLine("No virtual hosts found in database");
            return new List<VirtualHost>();
        }
        
        // Simple approach: Load virtual hosts first, then load related data separately
        var virtualHostSql = @"
            SELECT Id, ProjectId, ServiceId, ServerName, ServerAlias,
                   DocumentRoot, Port, IsSSL, SSLCertPath, SSLKeyPath,
                   IsEnabled, CreatedAt, UpdatedAt, Configuration
            FROM VirtualHosts 
            ORDER BY ServerName";
        
        try
        {
            var virtualHostsData = await connection.QueryAsync<VirtualHostData>(virtualHostSql);
            var virtualHosts = new List<VirtualHost>();
            
            foreach (var vhData in virtualHostsData)
            {
                var virtualHost = ConvertFromData(vhData);
                
                // Load related project if exists
                if (vhData.ProjectId.HasValue)
                {
                    var projectSql = @"
                        SELECT p.Id, p.Name, p.DisplayName, p.Path, p.Domain, p.StackId, 
                               p.DocumentRoot, p.IsActive, p.CreatedAt, p.UpdatedAt, p.Configuration,
                               s.Id, s.Name, s.DisplayName, s.Description, s.IsActive, s.IsDefault, 
                               COALESCE(s.Configuration, '{}') as Configuration, s.CreatedAt, s.UpdatedAt
                        FROM Projects p
                        LEFT JOIN Stacks s ON p.StackId = s.Id
                        WHERE p.Id = @ProjectId";
                    
                    var projectResult = await connection.QueryAsync<ProjectData, Stack, Project>(
                        projectSql,
                        (projectData, stack) =>
                        {
                            var project = ConvertProjectFromData(projectData);
                            project.Stack = stack;
                            return project;
                        },
                        new { ProjectId = vhData.ProjectId },
                        splitOn: "Id");
                    
                    virtualHost.Project = projectResult.FirstOrDefault();
                }
                
                // Load related service
                var serviceSql = @"
                    SELECT Id, Name, DisplayName, ServiceType, DefaultPort, BinaryPath as DefaultPath, Version as DefaultVersion,
                           IsInstalled, IsEnabled, InstallDate, CreatedAt, UpdatedAt
                    FROM Services 
                    WHERE Id = @ServiceId";
                
                var service = await connection.QuerySingleOrDefaultAsync<ServiceDefinition>(serviceSql, new { ServiceId = vhData.ServiceId });
                virtualHost.Service = service;
                
                virtualHosts.Add(virtualHost);
            }
            
            Console.WriteLine($"Retrieved {virtualHosts.Count} virtual hosts from database");
            return virtualHosts;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetAllAsync: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Fallback: Load virtual hosts and then load related data separately
            var fallbackSql = "SELECT * FROM VirtualHosts ORDER BY ServerName";
            var fallbackResults = await connection.QueryAsync<VirtualHostData>(fallbackSql);
            var fallbackVirtualHosts = new List<VirtualHost>();
            
            foreach (var data in fallbackResults)
            {
                var virtualHost = ConvertFromData(data);
                
                // Load related project if exists
                if (data.ProjectId.HasValue)
                {
                    var projectSql = "SELECT * FROM Projects WHERE Id = @ProjectId";
                    var project = await connection.QuerySingleOrDefaultAsync<Project>(projectSql, new { ProjectId = data.ProjectId });
                    virtualHost.Project = project;
                }
                
                // Load related service
                var serviceSql = "SELECT * FROM Services WHERE Id = @ServiceId";
                var service = await connection.QuerySingleOrDefaultAsync<ServiceDefinition>(serviceSql, new { ServiceId = data.ServiceId });
                virtualHost.Service = service;
                
                fallbackVirtualHosts.Add(virtualHost);
            }
            
            Console.WriteLine($"Fallback query returned {fallbackVirtualHosts.Count} virtual hosts");
            return fallbackVirtualHosts;
        }
    }

    public async Task<VirtualHost?> GetByIdAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT vh.Id, vh.ProjectId, vh.ServiceId, vh.ServerName, vh.ServerAlias,
                   vh.DocumentRoot, vh.Port, vh.IsSSL, vh.SSLCertPath, vh.SSLKeyPath,
                   vh.IsEnabled, vh.CreatedAt, vh.UpdatedAt, vh.Configuration,
                   p.Id, p.Name, p.DisplayName, p.Path, p.Domain, p.StackId,
                   s.Id, s.Name, s.DisplayName, s.Version, s.ServiceType, s.DefaultPort
            FROM VirtualHosts vh
            LEFT JOIN Projects p ON vh.ProjectId = p.Id
            LEFT JOIN Services s ON vh.ServiceId = s.Id
            WHERE vh.Id = @Id";
        
        var result = await connection.QueryAsync<VirtualHostData, Project, ServiceDefinition, VirtualHost>(
            sql,
            (vhostData, project, service) =>
            {
                var vhost = ConvertFromData(vhostData);
                vhost.Project = project;
                vhost.Service = service;
                return vhost;
            },
            new { Id = id },
            splitOn: "Id,Id");
        
        return result.FirstOrDefault();
    }

    public async Task<List<VirtualHost>> GetByProjectIdAsync(long projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT vh.Id, vh.ProjectId, vh.ServiceId, vh.ServerName, vh.ServerAlias,
                   vh.DocumentRoot, vh.Port, vh.IsSSL, vh.SSLCertPath, vh.SSLKeyPath,
                   vh.IsEnabled, vh.CreatedAt, vh.UpdatedAt, vh.Configuration,
                   s.Id, s.Name, s.DisplayName, s.Version, s.ServiceType, s.DefaultPort
            FROM VirtualHosts vh
            LEFT JOIN Services s ON vh.ServiceId = s.Id
            WHERE vh.ProjectId = @ProjectId
            ORDER BY vh.ServerName";
        
        var results = await connection.QueryAsync<VirtualHostData, ServiceDefinition, VirtualHost>(
            sql,
            (vhostData, service) =>
            {
                var vhost = ConvertFromData(vhostData);
                vhost.Service = service;
                return vhost;
            },
            new { ProjectId = projectId },
            splitOn: "Id");
        
        return results.ToList();
    }

    public async Task<List<VirtualHost>> GetByServiceIdAsync(long serviceId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT vh.Id, vh.ProjectId, vh.ServiceId, vh.ServerName, vh.ServerAlias,
                   vh.DocumentRoot, vh.Port, vh.IsSSL, vh.SSLCertPath, vh.SSLKeyPath,
                   vh.IsEnabled, vh.CreatedAt, vh.UpdatedAt, vh.Configuration,
                   p.Id, p.Name, p.DisplayName, p.Path, p.Domain, p.StackId
            FROM VirtualHosts vh
            LEFT JOIN Projects p ON vh.ProjectId = p.Id
            WHERE vh.ServiceId = @ServiceId
            ORDER BY vh.ServerName";
        
        var results = await connection.QueryAsync<VirtualHostData, Project, VirtualHost>(
            sql,
            (vhostData, project) =>
            {
                var vhost = ConvertFromData(vhostData);
                vhost.Project = project;
                return vhost;
            },
            new { ServiceId = serviceId },
            splitOn: "Id");
        
        return results.ToList();
    }

    public async Task<VirtualHost?> GetByServerNameAsync(string serverName)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT vh.Id, vh.ProjectId, vh.ServiceId, vh.ServerName, vh.ServerAlias,
                   vh.DocumentRoot, vh.Port, vh.IsSSL, vh.SSLCertPath, vh.SSLKeyPath,
                   vh.IsEnabled, vh.CreatedAt, vh.UpdatedAt, vh.Configuration,
                   p.Id, p.Name, p.DisplayName, p.Path, p.Domain, p.StackId,
                   s.Id, s.Name, s.DisplayName, s.Version, s.ServiceType, s.DefaultPort
            FROM VirtualHosts vh
            LEFT JOIN Projects p ON vh.ProjectId = p.Id
            LEFT JOIN Services s ON vh.ServiceId = s.Id
            WHERE vh.ServerName = @ServerName";
        
        var result = await connection.QueryAsync<VirtualHostData, Project, ServiceDefinition, VirtualHost>(
            sql,
            (vhostData, project, service) =>
            {
                var vhost = ConvertFromData(vhostData);
                vhost.Project = project;
                vhost.Service = service;
                return vhost;
            },
            new { ServerName = serverName },
            splitOn: "Id,Id");
        
        return result.FirstOrDefault();
    }

    public async Task<VirtualHost> CreateAsync(VirtualHost virtualHost)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var sql = @"
            INSERT INTO VirtualHosts (ProjectId, ServiceId, ServerName, ServerAlias, DocumentRoot,
                                     Port, IsSSL, SSLCertPath, SSLKeyPath, IsEnabled, 
                                     CreatedAt, UpdatedAt, Configuration)
            VALUES (@ProjectId, @ServiceId, @ServerName, @ServerAlias, @DocumentRoot,
                    @Port, @IsSSL, @SSLCertPath, @SSLKeyPath, @IsEnabled,
                    @CreatedAt, @UpdatedAt, @Configuration);
            SELECT last_insert_rowid();";
        
        var data = ConvertToData(virtualHost);
        data.CreatedAt = DateTime.UtcNow;
        data.UpdatedAt = data.CreatedAt;
        
        Console.WriteLine($"Creating virtual host: ServerName={data.ServerName}, Port={data.Port}, ProjectId={data.ProjectId}");
        
        var id = await connection.QuerySingleAsync<long>(sql, data);
        virtualHost.Id = id;
        virtualHost.CreatedAt = data.CreatedAt;
        virtualHost.UpdatedAt = data.UpdatedAt;
        
        Console.WriteLine($"Virtual host created with ID: {id}");
        return virtualHost;
    }

    public async Task<bool> UpdateAsync(VirtualHost virtualHost)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            UPDATE VirtualHosts 
            SET ProjectId = @ProjectId, ServiceId = @ServiceId, ServerName = @ServerName,
                ServerAlias = @ServerAlias, DocumentRoot = @DocumentRoot, Port = @Port,
                IsSSL = @IsSSL, SSLCertPath = @SSLCertPath, SSLKeyPath = @SSLKeyPath,
                IsEnabled = @IsEnabled, UpdatedAt = @UpdatedAt, Configuration = @Configuration
            WHERE Id = @Id";
        
        var data = ConvertToData(virtualHost);
        data.UpdatedAt = DateTime.UtcNow;
        
        var affected = await connection.ExecuteAsync(sql, data);
        if (affected > 0)
        {
            virtualHost.UpdatedAt = data.UpdatedAt;
            return true;
        }
        return false;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = "DELETE FROM VirtualHosts WHERE Id = @Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<bool> DeleteByProjectIdAsync(long projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = "DELETE FROM VirtualHosts WHERE ProjectId = @ProjectId";
        var affected = await connection.ExecuteAsync(sql, new { ProjectId = projectId });
        return affected > 0;
    }

    public async Task<List<VirtualHost>> GetEnabledVirtualHostsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT vh.Id, vh.ProjectId, vh.ServiceId, vh.ServerName, vh.ServerAlias,
                   vh.DocumentRoot, vh.Port, vh.IsSSL, vh.SSLCertPath, vh.SSLKeyPath,
                   vh.IsEnabled, vh.CreatedAt, vh.UpdatedAt, vh.Configuration,
                   p.Id, p.Name, p.DisplayName, p.Path, p.Domain, p.StackId,
                   s.Id, s.Name, s.DisplayName, s.Version, s.ServiceType, s.DefaultPort
            FROM VirtualHosts vh
            LEFT JOIN Projects p ON vh.ProjectId = p.Id
            LEFT JOIN Services s ON vh.ServiceId = s.Id
            WHERE vh.IsEnabled = 1
            ORDER BY vh.ServerName";
        
        var results = await connection.QueryAsync<VirtualHostData, Project, ServiceDefinition, VirtualHost>(
            sql,
            (vhostData, project, service) =>
            {
                var vhost = ConvertFromData(vhostData);
                vhost.Project = project;
                vhost.Service = service;
                return vhost;
            },
            splitOn: "Id,Id");
        
        return results.ToList();
    }

    public async Task<bool> ServerNameExistsAsync(string serverName, long? excludeId = null)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = "SELECT COUNT(1) FROM VirtualHosts WHERE ServerName = @ServerName";
        var parameters = new { ServerName = serverName };
        
        if (excludeId.HasValue)
        {
            sql += " AND Id != @ExcludeId";
            var extendedParams = new { ServerName = serverName, ExcludeId = excludeId.Value };
            var countWithExclusion = await connection.QuerySingleAsync<long>(sql, extendedParams);
            return countWithExclusion > 0;
        }
        
        var count = await connection.QuerySingleAsync<long>(sql, parameters);
        return count > 0;
    }

    private static VirtualHostData ConvertToData(VirtualHost virtualHost)
    {
        return new VirtualHostData
        {
            Id = virtualHost.Id,
            ProjectId = virtualHost.ProjectId,
            ServiceId = virtualHost.ServiceId,
            ServerName = virtualHost.ServerName,
            ServerAlias = virtualHost.ServerAlias,
            DocumentRoot = virtualHost.DocumentRoot,
            Port = virtualHost.Port,
            IsSSL = virtualHost.IsSSL,
            SSLCertPath = virtualHost.SSLCertPath,
            SSLKeyPath = virtualHost.SSLKeyPath,
            IsEnabled = virtualHost.IsEnabled,
            CreatedAt = virtualHost.CreatedAt,
            UpdatedAt = virtualHost.UpdatedAt,
            Configuration = virtualHost.Configuration.Any() ? JsonSerializer.Serialize(virtualHost.Configuration) : null
        };
    }

    private static VirtualHost ConvertFromData(VirtualHostData data)
    {
        var virtualHost = new VirtualHost
        {
            Id = data.Id,
            ProjectId = data.ProjectId,
            ServiceId = data.ServiceId,
            ServerName = data.ServerName,
            ServerAlias = data.ServerAlias,
            DocumentRoot = data.DocumentRoot,
            Port = data.Port,
            IsSSL = data.IsSSL,
            SSLCertPath = data.SSLCertPath,
            SSLKeyPath = data.SSLKeyPath,
            IsEnabled = data.IsEnabled,
            CreatedAt = data.CreatedAt,
            UpdatedAt = data.UpdatedAt
        };

        if (!string.IsNullOrEmpty(data.Configuration) && data.Configuration.StartsWith("{"))
        {
            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(data.Configuration);
                virtualHost.Configuration = config ?? new Dictionary<string, object>();
            }
            catch
            {
                virtualHost.Configuration = new Dictionary<string, object>();
            }
        }
        else
        {
            virtualHost.Configuration = new Dictionary<string, object>();
        }

        return virtualHost;
    }

    private static Project ConvertProjectFromData(ProjectData data)
    {
        var project = new Project
        {
            Id = data.Id,
            Name = data.Name,
            DisplayName = data.DisplayName,
            Path = data.Path,
            Domain = data.Domain,
            StackId = data.StackId,
            DocumentRoot = data.DocumentRoot,
            IsActive = data.IsActive,
            CreatedAt = data.CreatedAt,
            UpdatedAt = data.UpdatedAt
        };

        if (!string.IsNullOrEmpty(data.Configuration) && data.Configuration.StartsWith("{"))
        {
            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(data.Configuration);
                project.Configuration = config ?? new Dictionary<string, object>();
            }
            catch
            {
                project.Configuration = new Dictionary<string, object>();
            }
        }
        else
        {
            project.Configuration = new Dictionary<string, object>();
        }

        return project;
    }

    private class ProjectData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public int? StackId { get; set; }
        public string? DocumentRoot { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? Configuration { get; set; }
    }

    private class VirtualHostData
    {
        public long Id { get; set; }
        public long? ProjectId { get; set; }
        public long ServiceId { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public string? ServerAlias { get; set; }
        public string DocumentRoot { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool IsSSL { get; set; }
        public string? SSLCertPath { get; set; }
        public string? SSLKeyPath { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? Configuration { get; set; }
    }
}

