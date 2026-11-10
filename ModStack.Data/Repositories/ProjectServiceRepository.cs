using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Common;
using System.Data;

namespace ModStack.Data.Repositories;

/// <summary>
/// Repository implementation for project-service bindings
/// </summary>
public class ProjectServiceRepository(string connectionString, ILogger<ProjectServiceRepository> logger) : IProjectServiceRepository
{
    private readonly string _connectionString = connectionString;
    private readonly ILogger<ProjectServiceRepository> _logger = logger;

    // DTOs for strongly-typed query mapping - using POCO classes for better Dapper compatibility
    private class ProjectServiceRow
    {
        public long Id { get; set; }
        public long ProjectId { get; set; }
        public long ServiceId { get; set; }
        public bool IsEnabled { get; set; }       // Dapper maps 0/1 → false/true automatically
        public string? Configuration { get; set; }
        public DateTime CreatedAt { get; set; }   // Dapper handles string→DateTime conversion
        public DateTime UpdatedAt { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceDisplayName { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
    }

    private class ProjectServiceWithProjectRow
    {
        public long Id { get; set; }
        public long ProjectId { get; set; }
        public long ServiceId { get; set; }
        public bool IsEnabled { get; set; }
        public string? Configuration { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectDisplayName { get; set; } = string.Empty;
    }

    private class ProjectServiceFullRow
    {
        public long Id { get; set; }
        public long ProjectId { get; set; }
        public long ServiceId { get; set; }
        public bool IsEnabled { get; set; }
        public string? Configuration { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceDisplayName { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectDisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Safely parses ServiceType from database value, handling both integer and string formats
    /// </summary>
    private static ServiceType ParseServiceType(string raw)
    {
        if (int.TryParse(raw, out var typeInt))
            return (ServiceType)typeInt;

        return Enum.Parse<ServiceType>(raw, ignoreCase: true);
    }

    public async Task<List<ProjectService>> GetByProjectIdAsync(long projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var sql = @"
            SELECT ps.*, s.Name as ServiceName, s.DisplayName as ServiceDisplayName, s.ServiceType
            FROM ProjectServices ps
            INNER JOIN Services s ON ps.ServiceId = s.Id
            WHERE ps.ProjectId = @ProjectId
            ORDER BY s.DisplayName";
        
        var results = await connection.QueryAsync<ProjectServiceRow>(sql, new { ProjectId = projectId });
        
        return results.Select(r => new ProjectService
        {
            Id = r.Id,
            ProjectId = r.ProjectId,
            ServiceId = r.ServiceId,
            IsEnabled = r.IsEnabled,
            Configuration = r.Configuration,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            Service = new ServiceDefinition
            {
                Id = r.ServiceId,
                Name = r.ServiceName,
                DisplayName = r.ServiceDisplayName,
                ServiceType = ParseServiceType(r.ServiceType)
            }
        }).ToList(); // IDE0305: Keep ToList() for explicit conversion
    }

    public async Task<List<ProjectService>> GetByServiceIdAsync(long serviceId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT ps.*, p.Name as ProjectName, p.DisplayName as ProjectDisplayName
            FROM ProjectServices ps
            INNER JOIN Projects p ON ps.ProjectId = p.Id
            WHERE ps.ServiceId = @ServiceId
            ORDER BY p.DisplayName";
        
        var results = await connection.QueryAsync<ProjectServiceWithProjectRow>(sql, new { ServiceId = serviceId });
        
        return [.. results.Select(r => new ProjectService
        {
            Id = r.Id,
            ProjectId = r.ProjectId,
            ServiceId = r.ServiceId,
            IsEnabled = r.IsEnabled,
            Configuration = r.Configuration,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            Project = new Project
            {
                Id = r.ProjectId,
                Name = r.ProjectName,
                DisplayName = r.ProjectDisplayName
            }
        })];
    }

    public async Task<ProjectService?> GetByIdAsync(long id)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT ps.*, s.Name as ServiceName, s.DisplayName as ServiceDisplayName, s.ServiceType,
                   p.Name as ProjectName, p.DisplayName as ProjectDisplayName
            FROM ProjectServices ps
            INNER JOIN Services s ON ps.ServiceId = s.Id
            INNER JOIN Projects p ON ps.ProjectId = p.Id
            WHERE ps.Id = @Id";
        
        var result = await connection.QueryFirstOrDefaultAsync<ProjectServiceFullRow>(sql, new { Id = id });
        
        if (result == null) return null;
        
        return new ProjectService
        {
            Id = result.Id,
            ProjectId = result.ProjectId,
            ServiceId = result.ServiceId,
            IsEnabled = result.IsEnabled,
            Configuration = result.Configuration,
            CreatedAt = result.CreatedAt,
            UpdatedAt = result.UpdatedAt,
            Project = new Project
            {
                Id = result.ProjectId,
                Name = result.ProjectName,
                DisplayName = result.ProjectDisplayName
            },
            Service = new ServiceDefinition
            {
                Id = result.ServiceId,
                Name = result.ServiceName,
                DisplayName = result.ServiceDisplayName,
                ServiceType = ParseServiceType(result.ServiceType)
            }
        };
    }

    public async Task<ProjectService?> GetByProjectAndServiceIdAsync(long projectId, long serviceId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = @"
            SELECT ps.*, s.Name as ServiceName, s.DisplayName as ServiceDisplayName, s.ServiceType,
                   p.Name as ProjectName, p.DisplayName as ProjectDisplayName
            FROM ProjectServices ps
            INNER JOIN Services s ON ps.ServiceId = s.Id
            INNER JOIN Projects p ON ps.ProjectId = p.Id
            WHERE ps.ProjectId = @ProjectId AND ps.ServiceId = @ServiceId";
        
        var result = await connection.QueryFirstOrDefaultAsync<ProjectServiceFullRow>(sql, new { ProjectId = projectId, ServiceId = serviceId });
        
        if (result == null) return null;
        
        return new ProjectService
        {
            Id = result.Id,
            ProjectId = result.ProjectId,
            ServiceId = result.ServiceId,
            IsEnabled = result.IsEnabled,
            Configuration = result.Configuration,
            CreatedAt = result.CreatedAt,
            UpdatedAt = result.UpdatedAt,
            Project = new Project
            {
                Id = result.ProjectId,
                Name = result.ProjectName,
                DisplayName = result.ProjectDisplayName
            },
            Service = new ServiceDefinition
            {
                Id = result.ServiceId,
                Name = result.ServiceName,
                DisplayName = result.ServiceDisplayName,
                ServiceType = ParseServiceType(result.ServiceType)
            }
        };
    }

    public async Task<ProjectService> CreateAsync(ProjectService projectService)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var sql = @"
                INSERT INTO ProjectServices (ProjectId, ServiceId, IsEnabled, Configuration, CreatedAt, UpdatedAt)
                VALUES (@ProjectId, @ServiceId, @IsEnabled, @Configuration, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();";
            
            var now = DateTime.UtcNow;
            var id = await connection.ExecuteScalarAsync<long>(sql, new
            {
                projectService.ProjectId,
                projectService.ServiceId,
                projectService.IsEnabled,
                projectService.Configuration,
                CreatedAt = projectService.CreatedAt == default ? now : projectService.CreatedAt,
                UpdatedAt = now
            }, transaction);
            
            await transaction.CommitAsync();
            
            projectService.Id = id;
            projectService.CreatedAt = projectService.CreatedAt == default ? now : projectService.CreatedAt;
            projectService.UpdatedAt = now;
            
            _logger.LogDebug("Created project-service binding: ProjectId={ProjectId}, ServiceId={ServiceId}, Id={Id}", 
                projectService.ProjectId, projectService.ServiceId, projectService.Id);
            
            return projectService;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Transaction failed for {Operation}", nameof(CreateAsync));
            throw;
        }
    }

    public async Task<bool> UpdateAsync(ProjectService projectService)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var sql = @"
                UPDATE ProjectServices 
                SET IsEnabled = @IsEnabled, Configuration = @Configuration, UpdatedAt = @UpdatedAt
                WHERE Id = @Id";
            
            var now = DateTime.UtcNow;
            var affected = await connection.ExecuteAsync(sql, new
            {
                projectService.Id,
                projectService.IsEnabled,
                projectService.Configuration,
                UpdatedAt = now
            }, transaction);
            
            await transaction.CommitAsync();
            
            if (affected > 0)
            {
                projectService.UpdatedAt = now;
                _logger.LogDebug("Updated project-service binding: Id={Id}, ProjectId={ProjectId}, ServiceId={ServiceId}", 
                    projectService.Id, projectService.ProjectId, projectService.ServiceId);
            }
            else
            {
                _logger.LogInformation("No rows affected for update: Id={Id}", projectService.Id);
            }
            
            return affected > 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Transaction failed for {Operation}", nameof(UpdateAsync));
            throw;
        }
    }

    public async Task<bool> DeleteAsync(long id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var sql = "DELETE FROM ProjectServices WHERE Id = @Id";
            var affected = await connection.ExecuteAsync(sql, new { Id = id }, transaction);
            await transaction.CommitAsync();
            
            if (affected > 0)
            {
                _logger.LogDebug("Deleted project-service binding: Id={Id}", id);
            }
            else
            {
                _logger.LogInformation("No rows affected for delete: Id={Id}", id);
            }
            
            return affected > 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Transaction failed for {Operation}", nameof(DeleteAsync));
            throw;
        }
    }

