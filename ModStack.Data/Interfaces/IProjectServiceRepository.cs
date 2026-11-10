using ModStack.Common.Models;
using Microsoft.Extensions.Logging;

namespace ModStack.Data.Interfaces;

/// <summary>
/// Repository interface for project-service bindings
/// </summary>
public interface IProjectServiceRepository
{
    /// <summary>
    /// Get all project-service bindings for a specific project
    /// </summary>
    Task<List<ProjectService>> GetByProjectIdAsync(long projectId);

    /// <summary>
    /// Get all project-service bindings for a specific service
    /// </summary>
    Task<List<ProjectService>> GetByServiceIdAsync(long serviceId);

    /// <summary>
    /// Get a specific project-service binding
    /// </summary>
    Task<ProjectService?> GetByIdAsync(long id);

    /// <summary>
    /// Get a specific project-service binding by project and service IDs
    /// </summary>
    Task<ProjectService?> GetByProjectAndServiceIdAsync(long projectId, long serviceId);

    /// <summary>
    /// Create a new project-service binding
    /// </summary>
    Task<ProjectService> CreateAsync(ProjectService projectService);

    /// <summary>
    /// Update an existing project-service binding
    /// </summary>
    Task<bool> UpdateAsync(ProjectService projectService);

    /// <summary>
    /// Delete a project-service binding (returns true if any rows were deleted)
    /// </summary>
    Task<bool> DeleteAsync(long id);

    /// <summary>
    /// Delete all bindings for a specific project (returns true if any rows were deleted)
    /// </summary>
    Task<bool> DeleteByProjectIdAsync(long projectId);

    /// <summary>
    /// Delete a specific project-service binding by project and service IDs (returns true if any rows were deleted)
    /// </summary>
    Task<bool> DeleteByProjectAndServiceIdAsync(long projectId, long serviceId);

    /// <summary>
    /// Check if a project-service binding exists
    /// </summary>
    Task<bool> ExistsAsync(long projectId, long serviceId);

    /// <summary>
    /// Create multiple project-service bindings in a single transaction
    /// </summary>
    Task<List<ProjectService>> CreateBatchAsync(IEnumerable<ProjectService> projectServices);

    /// <summary>
    /// Delete project-service bindings for multiple projects in a single transaction (returns count of deleted rows)
    /// </summary>
    Task<int> DeleteByProjectIdsAsync(IEnumerable<long> projectIds);
}
