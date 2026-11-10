using ModStack.Common;

namespace ModStack.Common.Models;

/// <summary>
/// Represents a log entry for a service
/// </summary>
public class ServiceLog
{
    public long Id { get; set; }
    public long ServiceId { get; set; }
    public LogLevel LogLevel { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// The service this log entry belongs to
    /// </summary>
    public ServiceDefinition? Service { get; set; }
    
    /// <summary>
    /// Gets a formatted log message
    /// </summary>
    public string GetFormattedMessage()
    {
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{LogLevel}] {Message}";
    }
    
    /// <summary>
    /// Gets the full log entry including details
    /// </summary>
    public string GetFullLogEntry()
    {
        var entry = GetFormattedMessage();
        
        if (!string.IsNullOrEmpty(Details))
        {
            entry += Environment.NewLine + Details;
        }
        
        return entry;
    }
}
