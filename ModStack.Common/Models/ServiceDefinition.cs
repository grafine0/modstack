using System.ComponentModel.DataAnnotations;
using ModStack.Common;

namespace ModStack.Common.Models;

/// <summary>
/// Represents a service definition stored in the Services table
/// </summary>
public class ServiceDefinition
{
    [Key]
    public long Id { get; set; }
    
    /// <summary>
    /// Internal name of the service (e.g., "apache", "mysql")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name shown in the UI (e.g., "Apache HTTP Server")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of service (Apache, MySQL, PHP, etc.)
    /// </summary>
    [Required]
    public ServiceType ServiceType { get; set; }
    
    /// <summary>
    /// Version of the service
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// Binary path for the service
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string BinaryPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Configuration file path
    /// </summary>
    [MaxLength(500)]
    public string? ConfigPath { get; set; }
    
    /// <summary>
    /// Default port for the service
    /// </summary>
    public int? DefaultPort { get; set; }
    
    /// <summary>
    /// Whether this service is currently installed
    /// </summary>
    public bool IsInstalled { get; set; } = false;
    
    /// <summary>
    /// Whether this service is enabled/available for installation
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Installation date
    /// </summary>
    public DateTime? InstallDate { get; set; }
    
    /// <summary>
    /// When this service was first added to the system
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this service was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Description of the service for display in the UI
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Current status of the service
    /// </summary>
    public int Status { get; set; } = 0;
    
    /// <summary>
    /// Whether the service should auto-start
    /// </summary>
    public bool AutoStart { get; set; } = false;
    
    /// <summary>
    /// Service-specific configuration (JSON format)
    /// </summary>
    public string? Configuration { get; set; }
    
    /// <summary>
    /// Service dependencies
    /// </summary>
    public string? Dependencies { get; set; }
    
    /// <summary>
    /// Unique identifier for the service
    /// </summary>
    public string? UniqueId { get; set; }
    
    /// <summary>
    /// Windows service name (if registered as Windows service)
    /// </summary>
    public string? WindowsServiceName { get; set; }
}
