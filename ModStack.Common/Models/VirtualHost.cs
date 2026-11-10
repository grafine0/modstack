namespace ModStack.Common.Models;

/// <summary>
/// FCGID configuration for PHP processing
/// </summary>
public class FCGIDConfig
{
    public long MaxRequestLen { get; set; } = 1073741824; // 1GB
    public int IOTimeout { get; set; } = 7200;
    public int ConnectTimeout { get; set; } = 7200;
    public int BusyTimeout { get; set; } = 7200;
    public int IdleTimeout { get; set; } = 7200;
    public int ProcessLifeTime { get; set; } = 7200;
    public string? PHPWrapper { get; set; }
}

/// <summary>
/// Represents a virtual host configuration for a web server
/// </summary>
public class VirtualHost
{
    public long Id { get; set; }
    public long? ProjectId { get; set; }
    public long ServiceId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string? ServerAlias { get; set; }
    public string DocumentRoot { get; set; } = string.Empty;
    public int Port { get; set; } = 80;
    public bool IsSSL { get; set; }
    public string? SSLCertPath { get; set; }
    public string? SSLKeyPath { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// The project this virtual host belongs to
    /// </summary>
    public Project? Project { get; set; }
    
    /// <summary>
    /// The web server service for this virtual host
    /// </summary>
    public ServiceDefinition? Service { get; set; }
    
    /// <summary>
    /// Additional virtual host configuration directives
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();
    
    /// <summary>
    /// FCGID configuration for PHP processing
    /// </summary>
    public FCGIDConfig? FCGID { get; set; }
    
    /// <summary>
    /// Gets the full URL for this virtual host
    /// </summary>
    public string GetUrl()
    {
        var scheme = IsSSL ? "https" : "http";
        var defaultPort = IsSSL ? 443 : 80;
        
        if (Port == defaultPort)
        {
            return $"{scheme}://{ServerName}";
        }
        
        return $"{scheme}://{ServerName}:{Port}";
    }
    
    /// <summary>
    /// Gets all server names (primary + aliases)
    /// </summary>
    public List<string> GetAllServerNames()
    {
        var names = new List<string> { ServerName };
        
        if (!string.IsNullOrEmpty(ServerAlias))
        {
            names.AddRange(ServerAlias.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
        
        return names;
    }
    
    /// <summary>
    /// Generates Apache virtual host configuration with FCGID support
    /// </summary>
    public string GenerateApacheConfig()
    {
        var config = new List<string>();
        
        // Use specific IP and port format
        var bindAddress = IsSSL ? "*:443" : "127.0.0.1:80";
        config.Add($"<VirtualHost {bindAddress}>");
        config.Add($"    DocumentRoot \"{DocumentRoot.Replace("\\", "/")}\"");
        config.Add($"    ServerName {ServerName}");
        
        if (!string.IsNullOrEmpty(ServerAlias))
        {
            var aliases = ServerAlias.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var alias in aliases)
            {
                config.Add($"    ServerAlias {alias}");
            }
        }
        
        // Add FCGID configuration if available (with conditional loading)
        if (FCGID != null)
        {
            config.Add($"    # FCGID Configuration (only if mod_fcgid is loaded)");
            config.Add($"    <IfModule mod_fcgid.c>");
            config.Add($"        FcgidMaxRequestLen {FCGID.MaxRequestLen}");
            config.Add($"        FcgidIOTimeout {FCGID.IOTimeout}");
            config.Add($"        FcgidConnectTimeout {FCGID.ConnectTimeout}");
            config.Add($"        FcgidBusyTimeout {FCGID.BusyTimeout}");
            config.Add($"        FcgidIdleTimeout {FCGID.IdleTimeout}");
            config.Add($"        FcgidProcessLifeTime {FCGID.ProcessLifeTime}");
            
            if (!string.IsNullOrEmpty(FCGID.PHPWrapper))
            {
                config.Add($"        FcgidWrapper \"{FCGID.PHPWrapper.Replace("\\", "/")}\" .php");
            }
            config.Add($"    </IfModule>");
        }
        
        config.Add($"    <Directory \"{DocumentRoot.Replace("\\", "/")}\">");
        
        if (FCGID != null)
        {
            config.Add($"        <IfModule mod_fcgid.c>");
            config.Add($"            AddHandler fcgid-script .php");
            config.Add($"        </IfModule>");
        }
        
        config.Add($"        Options all");
        config.Add($"        AllowOverride all");
        config.Add($"        Require all granted");
        config.Add($"    </Directory>");
        
        if (IsSSL)
        {
            config.Add($"    # SSL Configuration (only if mod_ssl is loaded)");
            config.Add($"    <IfModule mod_ssl.c>");
            config.Add($"        SSLEngine on");
            if (!string.IsNullOrEmpty(SSLCertPath))
                config.Add($"        SSLCertificateFile \"{SSLCertPath.Replace("\\", "/")}\"");
            if (!string.IsNullOrEmpty(SSLKeyPath))
                config.Add($"        SSLCertificateKeyFile \"{SSLKeyPath.Replace("\\", "/")}\"");
            config.Add($"    </IfModule>");
        }
        
        // Add custom configuration
        foreach (var kvp in Configuration)
        {
            config.Add($"    {kvp.Key} {kvp.Value}");
        }
        
        config.Add($"</VirtualHost>");
        
        return string.Join(Environment.NewLine, config);
    }
    
    /// <summary>
    /// Generates Nginx server block configuration
    /// </summary>
    public string GenerateNginxConfig()
    {
        var config = new List<string>();
        
        config.Add($"server {{");
        config.Add($"    listen {Port}{(IsSSL ? " ssl" : "")};");
        config.Add($"    server_name {ServerName}{(string.IsNullOrEmpty(ServerAlias) ? "" : " " + ServerAlias)};");
        config.Add($"    root \"{DocumentRoot.Replace("\\", "/")}\";");
        config.Add($"    index index.php index.html index.htm;");
        
        if (IsSSL)
        {
            if (!string.IsNullOrEmpty(SSLCertPath))
                config.Add($"    ssl_certificate \"{SSLCertPath.Replace("\\", "/")}\";");
            if (!string.IsNullOrEmpty(SSLKeyPath))
                config.Add($"    ssl_certificate_key \"{SSLKeyPath.Replace("\\", "/")}\";");
        }
        
        config.Add($"    location / {{");
        config.Add($"        try_files $uri $uri/ /index.php?$query_string;");
        config.Add($"    }}");
        
        config.Add($"    location ~ \\.php$ {{");
        config.Add($"        fastcgi_pass 127.0.0.1:9000;");
        config.Add($"        fastcgi_index index.php;");
        config.Add($"        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;");
        config.Add($"        include fastcgi_params;");
        config.Add($"    }}");
        
        // Add custom configuration
        foreach (var kvp in Configuration)
        {
            config.Add($"    {kvp.Key} {kvp.Value};");
        }
        
        config.Add($"}}");
        
        return string.Join(Environment.NewLine, config);
    }
}
