namespace ModStack.Common;

/// <summary>
/// Supported service types in the ModStack environment
/// </summary>
public enum ServiceType
{
    Apache,
    Nginx,
    PHP,
    MySQL,
    MariaDB,
    PostgreSQL,
    MongoDB,
    Redis,
    Memcached,
    Elasticsearch,
    NodeJS,
    PhpMyAdmin,
    Python,
    Java,
    Other
}

/// <summary>
/// Current status of a service
/// </summary>
public enum ServiceStatus
{
    Unknown,
    Stopped,
    Starting,
    Running,
    Stopping,
    Error,
    Installing,
    Uninstalling
}

/// <summary>
/// Stack status
/// </summary>
public enum StackStatus
{
    Inactive,
    Starting,
    Active,
    Stopping,
    Error,
    PartiallyActive
}

/// <summary>
/// Log levels for service logging
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// Configuration file types
/// </summary>
public enum ConfigFileType
{
    Conf,
    Ini,
    Json,
    Yaml,
    Xml,
    Properties
}

/// <summary>
/// Network protocols
/// </summary>
public enum Protocol
{
    TCP,
    UDP
}

/// <summary>
/// Service installation types
/// </summary>
public enum InstallationType
{
    Portable,
    WindowsService,
    Docker,
    WSL
}
