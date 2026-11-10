using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Common;
using ModStack.Common.Security;
using ModStack.Services.Interfaces;
using System.Text;
using System.Diagnostics;

namespace ModStack.Services;

/// <summary>
/// Manages virtual hosts for Apache and Nginx services
/// </summary>
public class VirtualHostManager : IVirtualHostManager
{
    private readonly ILogger<VirtualHostManager> _logger;
    private readonly IVirtualHostRepository _virtualHostRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IProjectRepository _projectRepository;

    public VirtualHostManager(
        ILogger<VirtualHostManager> logger,
        IVirtualHostRepository virtualHostRepository,
        IServiceRepository serviceRepository,
        IProjectRepository projectRepository)
    {
        _logger = logger;
        _virtualHostRepository = virtualHostRepository;
        _serviceRepository = serviceRepository;
        _projectRepository = projectRepository;
    }

    /// <summary>
    /// Creates a virtual host for a project
    /// </summary>
    public async Task<VirtualHost> CreateVirtualHostAsync(CreateVirtualHostRequest request)
    {
        try
        {
            SecurityValidator.ValidateHostnameOrThrow(request.ServerName, nameof(request.ServerName));
            if (!string.IsNullOrWhiteSpace(request.ServerAlias))
            {
                foreach (var alias in request.ServerAlias.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    SecurityValidator.ValidateHostnameOrThrow(alias, nameof(request.ServerAlias));
                }
            }

            _logger.LogInformation("Creating virtual host for project {ProjectId}", request.ProjectId);

            // Validate project exists if ProjectId is provided
            if (request.ProjectId.HasValue)
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId.Value);
                if (project == null)
                    throw new ArgumentException($"Project with ID {request.ProjectId.Value} not found");
            }

            // Validate service exists and is web server
            var service = await _serviceRepository.GetByIdAsync(request.ServiceId);
            if (service == null)
                throw new ArgumentException($"Service with ID {request.ServiceId} not found");

            if (service.ServiceType != ServiceType.Apache && service.ServiceType != ServiceType.Nginx)
                throw new ArgumentException("Service must be Apache or Nginx");

            // Validate SSL configuration
            if (request.IsSSL && (string.IsNullOrEmpty(request.SSLCertPath) || string.IsNullOrEmpty(request.SSLKeyPath)))
                throw new InvalidOperationException("SSL is enabled but certificate paths are missing. Both SSLCertPath and SSLKeyPath are required when IsSSL is true.");

            // Check for port conflicts
            await CheckPortConflictAsync(request.Port, request.ServiceId);

