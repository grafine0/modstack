using Dapper;
using Microsoft.Data.Sqlite;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using System.Text.Json;

namespace ModStack.Data.Repositories;

/// <summary>
/// Repository implementation for project data access
/// </summary>
public class ProjectRepository : IProjectRepository
{
    private readonly string _connectionString;

    public ProjectRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT p.Id, p.Name, p.DisplayName, p.Path, p.Domain, p.StackId, 
                   p.DocumentRoot, p.IsActive, p.CreatedAt, p.UpdatedAt, p.Configuration,
                   s.Id, s.Name, s.DisplayName, s.Description, s.IsActive, s.IsDefault, 
                   COALESCE(s.Configuration, '{}') as Configuration, s.CreatedAt, s.UpdatedAt
            FROM Projects p
            LEFT JOIN Stacks s ON p.StackId = s.Id
            ORDER BY p.Name";
        
        var results = await connection.QueryAsync<ProjectData, Stack, Project>(
            new CommandDefinition(sql, cancellationToken: cancellationToken),
            (projectData, stack) =>
            {
                var project = ConvertFromData(projectData);
                project.Stack = stack;
                return project;
            },
            splitOn: "Id");
        
        return results.ToList();
    }

    public async Task<Project?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT p.Id, p.Name, p.DisplayName, p.Path, p.Domain, p.StackId, 
                   p.DocumentRoot, p.IsActive, p.EnableSSL, p.CreatedAt, p.UpdatedAt, p.Configuration,
                   s.Id, s.Name, s.DisplayName, s.Description, s.IsActive, s.IsDefault, 
                   COALESCE(s.Configuration, '{}') as Configuration, s.CreatedAt, s.UpdatedAt
            FROM Projects p
            LEFT JOIN Stacks s ON p.StackId = s.Id
            WHERE p.Id = @Id";
        
        var result = await connection.QueryAsync<ProjectData, Stack, Project>(
            sql,
            (projectData, stack) =>
            {
                var project = ConvertFromData(projectData);
                project.Stack = stack;
                return project;
            },
            new { Id = id },
            splitOn: "Id");
        
        return result.FirstOrDefault();
    }

    public async Task<Project?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT p.Id, p.Name, p.DisplayName, p.Path, p.Domain, p.StackId, 
                   p.DocumentRoot, p.IsActive, p.CreatedAt, p.UpdatedAt, p.Configuration,
                   s.Id, s.Name, s.DisplayName, s.Description, s.IsActive, s.IsDefault, 
                   COALESCE(s.Configuration, '{}') as Configuration, s.CreatedAt, s.UpdatedAt
            FROM Projects p
            LEFT JOIN Stacks s ON p.StackId = s.Id
            WHERE p.Name = @Name";
        
        var result = await connection.QueryAsync<ProjectData, Stack, Project>(
            sql,
            (projectData, stack) =>
            {
                var project = ConvertFromData(projectData);
                project.Stack = stack;
                return project;
            },
            new { Name = name },
            splitOn: "Id");
        
        return result.FirstOrDefault();
    }

    public async Task<List<Project>> GetByStackIdAsync(long stackId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT p.Id, p.Name, p.DisplayName, p.Path, p.Domain, p.StackId, 
                   p.DocumentRoot, p.IsActive, p.CreatedAt, p.UpdatedAt, p.Configuration,
                   s.Id, s.Name, s.DisplayName, s.Description, s.IsActive, s.IsDefault, 
                   COALESCE(s.Configuration, '{}') as Configuration, s.CreatedAt, s.UpdatedAt
            FROM Projects p
            LEFT JOIN Stacks s ON p.StackId = s.Id
            WHERE p.StackId = @StackId
            ORDER BY p.Name";
        
        var results = await connection.QueryAsync<ProjectData, Stack, Project>(
            sql,
            (projectData, stack) =>
            {
                var project = ConvertFromData(projectData);
                project.Stack = stack;
                return project;
            },
            new { StackId = stackId },
            splitOn: "Id");
        
        return results.ToList();
    }

    public async Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            INSERT INTO Projects (Name, DisplayName, Path, Domain, StackId, DocumentRoot, 
                                 IsActive, EnableSSL, CreatedAt, UpdatedAt, Configuration)
            VALUES (@Name, @DisplayName, @Path, @Domain, @StackId, @DocumentRoot, 
                    @IsActive, @EnableSSL, @CreatedAt, @UpdatedAt, @Configuration);
            SELECT last_insert_rowid();";
        
        var data = ConvertToData(project);
        data.CreatedAt = DateTime.UtcNow;
        data.UpdatedAt = data.CreatedAt;
        
        var id = await connection.QuerySingleAsync<long>(sql, data);
        project.Id = id;
        project.CreatedAt = data.CreatedAt;
        project.UpdatedAt = data.UpdatedAt;
        return project;
    }

    public async Task<bool> UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            UPDATE Projects 
            SET Name = @Name, DisplayName = @DisplayName, Path = @Path, Domain = @Domain,
                StackId = @StackId, DocumentRoot = @DocumentRoot, IsActive = @IsActive,
                EnableSSL = @EnableSSL, UpdatedAt = @UpdatedAt, Configuration = @Configuration
            WHERE Id = @Id";
        
        var data = ConvertToData(project);
        data.UpdatedAt = DateTime.UtcNow;
        
        var affected = await connection.ExecuteAsync(sql, data);
        if (affected > 0)
        {
            project.UpdatedAt = data.UpdatedAt;
            return true;
        }
        return false;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = "DELETE FROM Projects WHERE Id = @Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<bool> BindToStackAsync(long projectId, long stackId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            UPDATE Projects 
            SET StackId = @StackId, UpdatedAt = @UpdatedAt
            WHERE Id = @ProjectId";
        
        var affected = await connection.ExecuteAsync(sql, new 
        { 
            ProjectId = projectId, 
            StackId = stackId, 
            UpdatedAt = DateTime.UtcNow 
        });
        return affected > 0;
    }

    public async Task<bool> UnbindFromStackAsync(long projectId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            UPDATE Projects 
            SET StackId = NULL, UpdatedAt = @UpdatedAt
            WHERE Id = @ProjectId";
        
        var affected = await connection.ExecuteAsync(sql, new 
        { 
            ProjectId = projectId, 
            UpdatedAt = DateTime.UtcNow 
        });
        return affected > 0;
    }

    public async Task<List<Project>> GetActiveProjectsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT p.Id, p.Name, p.DisplayName, p.Path, p.Domain, p.StackId, 
                   p.DocumentRoot, p.IsActive, p.CreatedAt, p.UpdatedAt, p.Configuration,
                   s.Id, s.Name, s.DisplayName, s.Description, s.IsActive, s.IsDefault, 
                   COALESCE(s.Configuration, '{}') as Configuration, s.CreatedAt, s.UpdatedAt
            FROM Projects p
            LEFT JOIN Stacks s ON p.StackId = s.Id
            WHERE p.IsActive = 1
            ORDER BY p.Name";
        
        var results = await connection.QueryAsync<ProjectData, Stack, Project>(
            sql,
            (projectData, stack) =>
            {
                var project = ConvertFromData(projectData);
                project.Stack = stack;
                return project;
            },
            splitOn: "Id");
        
        return results.ToList();
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = "SELECT COUNT(1) FROM Projects WHERE Name = @Name";
        var count = await connection.QuerySingleAsync<long>(sql, new { Name = name });
        return count > 0;
    }

    private static ProjectData ConvertToData(Project project)
    {
        return new ProjectData
        {
            Id = project.Id,
            Name = project.Name,
            DisplayName = project.DisplayName,
            Path = project.Path,
            Domain = project.Domain,
            StackId = project.StackId,
            DocumentRoot = project.DocumentRoot,
            IsActive = project.IsActive,
            EnableSSL = project.EnableSSL,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            Configuration = project.Configuration.Any() ? JsonSerializer.Serialize(project.Configuration) : null
        };
    }

    private static Project ConvertFromData(ProjectData data)
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
            EnableSSL = data.EnableSSL,
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
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public long? StackId { get; set; }
        public string? DocumentRoot { get; set; }
        public bool IsActive { get; set; }
        public bool EnableSSL { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? Configuration { get; set; }
    }
}
