namespace ModStack.Core.Common;

/// <summary>
/// Constants for service status values to avoid hard-coded strings and typos
/// </summary>
public static class ServiceStatus
{
    public const string Running = "Running";
    public const string Stopped = "Stopped";
    public const string NotInstalled = "Not Installed";
    public const string Unknown = "Unknown";
    public const string Starting = "Starting...";
    public const string Stopping = "Stopping...";
    public const string Error = "Error";
}
