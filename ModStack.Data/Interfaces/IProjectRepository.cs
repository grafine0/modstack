using ModStack.Common.Models;
using System.Threading;

namespace ModStack.Data.Interfaces;

/// <summary>
/// Repository interface for project data access
/// </summary>
public interface IProjectRepository
{
    Task<List<Project>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Project?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Project?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<Project>> GetByStackIdAsync(long stackId, CancellationToken cancellationToken = default);
    Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Project project, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> BindToStackAsync(long projectId, long stackId, CancellationToken cancellationToken = default);
    Task<bool> UnbindFromStackAsync(long projectId, CancellationToken cancellationToken = default);
    Task<List<Project>> GetActiveProjectsAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);
}