            // Create virtual host
            var virtualHost = new VirtualHost
            {
                ProjectId = request.ProjectId,
                ServiceId = request.ServiceId,
                ServerName = request.ServerName,
                ServerAlias = request.ServerAlias,
                DocumentRoot = request.DocumentRoot,
                Port = request.Port,
                IsSSL = request.IsSSL,
                SSLCertPath = request.SSLCertPath,
                SSLKeyPath = request.SSLKeyPath,
                IsEnabled = request.IsEnabled,
                Configuration = new Dictionary<string, object>(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            // Store AdditionalConfiguration for use in config generation
            if (!string.IsNullOrEmpty(request.AdditionalConfiguration))
            {
                virtualHost.Configuration["AdditionalConfiguration"] = request.AdditionalConfiguration;
            }

            // Set FCGID configuration if PHP wrapper is provided
            if (!string.IsNullOrEmpty(request.PHPWrapper))
            {
                virtualHost.FCGID = new FCGIDConfig
                {
                    PHPWrapper = request.PHPWrapper,
                    MaxRequestLen = 1073741824, // 1GB
                    IOTimeout = 20,
                    ConnectTimeout = 20,
                    BusyTimeout = 20,
                    IdleTimeout = 20,
                    ProcessLifeTime = 20
                };
            }

            var createdVirtualHost = await _virtualHostRepository.CreateAsync(virtualHost);

            // Generate configuration file
            await GenerateVirtualHostConfigurationAsync(createdVirtualHost, service);

            _logger.LogInformation("Virtual host created successfully with ID {VirtualHostId}", createdVirtualHost.Id);
            _logger.LogInformation("Virtual host details: ServerName={ServerName}, Port={Port}, DocumentRoot={DocumentRoot}", 
                createdVirtualHost.ServerName, createdVirtualHost.Port, createdVirtualHost.DocumentRoot);
            return createdVirtualHost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create virtual host");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing virtual host
    /// </summary>
    public async Task<VirtualHost> UpdateVirtualHostAsync(long virtualHostId, UpdateVirtualHostRequest request)
    {
        try
        {
            SecurityValidator.ValidateHostnameOrThrow(request.ServerName, nameof(request.ServerName));
            if (!string.IsNullOrWhiteSpace(request.ServerAlias))
            {
                foreach (var alias in request.ServerAlias.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    SecurityValidator.ValidateHostnameOrThrow(alias, nameof(request.ServerAlias));
                }
            }

            _logger.LogInformation("Updating virtual host {VirtualHostId}", virtualHostId);

            var virtualHost = await _virtualHostRepository.GetByIdAsync(virtualHostId);
            if (virtualHost == null)
                throw new ArgumentException($"Virtual host with ID {virtualHostId} not found");

            // Check for port conflicts if port changed
            if (virtualHost.Port != request.Port)
            {
                await CheckPortConflictAsync(request.Port, virtualHost.ServiceId);
            }

            // Validate SSL configuration
            if (request.IsSSL && (string.IsNullOrEmpty(request.SSLCertPath) || string.IsNullOrEmpty(request.SSLKeyPath)))
                throw new InvalidOperationException("SSL is enabled but certificate paths are missing. Both SSLCertPath and SSLKeyPath are required when IsSSL is true.");

            // Update virtual host
            virtualHost.ServerName = request.ServerName;
            virtualHost.ServerAlias = request.ServerAlias;
            virtualHost.DocumentRoot = request.DocumentRoot;
            virtualHost.Port = request.Port;
            virtualHost.IsSSL = request.IsSSL;
            virtualHost.SSLCertPath = request.SSLCertPath;
            virtualHost.SSLKeyPath = request.SSLKeyPath;
            virtualHost.IsEnabled = request.IsEnabled;
            
            // Handle AdditionalConfiguration
            if (!string.IsNullOrEmpty(request.AdditionalConfiguration))
            {
                virtualHost.Configuration["AdditionalConfiguration"] = request.AdditionalConfiguration;
            }
            else if (virtualHost.Configuration.ContainsKey("AdditionalConfiguration"))
            {
                virtualHost.Configuration.Remove("AdditionalConfiguration");
            }
            virtualHost.UpdatedAt = DateTime.Now;

            await _virtualHostRepository.UpdateAsync(virtualHost);

            // Regenerate configuration file
            var service = await _serviceRepository.GetByIdAsync(virtualHost.ServiceId);
            if (service != null)
            {
                await GenerateVirtualHostConfigurationAsync(virtualHost, service);
            }

            _logger.LogInformation("Virtual host updated successfully");
            return virtualHost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update virtual host");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing virtual host (interface-compliant overload)
    /// </summary>
    public async Task<bool> UpdateVirtualHostAsync(VirtualHost virtualHost)
    {
        try
        {
            _logger.LogInformation("Updating virtual host {VirtualHostId}", virtualHost.Id);
            
            // Update the virtual host in the database
            var result = await _virtualHostRepository.UpdateAsync(virtualHost);
            
            if (result)
            {
                // Regenerate configuration file
                var service = await _serviceRepository.GetByIdAsync(virtualHost.ServiceId);
                if (service != null)
                {
                    await GenerateVirtualHostConfigurationAsync(virtualHost, service);
                }
                
                _logger.LogInformation("Virtual host updated successfully");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update virtual host");
            throw;
        }
    }

    /// <summary>
    /// Deletes a virtual host
    /// </summary>
    public async Task DeleteVirtualHostAsync(long virtualHostId)
    {
        try
        {
            _logger.LogInformation("Deleting virtual host {VirtualHostId}", virtualHostId);

            var virtualHost = await _virtualHostRepository.GetByIdAsync(virtualHostId);
            if (virtualHost == null)
                throw new ArgumentException($"Virtual host with ID {virtualHostId} not found");

            // Remove configuration file
            await RemoveVirtualHostConfigurationAsync(virtualHost);

            // Delete from database
            await _virtualHostRepository.DeleteAsync(virtualHostId);

            _logger.LogInformation("Virtual host deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete virtual host");
            throw;
        }
    }

    /// <summary>
    /// Gets all virtual hosts
    /// </summary>
    public async Task<List<VirtualHost>> GetAllVirtualHostsAsync()
    {
        try
        {
            _logger.LogInformation("=== VirtualHostManager.GetAllVirtualHostsAsync START ===");
            var result = await _virtualHostRepository.GetAllAsync();
            _logger.LogInformation("=== VirtualHostManager.GetAllVirtualHostsAsync END - Returning {Count} virtual hosts ===", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== VirtualHostManager.GetAllVirtualHostsAsync ERROR: {Message} ===", ex.Message);
            throw;
        }
    }


    /// <summary>
    /// Gets all virtual hosts for a service
    /// </summary>
    public async Task<List<VirtualHost>> GetVirtualHostsForServiceAsync(long serviceId)
    {
        return await _virtualHostRepository.GetByServiceIdAsync(serviceId);
    }

    /// <summary>
    /// Lists all virtual host domain names (for compatibility with GUI service)
    /// </summary>
    public async Task<List<string>> GetAllVirtualHostDomainsAsync()
    {
        try
        {
            var virtualHosts = await _virtualHostRepository.GetAllAsync();
            return virtualHosts.Select(vh => vh.ServerName).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing virtual host domain names");
            return new List<string>();
        }
    }

    /// <summary>
    /// Checks if a virtual host exists for the given domain (for compatibility with GUI service)
    /// </summary>
    public async Task<bool> VirtualHostExistsAsync(string domain)
    {
        try
        {
            var virtualHosts = await _virtualHostRepository.GetAllAsync();
            return virtualHosts.Any(vh => vh.ServerName.Equals(domain, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if virtual host exists for {Domain}", domain);
            return false;
        }
    }

    /// <summary>
    /// Gets the content of a virtual host configuration file (for compatibility with GUI service)
    /// </summary>
    public async Task<string?> GetVirtualHostContentAsync(string domain)
    {
        try
        {
            var virtualHosts = await _virtualHostRepository.GetAllAsync();
            var virtualHost = virtualHosts.FirstOrDefault(vh => vh.ServerName.Equals(domain, StringComparison.OrdinalIgnoreCase));
            
            if (virtualHost == null)
                return null;

            var service = await _serviceRepository.GetByIdAsync(virtualHost.ServiceId);
            if (service == null)
                return null;

            var configPath = GetVirtualHostConfigPath(service, virtualHost);
            
            if (File.Exists(configPath))
            {
                return await File.ReadAllTextAsync(configPath);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting virtual host content for {Domain}", domain);
            return null;
        }
    }

    /// <summary>
    /// Fixes existing virtual host files that may have FCGID directive issues (for compatibility with GUI service)
    /// </summary>
    public async Task<int> FixExistingVirtualHostFilesAsync()
    {
        try
        {
            _logger.LogInformation("Starting to fix existing virtual host files...");
            
            var virtualHosts = await _virtualHostRepository.GetAllAsync();
            var fixedCount = 0;

            foreach (var virtualHost in virtualHosts)
            {
                try
                {
                    var service = await _serviceRepository.GetByIdAsync(virtualHost.ServiceId);
                    if (service == null || service.ServiceType != ServiceType.Apache)
                        continue;

                    var configPath = GetVirtualHostConfigPath(service, virtualHost);
                    
                    if (File.Exists(configPath))
                    {
                        var content = await File.ReadAllTextAsync(configPath);
                        
                        // Check if file has FCGID directives without IfModule
                        if (content.Contains("FcgidMaxRequestLen") && !content.Contains("<IfModule mod_fcgid.c>"))
                        {
                            _logger.LogInformation("Fixing virtual host file: {ConfigPath}", configPath);
                            
                            // Fix FCGID directives by wrapping them in IfModule
                            var fixedContent = FixFCGIDDirectivesInVirtualHost(content);
                            
                            // Atomic write
                            var tempPath = configPath + ".tmp";
                            await File.WriteAllTextAsync(tempPath, fixedContent);
                            File.Move(tempPath, configPath, overwrite: true);
                            
                            fixedCount++;
                            _logger.LogInformation("Fixed virtual host file: {ConfigPath}", configPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fixing virtual host file for {ServerName}", virtualHost.ServerName);
                }
            }

            _logger.LogInformation("Fixed {Count} virtual host file(s)", fixedCount);
            return fixedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing existing virtual host files");
            return 0;
        }
    }

    /// <summary>
    /// Tests if Apache configuration is valid (for compatibility with GUI service)
    /// </summary>
    public async Task<bool> TestApacheConfigurationAsync()
    {
        try
        {
            var apacheService = await ApacheServiceHelper.FindApacheServiceAsync(_serviceRepository, _logger);
            
            if (apacheService == null)
            {
                _logger.LogWarning("No Apache service found for configuration test");
                return false;
            }

            var servicePath = apacheService.BinaryPath;
            if (string.IsNullOrEmpty(servicePath) || !Directory.Exists(servicePath))
            {
                _logger.LogWarning("Apache installation path not found for configuration test");
                return false;
            }

            return await ApacheServiceHelper.TestApacheConfigurationAsync(servicePath, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Apache configuration");
            return false;
        }
    }


    private async Task CheckPortConflictAsync(int port, long serviceId)
    {
        var existingVirtualHosts = await _virtualHostRepository.GetByServiceIdAsync(serviceId);
        var conflict = existingVirtualHosts.FirstOrDefault(vh => vh.Port == port && vh.IsEnabled);
        
        if (conflict != null)
        {
            throw new InvalidOperationException($"Port {port} is already in use by virtual host '{conflict.ServerName}'. Please choose a different port or disable the existing virtual host.");
        }
    }

    private async Task GenerateVirtualHostConfigurationAsync(VirtualHost virtualHost, ServiceDefinition service)
    {
        try
        {
            // For Apache, ensure required modules are enabled before generating config
            if (service.ServiceType == ServiceType.Apache)
            {
                await EnsureRequiredApacheModulesAsync(service);
            }

            var configContent = service.ServiceType switch
            {
                ServiceType.Apache => GenerateApacheVirtualHostConfig(virtualHost),
                ServiceType.Nginx => GenerateNginxVirtualHostConfig(virtualHost),
                _ => throw new NotSupportedException($"Service type {service.ServiceType} not supported for virtual hosts")
            };

            var configPath = GetVirtualHostConfigPath(service, virtualHost);
            
            // Atomic file write: write to temp file first, then replace
            var tempPath = configPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, configContent);
            File.Move(tempPath, configPath, overwrite: true);

            _logger.LogInformation("Generated virtual host configuration at {ConfigPath}", configPath);
            
            // Reload the web server configuration
            await ReloadWebServerConfigAsync(service);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate virtual host configuration");
            throw;
        }
    }

    private async Task RemoveVirtualHostConfigurationAsync(VirtualHost virtualHost)
    {
        try
        {
            var service = await _serviceRepository.GetByIdAsync(virtualHost.ServiceId);
            if (service == null) return;

            var configPath = GetVirtualHostConfigPath(service, virtualHost);
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
                _logger.LogInformation("Removed virtual host configuration at {ConfigPath}", configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove virtual host configuration");
        }
    }

    private string GenerateApacheVirtualHostConfig(VirtualHost virtualHost)
    {
        var config = new StringBuilder();
        
        if (virtualHost.IsSSL)
        {
            config.AppendLine($"<VirtualHost *:443>");
            config.AppendLine($"    ServerName {virtualHost.ServerName}");
            if (!string.IsNullOrEmpty(virtualHost.ServerAlias))
            {
                config.AppendLine($"    ServerAlias {virtualHost.ServerAlias}");
            }
            config.AppendLine($"    DocumentRoot \"{virtualHost.DocumentRoot}\"");
            config.AppendLine($"    # SSL Configuration (only if mod_ssl is loaded)");
            config.AppendLine($"    <IfModule mod_ssl.c>");
            config.AppendLine($"        SSLEngine on");
            config.AppendLine($"        SSLCertificateFile \"{virtualHost.SSLCertPath}\"");
            config.AppendLine($"        SSLCertificateKeyFile \"{virtualHost.SSLKeyPath}\"");
            config.AppendLine($"    </IfModule>");
        }
        else
        {
            config.AppendLine($"<VirtualHost *:{virtualHost.Port}>");
            config.AppendLine($"    ServerName {virtualHost.ServerName}");
            if (!string.IsNullOrEmpty(virtualHost.ServerAlias))
            {
                config.AppendLine($"    ServerAlias {virtualHost.ServerAlias}");
            }
            config.AppendLine($"    DocumentRoot \"{virtualHost.DocumentRoot}\"");
        }

        config.AppendLine($"    <Directory \"{virtualHost.DocumentRoot}\">");
        config.AppendLine($"        AllowOverride All");
        config.AppendLine($"        Require all granted");
        config.AppendLine($"    </Directory>");

        // Add FCGID configuration for PHP
        config.AppendLine($"    <IfModule mod_fcgid.c>");
        
        // Use PHP wrapper from FCGID configuration if available
        if (virtualHost.FCGID != null && !string.IsNullOrEmpty(virtualHost.FCGID.PHPWrapper))
        {
            config.AppendLine($"        FcgidWrapper \"{virtualHost.FCGID.PHPWrapper}\" .php");
            config.AppendLine($"        AddHandler fcgid-script .php");
            config.AppendLine($"        FcgidMaxRequestLen {virtualHost.FCGID.MaxRequestLen}");
            config.AppendLine($"        FcgidConnectTimeout {virtualHost.FCGID.ConnectTimeout}");
            config.AppendLine($"        FcgidIOTimeout {virtualHost.FCGID.IOTimeout}");
            config.AppendLine($"        FcgidBusyTimeout {virtualHost.FCGID.BusyTimeout}");
            config.AppendLine($"        FcgidIdleTimeout {virtualHost.FCGID.IdleTimeout}");
            config.AppendLine($"        FcgidProcessLifeTime {virtualHost.FCGID.ProcessLifeTime}");
        }
        else
        {
            // Default FCGID configuration without specific PHP wrapper
            config.AppendLine($"        FcgidMaxRequestLen 1073741824");
            config.AppendLine($"        FcgidConnectTimeout 20");
            config.AppendLine($"        FcgidIOTimeout 20");
            config.AppendLine($"        FcgidBusyTimeout 20");
        }
        
        config.AppendLine($"    </IfModule>");

        // Add additional configuration if provided
        if (virtualHost.Configuration.Count > 0)
        {
            foreach (var kvp in virtualHost.Configuration)
            {
                if (kvp.Key == "AdditionalConfiguration")
                {
                    // Add raw configuration lines
                    var additionalLines = kvp.Value.ToString()?.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (additionalLines != null)
                    {
                        foreach (var line in additionalLines)
                        {
                            config.AppendLine($"    {line.Trim()}");
                        }
                    }
                }
                else
                {
                    config.AppendLine($"    {kvp.Key} {kvp.Value}");
                }
            }
        }

        config.AppendLine($"</VirtualHost>");

        // Add HTTP to HTTPS redirect if SSL is enabled
        if (virtualHost.IsSSL)
        {
            config.AppendLine();
            config.AppendLine($"<VirtualHost *:80>");
            config.AppendLine($"    ServerName {virtualHost.ServerName}");
            if (!string.IsNullOrEmpty(virtualHost.ServerAlias))
            {
                config.AppendLine($"    ServerAlias {virtualHost.ServerAlias}");
            }
            config.AppendLine($"    Redirect permanent / https://{virtualHost.ServerName}/");
            config.AppendLine($"</VirtualHost>");
        }

        return config.ToString();
    }

    private string GenerateNginxVirtualHostConfig(VirtualHost virtualHost)
    {
        var config = new StringBuilder();
        
        if (virtualHost.IsSSL)
        {
            config.AppendLine($"server {{");
            config.AppendLine($"    listen 443 ssl http2;");
            config.AppendLine($"    server_name {virtualHost.ServerName}");
            if (!string.IsNullOrEmpty(virtualHost.ServerAlias))
            {
                config.AppendLine($"                 {virtualHost.ServerAlias}");
            }
            config.AppendLine($"    ;");
            config.AppendLine($"    root {virtualHost.DocumentRoot};");
            config.AppendLine($"    index index.php index.html index.htm;");
            config.AppendLine($"    ssl_certificate \"{virtualHost.SSLCertPath}\";");
            config.AppendLine($"    ssl_certificate_key \"{virtualHost.SSLKeyPath}\";");
        }
        else
        {
            config.AppendLine($"server {{");
            config.AppendLine($"    listen {virtualHost.Port};");
            config.AppendLine($"    server_name {virtualHost.ServerName}");
            if (!string.IsNullOrEmpty(virtualHost.ServerAlias))
            {
                config.AppendLine($"                 {virtualHost.ServerAlias}");
            }
            config.AppendLine($"    ;");
            config.AppendLine($"    root {virtualHost.DocumentRoot};");
            config.AppendLine($"    index index.php index.html index.htm;");
        }

        config.AppendLine($"    location / {{");
        config.AppendLine($"        try_files $uri $uri/ /index.php?$query_string;");
        config.AppendLine($"    }}");

        config.AppendLine($"    location ~ \\.php$ {{");
        config.AppendLine($"        fastcgi_pass 127.0.0.1:9000;");
        config.AppendLine($"        fastcgi_index index.php;");
        config.AppendLine($"        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;");
        config.AppendLine($"        include fastcgi_params;");
        config.AppendLine($"    }}");

        // Add additional configuration if provided
        if (virtualHost.Configuration.Count > 0)
        {
            foreach (var kvp in virtualHost.Configuration)
            {
                if (kvp.Key == "AdditionalConfiguration")
                {
                    // Add raw configuration lines
                    var additionalLines = kvp.Value.ToString()?.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (additionalLines != null)
                    {
                        foreach (var line in additionalLines)
                        {
                            config.AppendLine($"    {line.Trim()}");
                        }
                    }
                }
                else
                {
                    config.AppendLine($"    {kvp.Key} {kvp.Value};");
                }
            }
        }

        config.AppendLine($"}}");

        // Add HTTP to HTTPS redirect if SSL is enabled
        if (virtualHost.IsSSL)
        {
            config.AppendLine();
            config.AppendLine($"server {{");
            config.AppendLine($"    listen 80;");
            config.AppendLine($"    server_name {virtualHost.ServerName}");
            if (!string.IsNullOrEmpty(virtualHost.ServerAlias))
            {
                config.AppendLine($"                 {virtualHost.ServerAlias}");
            }
            config.AppendLine($"    ;");
            config.AppendLine($"    return 301 https://$server_name$request_uri;");
            config.AppendLine($"}}");
        }

        return config.ToString();
    }

    private string GetVirtualHostConfigPath(ServiceDefinition service, VirtualHost virtualHost)
    {
                    var servicePath = Path.GetDirectoryName(service.BinaryPath) ?? "";
        var configDir = service.ServiceType switch
        {
            ServiceType.Apache => Path.Combine(servicePath, "conf", "extra"),
            ServiceType.Nginx => Path.Combine(servicePath, "conf", "conf.d"),
            _ => throw new NotSupportedException($"Service type {service.ServiceType} not supported")
        };

        Directory.CreateDirectory(configDir);
        return Path.Combine(configDir, $"{virtualHost.ServerName.Replace(".", "_")}.conf");
    }

    /// <summary>
    /// Reloads the web server configuration after changes
    /// </summary>
    private async Task ReloadWebServerConfigAsync(ServiceDefinition service)
    {
        try
        {
            var servicePath = service.BinaryPath;
            if (string.IsNullOrEmpty(servicePath) || !Directory.Exists(servicePath))
            {
                _logger.LogWarning("Service path not found for {ServiceName}, skipping config reload", service.Name);
                return;
            }

            switch (service.ServiceType)
            {
                case ServiceType.Apache:
                    await ReloadApacheConfigAsync(servicePath);
                    break;
                case ServiceType.Nginx:
                    await ReloadNginxConfigAsync(servicePath);
                    break;
                default:
                    _logger.LogWarning("Config reload not supported for service type {ServiceType}", service.ServiceType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload web server configuration for {ServiceName}", service.Name);
            // Don't throw - config reload failure shouldn't break the main operation
        }
    }

    /// <summary>
    /// Reloads Apache configuration using graceful restart
    /// </summary>
    private async Task ReloadApacheConfigAsync(string servicePath)
    {
        try
        {
            var httpdPath = Path.Combine(servicePath, "bin", "httpd.exe");
            if (!File.Exists(httpdPath))
            {
                _logger.LogWarning("Apache httpd.exe not found at {HttpdPath}, skipping graceful reload", httpdPath);
                return;
            }

            // Test configuration first using ApacheServiceHelper
            var configValid = await ApacheServiceHelper.TestApacheConfigurationAsync(servicePath, _logger);
            if (!configValid)
            {
                _logger.LogError("Apache configuration test failed - skipping reload");
                return; // Don't reload if config is invalid
            }

            // Graceful reload using Windows service commands
            var reloadProcess = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = "stop Apache2.4 && net start Apache2.4",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var reloadProc = Process.Start(reloadProcess);
            if (reloadProc != null)
            {
                await reloadProc.WaitForExitAsync();
                if (reloadProc.ExitCode == 0)
                {
                    _logger.LogInformation("Apache configuration reloaded successfully");
                }
                else
                {
                    var error = await reloadProc.StandardError.ReadToEndAsync();
                    _logger.LogWarning("Apache reload completed with warnings: {Error}", error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload Apache configuration");
        }
    }

    /// <summary>
    /// Reloads Nginx configuration
    /// </summary>
    private async Task ReloadNginxConfigAsync(string servicePath)
    {
        try
        {
            var nginxPath = Path.Combine(servicePath, "nginx.exe");
            if (!File.Exists(nginxPath))
            {
                _logger.LogWarning("Nginx executable not found at {NginxPath}, skipping reload", nginxPath);
                return;
            }

            // Test configuration first
            var testProcess = new ProcessStartInfo
            {
                FileName = nginxPath,
                Arguments = "-t",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var testProc = Process.Start(testProcess);
            if (testProc != null)
            {
                await testProc.WaitForExitAsync();
                if (testProc.ExitCode != 0)
                {
                    var error = await testProc.StandardError.ReadToEndAsync();
                    _logger.LogError("Nginx configuration test failed: {Error}", error);
                    return; // Don't reload if config is invalid
                }
            }

            // Reload configuration
            var reloadProcess = new ProcessStartInfo
            {
                FileName = nginxPath,
                Arguments = "-s reload",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var reloadProc = Process.Start(reloadProcess);
            if (reloadProc != null)
            {
                await reloadProc.WaitForExitAsync();
                if (reloadProc.ExitCode == 0)
                {
                    _logger.LogInformation("Nginx configuration reloaded successfully");
                }
                else
                {
                    var error = await reloadProc.StandardError.ReadToEndAsync();
                    _logger.LogWarning("Nginx reload completed with warnings: {Error}", error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload Nginx configuration");
        }
    }

    /// <summary>
    /// Fixes FCGID directives in virtual host content by wrapping them in IfModule blocks
    /// </summary>
    private string FixFCGIDDirectivesInVirtualHost(string content)
    {
        return ApacheServiceHelper.FixFCGIDDirectivesInVirtualHost(content);
    }

    /// <summary>
    /// Ensures required Apache modules are enabled for virtual host functionality
    /// </summary>
    private async Task EnsureRequiredApacheModulesAsync(ServiceDefinition service)
    {
        try
        {
            _logger.LogInformation("Ensuring required Apache modules are enabled");

            var servicePath = service.BinaryPath;
            var httpdConfPath = Path.Combine(servicePath, "conf", "httpd.conf");
            
            if (!File.Exists(httpdConfPath))
            {
                _logger.LogWarning("Apache httpd.conf not found at {HttpdConfPath}, skipping module check", httpdConfPath);
                return;
            }

            // Read current configuration
            var httpdConfContent = await File.ReadAllTextAsync(httpdConfPath);
            
            // Required modules for virtual host generation
            var requiredModules = new Dictionary<string, string>
            {
                { "mod_fcgid", "fcgid_module" },
                { "mod_ssl", "ssl_module" },
                { "mod_rewrite", "rewrite_module" },
                { "mod_headers", "headers_module" }
            };

            var modulesToEnable = new List<string>();
            var configUpdated = false;

            foreach (var module in requiredModules)
            {
                var moduleName = module.Value;
                if (!httpdConfContent.Contains($"LoadModule {moduleName}"))
                {
                    modulesToEnable.Add(moduleName);
                    _logger.LogInformation("Module {ModuleName} needs to be enabled", moduleName);
                }
            }

            // Enable missing modules
            if (modulesToEnable.Count > 0)
            {
                foreach (var moduleName in modulesToEnable)
                {
                    var moduleFile = GetModuleFileName(moduleName);
                    if (!string.IsNullOrEmpty(moduleFile))
                    {
                        var modulesDir = Path.Combine(servicePath, "modules");
                        var fullModulePath = Path.Combine(modulesDir, moduleFile);
                        
                        if (File.Exists(fullModulePath))
                        {
                            var loadModuleLine = $"LoadModule {moduleName} \"{fullModulePath}\"";
                            httpdConfContent += $"\n{loadModuleLine}";
                            configUpdated = true;
                            _logger.LogInformation("Added {LoadModuleLine} to httpd.conf", loadModuleLine);
                        }
                        else
                        {
                            _logger.LogWarning("Module file not found: {FullModulePath}", fullModulePath);
                        }
                    }
                }

                if (configUpdated)
                {
                    // Write updated configuration
                    await File.WriteAllTextAsync(httpdConfPath, httpdConfContent);
                    _logger.LogInformation("Updated httpd.conf with required modules");
                }
            }
            else
            {
                _logger.LogInformation("All required Apache modules are already enabled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring required Apache modules are enabled");
            // Don't throw - this is not critical for virtual host generation
        }
    }

    /// <summary>
    /// Maps Apache module names to their file names
    /// </summary>
    private string GetModuleFileName(string moduleName)
    {
        var moduleFileMap = new Dictionary<string, string>
        {
            { "fcgid_module", "mod_fcgid.so" },
            { "ssl_module", "mod_ssl.so" },
            { "rewrite_module", "mod_rewrite.so" },
            { "headers_module", "mod_headers.so" }
        };

        return moduleFileMap.TryGetValue(moduleName, out var fileName) ? fileName : string.Empty;
    }
}
