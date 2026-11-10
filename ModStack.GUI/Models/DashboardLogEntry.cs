namespace ModStack.GUI.Models;

/// <summary>
/// Enhanced log entry class for recent activity with data binding support
/// </summary>
public class DashboardLogEntry
{
    public string LogLevel { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Formatted message for display
    /// </summary>
    public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] {Message}";
    
    /// <summary>
    /// Style resource name based on log level
    /// </summary>
    public string LogLevelStyle => LogLevel.ToLower() switch
    {
        "error" => "ErrorLogStyle",
        "warning" => "WarningLogStyle", 
        "info" => "InfoLogStyle",
        _ => "DefaultLogStyle"
    };
}
