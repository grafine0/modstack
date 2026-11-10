namespace ModStack.Common.Models;

/// <summary>
/// Request model for creating virtual hosts
/// </summary>
public class CreateVirtualHostRequest
{
    public long? ProjectId { get; set; }
    public long ServiceId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string? ServerAlias { get; set; }
    public string DocumentRoot { get; set; } = string.Empty;
    public int Port { get; set; } = 80;
    public bool IsSSL { get; set; } = false;
    public string? SSLCertPath { get; set; }
    public string? SSLKeyPath { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? AdditionalConfiguration { get; set; }
    
    /// <summary>
    /// PHP wrapper path for FCGID configuration (e.g., "C:\ModStack\Services\PHP\php-8.4.0\bin\php-cgi.exe")
    /// </summary>
    public string? PHPWrapper { get; set; }
}

/// <summary>
/// Request model for updating virtual hosts
/// </summary>
public class UpdateVirtualHostRequest
{
    public string ServerName { get; set; } = string.Empty;
    public string? ServerAlias { get; set; }
    public string DocumentRoot { get; set; } = string.Empty;
    public int Port { get; set; } = 80;
    public bool IsSSL { get; set; } = false;
    public string? SSLCertPath { get; set; }
    public string? SSLKeyPath { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? AdditionalConfiguration { get; set; }
}
