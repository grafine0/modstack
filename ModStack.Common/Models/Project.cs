namespace ModStack.Common.Models;

/// <summary>
/// Represents a project bound to a stack
/// </summary>
public class Project
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public long? StackId { get; set; }
    public string? DocumentRoot { get; set; }
    public bool IsActive { get; set; } = true;
    public bool EnableSSL { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// The stack this project is bound to
    /// </summary>
    public Stack? Stack { get; set; }
    
    /// <summary>
    /// Virtual hosts configured for this project
    /// </summary>
    public List<VirtualHost> VirtualHosts { get; set; } = new();
    
    /// <summary>
    /// Project-specific configuration
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();
    
    /// <summary>
    /// Gets the effective document root for this project
    /// </summary>
    public string GetDocumentRoot()
    {
        return DocumentRoot ?? Path;
    }
    
    /// <summary>
    /// Gets the primary domain for this project
    /// </summary>
    public string GetPrimaryDomain()
    {
        return Domain ?? $"{Name.ToLowerInvariant()}.local";
    }
    
    /// <summary>
    /// Gets all domains associated with this project
    /// </summary>
    public List<string> GetAllDomains()
    {
        var domains = new List<string> { GetPrimaryDomain() };
        
        foreach (var vhost in VirtualHosts)
        {
            domains.Add(vhost.ServerName);
            if (!string.IsNullOrEmpty(vhost.ServerAlias))
            {
                domains.AddRange(vhost.ServerAlias.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
        }
        
        return domains.Distinct().ToList();
    }
    
    /// <summary>
    /// Checks if the project path exists and is accessible
    /// </summary>
    public bool IsPathValid()
    {
        return Directory.Exists(Path);
    }
    
    /// <summary>
    /// Gets the project URL based on stack configuration
    /// </summary>
    public string GetProjectUrl()
    {
        var domain = GetPrimaryDomain();
        var webServer = Stack?.GetWebServer();
        
        if (webServer == null)
            return $"http://{domain}";
        
        var port = webServer.EffectivePort;
        var scheme = port == 443 ? "https" : "http";
        
        if ((scheme == "http" && port == 80) || (scheme == "https" && port == 443))
        {
            return $"{scheme}://{domain}";
        }
        
        return $"{scheme}://{domain}:{port}";
    }
}
