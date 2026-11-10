using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModStack.Common.Models;

/// <summary>
/// Represents a direct binding between a project and a service
/// </summary>
public class ProjectService
{
    [Key]
    public long Id { get; set; }

    [Required]
    public long ProjectId { get; set; }

    [Required]
    public long ServiceId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string? Configuration { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey(nameof(ServiceId))]
    public virtual ServiceDefinition Service { get; set; } = null!;
}