    public async Task<bool> DeleteByProjectIdAsync(long projectId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var sql = "DELETE FROM ProjectServices WHERE ProjectId = @ProjectId";
            var affected = await connection.ExecuteAsync(sql, new { ProjectId = projectId }, transaction);
            await transaction.CommitAsync();
            
            if (affected > 0)
            {
                _logger.LogDebug("Deleted {Count} project-service bindings for ProjectId={ProjectId}", affected, projectId);
            }
            else
            {
                _logger.LogInformation("No rows affected for delete by ProjectId: ProjectId={ProjectId}", projectId);
            }
            
            return affected > 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Transaction failed for {Operation}", nameof(DeleteByProjectIdAsync));
            throw;
        }
    }

    public async Task<bool> DeleteByProjectAndServiceIdAsync(long projectId, long serviceId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var sql = "DELETE FROM ProjectServices WHERE ProjectId = @ProjectId AND ServiceId = @ServiceId";
            var affected = await connection.ExecuteAsync(sql, new { ProjectId = projectId, ServiceId = serviceId }, transaction);
            await transaction.CommitAsync();
            
            if (affected > 0)
            {
                _logger.LogDebug("Deleted project-service binding: ProjectId={ProjectId}, ServiceId={ServiceId}", projectId, serviceId);
            }
            else
            {
                _logger.LogInformation("No rows affected for delete by ProjectId and ServiceId: ProjectId={ProjectId}, ServiceId={ServiceId}", projectId, serviceId);
            }
            
            return affected > 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Transaction failed for {Operation}", nameof(DeleteByProjectAndServiceIdAsync));
            throw;
        }
    }

    public async Task<bool> ExistsAsync(long projectId, long serviceId)
    {
        using var connection = new SqliteConnection(_connectionString);
        var sql = "SELECT 1 FROM ProjectServices WHERE ProjectId = @ProjectId AND ServiceId = @ServiceId LIMIT 1";
        var result = await connection.ExecuteScalarAsync<int?>(sql, new { ProjectId = projectId, ServiceId = serviceId });
        return result.HasValue;
    }

    public async Task<List<ProjectService>> CreateBatchAsync(IEnumerable<ProjectService> projectServices)
    {
        var projectServicesList = projectServices.ToList();
        if (projectServicesList.Count == 0)
            return [];

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var sql = @"
                INSERT INTO ProjectServices (ProjectId, ServiceId, IsEnabled, Configuration, CreatedAt, UpdatedAt)
                VALUES (@ProjectId, @ServiceId, @IsEnabled, @Configuration, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();";
            
            var now = DateTime.UtcNow;
            
            // Prepare all objects with timestamps
            foreach (var ps in projectServicesList)
            {
                ps.CreatedAt = ps.CreatedAt == default ? now : ps.CreatedAt;
                ps.UpdatedAt = now;
            }
            
            // Execute batch insert - Dapper handles multiple parameters efficiently
            var ids = await connection.QueryAsync<long>(sql, projectServicesList, transaction);
            
            // Assign the returned IDs back to the objects
            for (int i = 0; i < projectServicesList.Count; i++)
            {
                projectServicesList[i].Id = ids.ElementAt(i);
            }
            
            await transaction.CommitAsync();
            
            _logger.LogDebug("Created {Count} project-service bindings in batch for ProjectIds: {ProjectIds}", 
                projectServicesList.Count, string.Join(", ", projectServicesList.Select(r => r.ProjectId).Distinct()));
            
            return projectServicesList;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Transaction failed for {Operation}", nameof(CreateBatchAsync));
            throw;
        }
    }

    public async Task<int> DeleteByProjectIdsAsync(IEnumerable<long> projectIds)
    {
        var projectIdsList = projectIds.ToList();
        if (projectIdsList.Count == 0)
            return 0;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            // Note: Dapper automatically expands @ProjectIds parameter for SQLite IN clauses
            // This works well with SQLite, but may need explicit parameter expansion for other databases
            var sql = "DELETE FROM ProjectServices WHERE ProjectId IN @ProjectIds";
            var affected = await connection.ExecuteAsync(sql, new { ProjectIds = projectIdsList }, transaction);
            await transaction.CommitAsync();
            
            _logger.LogDebug("Deleted {Count} project-service bindings for ProjectIds: {ProjectIds}", 
                affected, string.Join(", ", projectIdsList));
            
            return affected;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Transaction failed for {Operation}", nameof(DeleteByProjectIdsAsync));
            throw;
        }
    }
}
