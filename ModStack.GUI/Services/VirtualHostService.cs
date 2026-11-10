using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModStack.Core.Services;
using ModStack.Data.Interfaces;
using ModStack.Common;
using ModStack.Common.Interfaces;
using ModStack.Common.Security;
using ModStack.Common.Services;
using ModStack.Common.Models;

namespace ModStack.GUI.Services;

/// <summary>
/// Service for managing Apache virtual host configuration
/// </summary>
public partial class VirtualHostService(
    IPathConfigurationService pathConfigurationService,
    IServiceService serviceService,
    ApacheHttpdConfigurator apacheConfigurator,
    IProjectServiceRepository projectServiceRepository,
    IProjectRepository projectRepository,
    IStackServiceRepository stackServiceRepository,
    ISSLCertificateService sslCertificateService,
    ServiceControlManager serviceControlManager,
    ILogger<VirtualHostService> logger) : IVirtualHostService
{
    private readonly IPathConfigurationService _pathConfigurationService = pathConfigurationService;
    private readonly IServiceService _serviceService = serviceService;
    private readonly IProjectServiceRepository _projectServiceRepository = projectServiceRepository;
    private readonly IProjectRepository _projectRepository = projectRepository;
    private readonly IStackServiceRepository _stackServiceRepository = stackServiceRepository;
    private readonly ISSLCertificateService _sslCertificateService = sslCertificateService;
    private readonly ServiceControlManager _serviceControlManager = serviceControlManager;
    private readonly ILogger<VirtualHostService> _logger = logger;
    private readonly ApacheHttpdConfigurator _apacheConfigurator = apacheConfigurator;

    [System.Text.RegularExpressions.GeneratedRegex(@"ServerName\s+(\S+)")]
    private static partial System.Text.RegularExpressions.Regex ServerNamePattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"DocumentRoot\s+""([^""]+)""")]
    private static partial System.Text.RegularExpressions.Regex DocumentRootPattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"<VirtualHost[^>]*:(\d+)>")]
    private static partial System.Text.RegularExpressions.Regex VirtualHostPortPattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"ServerAlias\s+(\S+)")]
    private static partial System.Text.RegularExpressions.Regex ServerAliasPattern();
    
    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*SSLSessionCache.*$", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial System.Text.RegularExpressions.Regex SslSessionCacheRegex();
    
    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*SSLSessionCacheTimeout.*$", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial System.Text.RegularExpressions.Regex SslSessionCacheTimeoutRegex();

    public async Task<VirtualHostResult> CreateVirtualHostAsync(
        string domain, 
        string projectPath, 
        List<string>? serverAliases = null, 
        int port = 80, 
        bool enableSSL = false, 
        long? projectId = null)
    {
        try
        {
            SecurityValidator.ValidateHostnameOrThrow(domain, nameof(domain));
            if (serverAliases != null)
            {
                foreach (var alias in serverAliases.Where(a => !string.IsNullOrWhiteSpace(a)))
                {
                    SecurityValidator.ValidateHostnameOrThrow(alias, nameof(serverAliases));
                }
            }

            LogService.Info($"Creating virtual host for domain: {domain}");

            // Find Apache installation
            var apachePath = await FindApacheInstallationPathAsync();
            if (string.IsNullOrEmpty(apachePath))
            {
                return new VirtualHostResult
                {
                    Success = false,
                    Message = "Apache installation not found"
                };
            }

            LogService.Info($"Apache installation found at: {apachePath}");

            // Ensure required modules are configured
            await _apacheConfigurator.EnsureModulesAsync(apachePath, enableSSL);

            // Create vhosts directory
            var vhostsDir = Path.Combine(apachePath, "conf", "vhosts");
            Directory.CreateDirectory(vhostsDir);
            
            LogService.Info($"Virtual hosts directory: {vhostsDir}");

            // Determine document root
            var documentRoot = DetermineDocumentRoot(projectPath);
            
            LogService.Info($"Document root for virtual host: {documentRoot}");
            
            // Ensure index file exists
            await EnsureIndexFileExistsAsync(documentRoot, domain);

            // Generate virtual host configuration
            var vhostContent = await GenerateVirtualHostContentAsync(
                domain, documentRoot, serverAliases, port, enableSSL, projectId);

            // Write virtual host file inside validated vhosts directory
            var vhostFile = SecurityValidator.ValidateSafePathWithinRoot(vhostsDir, $"{domain}.vhost");
            await File.WriteAllTextAsync(vhostFile, vhostContent);

            LogService.Info($"Virtual host file written to: {vhostFile}");

            var result = new VirtualHostResult
            {
                Success = true,
                Message = $"Virtual host created successfully for {domain}",
                VirtualHostFilePath = vhostFile
            };

            // Generate SSL certificate if needed
            if (enableSSL)
            {
                var (certificatePath, keyPath) = await GenerateSSLCertificateAsync(domain, vhostsDir);
                result.SSLCertificatePath = certificatePath;
                result.SSLKeyPath = keyPath;
            }

            // Ensure Apache includes vhosts
            LogService.Info($"Ensuring Apache includes vhosts for: {apachePath}");
            await EnsureApacheIncludesVhostsAsync(apachePath);
            LogService.Info($"Apache vhosts configuration completed");

            // Test and validate Apache configuration synchronously before reporting success
            LogService.Info($"Testing Apache configuration at: {apachePath}");
            var (isConfigValid, validationDetails) = await TestAndRestartApacheAsync(apachePath);
            result.ConfigurationValidated = isConfigValid;

            if (!isConfigValid)
            {
                result.Success = false;
                result.Message = $"Virtual host configuration saved for {domain}, but Apache validation failed: {validationDetails}";
                LogService.Warning(result.Message);
                return result;
            }

            LogService.Info($"Virtual host created and validated successfully for {domain}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating virtual host for {Domain}", domain);
            return new VirtualHostResult
            {
                Success = false,
                Message = $"Error creating virtual host: {ex.Message}"
            };
        }
    }

    private async Task<string?> GetPhpWrapperPathAsync(long? projectId)
    {
        try
        {
            ServiceDefinition? phpService = null;

            if (projectId.HasValue)
            {
                var project = await _projectRepository.GetByIdAsync(projectId.Value);

                if (project?.StackId.HasValue == true)
                {
                    var stackServices = await _stackServiceRepository.GetByStackIdAsync(project.StackId.Value);
                    var stackPhp = stackServices.FirstOrDefault(ss => ss.IsEnabled && ss.Service?.ServiceType == ServiceType.PHP);
                    if (stackPhp != null)
                    {
                        phpService = await _serviceService.GetServiceByIdAsync(stackPhp.ServiceId);
                    }
                }

                if (phpService is null)
                {
                    var projectServices = await _projectServiceRepository.GetByProjectIdAsync(projectId.Value);
                    var projectPhp = projectServices.FirstOrDefault(ps => ps.IsEnabled && ps.Service?.ServiceType == ServiceType.PHP);
                    if (projectPhp != null)
                    {
                        phpService = await _serviceService.GetServiceByIdAsync(projectPhp.ServiceId);
                    }
                }
            }

            if (phpService is null)
            {
                var phpServices = await _serviceService.GetServicesByTypeAsync(ServiceType.PHP);
                phpService = phpServices.FirstOrDefault(s => s.IsInstalled) ?? phpServices.FirstOrDefault();
            }

            string? wrapperPath = null;

            if (phpService is not null)
            {
                wrapperPath = FindPhpExecutablePath(phpService.BinaryPath);
                if (string.IsNullOrEmpty(wrapperPath) && !string.IsNullOrEmpty(phpService.Configuration))
                {
                    try
                    {
                        var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(phpService.Configuration);
                        if (config != null && config.TryGetValue("BinaryPath", out var binaryPathValue))
                        {
                            wrapperPath = FindPhpExecutablePath(binaryPathValue?.ToString());
                        }
                    }
                    catch
                    {
                        // ignore configuration parsing errors
                    }
                }
            }

            if (string.IsNullOrEmpty(wrapperPath))
            {
                var defaultPhpPath = _pathConfigurationService.GetServicePath("PHP");
                wrapperPath = FindPhpExecutablePath(defaultPhpPath);
            }

            return string.IsNullOrEmpty(wrapperPath) ? null : wrapperPath.Replace("\\", "/");
        }
        catch (Exception ex)
        {
            LogService.Warning($"Failed to locate PHP wrapper executable: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses a PHP size string (e.g., "512M", "1G", "128K") to bytes.
    /// Returns a default value if parsing fails.
    /// </summary>
    internal static long ParsePhpSizeToBytes(string? sizeStr, long defaultValue = 1073741824)
    {
        if (string.IsNullOrWhiteSpace(sizeStr))
            return defaultValue;

        sizeStr = sizeStr.Trim();

        // Handle pure numeric values (bytes)
        if (long.TryParse(sizeStr, out var pureBytes))
            return pureBytes;

        // Extract numeric part and suffix
        var suffix = char.ToUpperInvariant(sizeStr[^1]);
        var numericPart = sizeStr[..^1].Trim();

        if (!double.TryParse(numericPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numericValue))
            return defaultValue;

        return suffix switch
        {
            'K' => (long)(numericValue * 1024),
            'M' => (long)(numericValue * 1024 * 1024),
            'G' => (long)(numericValue * 1024 * 1024 * 1024),
            _ => defaultValue
        };
    }

    /// <summary>
    /// Resolves the FcgidMaxRequestLen value from the PHP service's configuration
    /// bound to the given project. Uses max(post_max_size, upload_max_filesize)
    /// with a buffer, minimum 1GB.
    /// </summary>
    private async Task<long> GetFcgidMaxRequestLenFromPhpConfigAsync(long? projectId)
    {
        const long defaultMaxRequestLen = 1073741824; // 1GB

        try
        {
            ServiceDefinition? phpService = null;

            if (projectId.HasValue)
            {
                var project = await _projectRepository.GetByIdAsync(projectId.Value);

                if (project?.StackId.HasValue == true)
                {
                    var stackServices = await _stackServiceRepository.GetByStackIdAsync(project.StackId.Value);
                    var stackPhp = stackServices.FirstOrDefault(ss => ss.IsEnabled && ss.Service?.ServiceType == ServiceType.PHP);
                    if (stackPhp != null)
                    {
                        phpService = await _serviceService.GetServiceByIdAsync(stackPhp.ServiceId);
                    }
                }

                if (phpService is null)
                {
                    var projectServices = await _projectServiceRepository.GetByProjectIdAsync(projectId.Value);
                    var projectPhp = projectServices.FirstOrDefault(ps => ps.IsEnabled && ps.Service?.ServiceType == ServiceType.PHP);
                    if (projectPhp != null)
                    {
                        phpService = await _serviceService.GetServiceByIdAsync(projectPhp.ServiceId);
                    }
                }
            }

            if (phpService is null)
            {
                var phpServices = await _serviceService.GetServicesByTypeAsync(ServiceType.PHP);
                phpService = phpServices.FirstOrDefault(s => s.IsInstalled) ?? phpServices.FirstOrDefault();
            }

            return ComputeFcgidMaxRequestLenFromServiceConfig(phpService?.Configuration, defaultMaxRequestLen);
        }
        catch (Exception ex)
        {
            LogService.Warning($"Failed to resolve FcgidMaxRequestLen from PHP config: {ex.Message}");
            return defaultMaxRequestLen;
        }
    }

    /// <summary>
    /// Computes FcgidMaxRequestLen from a PHP service's JSON configuration string.
    /// Returns max(post_max_size, upload_max_filesize) + 10% buffer, minimum 1GB.
    /// </summary>
    internal static long ComputeFcgidMaxRequestLenFromServiceConfig(string? configurationJson, long defaultValue = 1073741824)
    {
        if (string.IsNullOrEmpty(configurationJson))
            return defaultValue;

        try
        {
            var configDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(configurationJson);
            if (configDict == null)
                return defaultValue;

            long postMaxSize = 0;
            long uploadMaxFilesize = 0;

            if (configDict.TryGetValue("post_max_size", out var postMaxSizeEl) && postMaxSizeEl.ValueKind == System.Text.Json.JsonValueKind.String)
                postMaxSize = ParsePhpSizeToBytes(postMaxSizeEl.GetString(), 0);

            if (configDict.TryGetValue("upload_max_filesize", out var uploadMaxEl) && uploadMaxEl.ValueKind == System.Text.Json.JsonValueKind.String)
                uploadMaxFilesize = ParsePhpSizeToBytes(uploadMaxEl.GetString(), 0);

            var maxSize = Math.Max(postMaxSize, uploadMaxFilesize);
            if (maxSize <= 0)
                return defaultValue;

            // Add 10% buffer for POST overhead (multipart boundaries, etc.)
            // We do not enforce a minimum here so that the vhost accurately reflects the PHP configuration limits + buffer.
            var withBuffer = (long)(maxSize * 1.1);

            return withBuffer;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Updates FcgidMaxRequestLen in all vhost files that reference the given PHP service's php-cgi.exe.
    /// Called when PHP configuration (post_max_size, upload_max_filesize) changes.
    /// </summary>
    public async Task<int> UpdateVhostFcgidMaxRequestLenForPhpServiceAsync(ServiceDefinition phpService)
    {
        try
        {
            var apachePath = await FindApacheInstallationPathAsync();
            if (string.IsNullOrEmpty(apachePath))
                return 0;

            var vhostsDir = Path.Combine(apachePath, "conf", "vhosts");
            if (!Directory.Exists(vhostsDir))
                return 0;

            // Determine the php-cgi.exe path for this PHP service
            var phpCgiPath = FindPhpExecutablePath(phpService.BinaryPath);
            if (string.IsNullOrEmpty(phpCgiPath))
                return 0;

            // Normalize for comparison (vhost files use forward slashes)
            var normalizedPhpCgiPath = phpCgiPath.Replace("\\", "/");

            // Compute the new FcgidMaxRequestLen from this PHP service's config
            var newMaxRequestLen = ComputeFcgidMaxRequestLenFromServiceConfig(phpService.Configuration);

            var vhostFiles = Directory.GetFiles(vhostsDir, "*.vhost");
            var updatedCount = 0;

            foreach (var vhostFile in vhostFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(vhostFile);

                    // Check if this vhost uses this PHP service (match the php-cgi.exe path)
                    if (!content.Contains(normalizedPhpCgiPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Replace FcgidMaxRequestLen values
                    var updatedContent = Regex.Replace(
                        content,
                        @"(FcgidMaxRequestLen\s+)\d+",
                        $"${{1}}{newMaxRequestLen}");

                    if (updatedContent != content)
                    {
                        await File.WriteAllTextAsync(vhostFile, updatedContent);
                        updatedCount++;
                        LogService.Info($"Updated FcgidMaxRequestLen to {newMaxRequestLen} in: {Path.GetFileName(vhostFile)}");
                    }
                }
                catch (Exception ex)
                {
                    LogService.Warning($"Error updating FcgidMaxRequestLen in {Path.GetFileName(vhostFile)}: {ex.Message}");
                }
            }

            if (updatedCount > 0)
            {
                LogService.Info($"Updated FcgidMaxRequestLen in {updatedCount} vhost file(s) for PHP service '{phpService.Name}'");
            }

            return updatedCount;
        }
        catch (Exception ex)
        {
            LogService.Warning($"Error updating vhost FcgidMaxRequestLen for PHP service: {ex.Message}");
            return 0;
        }
    }

    private static string? FindPhpExecutablePath(string? basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return null;
        }

        try
        {
            var path = basePath;

            if (File.Exists(path))
            {
                var fileName = Path.GetFileName(path);
                if (string.Equals(fileName, "php-cgi.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "php.exe", StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }

                var parentDirectory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parentDirectory))
                {
                    path = parentDirectory;
                }
            }

            if (!Directory.Exists(path))
            {
                return null;
            }

            var directPhpCgi = Path.Combine(path, "php-cgi.exe");
            if (File.Exists(directPhpCgi))
            {
                return directPhpCgi;
            }

            var recursivePhpCgi = Directory.GetFiles(path, "php-cgi.exe", SearchOption.AllDirectories)
                .OrderBy(p => p.Length)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(recursivePhpCgi))
            {
                return recursivePhpCgi;
            }

            var directPhpExe = Path.Combine(path, "php.exe");
            if (File.Exists(directPhpExe))
            {
                return directPhpExe;
            }

            var recursivePhpExe = Directory.GetFiles(path, "php.exe", SearchOption.AllDirectories)
                .OrderBy(p => p.Length)
                .FirstOrDefault();
            return recursivePhpExe;
        }
        catch (Exception ex)
        {
            LogService.Warning($"Error searching for PHP executable in '{basePath}': {ex.Message}");
            return null;
        }
    }

    public async Task<bool> RemoveVirtualHostAsync(string domain)
    {
        try
        {
            SecurityValidator.ValidateHostnameOrThrow(domain, nameof(domain));
            _logger.LogInformation("Removing virtual host for domain: {Domain}", domain);

            var apachePath = await FindApacheInstallationPathAsync();
            if (string.IsNullOrEmpty(apachePath))
            {
                _logger.LogWarning("Apache installation not found");
                return false;
            }

            var vhostsDir = Path.Combine(apachePath, "conf", "vhosts");
            var vhostFile = SecurityValidator.ValidateSafePathWithinRoot(vhostsDir, $"{domain}.vhost");
            if (File.Exists(vhostFile))
            {
                File.Delete(vhostFile);
                _logger.LogInformation("Removed virtual host file: {VhostFile}", vhostFile);
                
                // Restart Apache
                var (isValid, _) = await TestAndRestartApacheAsync(apachePath);
                return isValid;
            }

            _logger.LogWarning("Virtual host file not found for domain: {Domain}", domain);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing virtual host for {Domain}", domain);
            return false;
        }
    }

    public async Task<VirtualHostValidationResult> ValidateVirtualHostAsync(string domain, string projectPath)
    {
        var result = new VirtualHostValidationResult { IsValid = true };

        try
        {
            // Validate domain using authoritative RFC 1123 hostname validator
            if (string.IsNullOrWhiteSpace(domain))
            {
                result.Errors.Add("Domain is required");
                result.IsValid = false;
            }
            else if (!SecurityValidator.IsValidHostname(domain))
            {
                result.Errors.Add("Invalid domain format. Domain must be a valid RFC-compliant hostname (e.g., myapp.local).");
                result.IsValid = false;
            }

            // Validate project path
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                result.Errors.Add("Project path is required");
                result.IsValid = false;
            }
            else if (!Directory.Exists(projectPath))
            {
                result.Warnings.Add("Project directory does not exist and will be created");
            }

            // Check for existing virtual host only when domain is valid
            if (result.IsValid)
            {
                var apachePath = await FindApacheInstallationPathAsync();
                if (!string.IsNullOrEmpty(apachePath))
                {
                    var vhostsDir = Path.Combine(apachePath, "conf", "vhosts");
                    var vhostFile = SecurityValidator.ValidateSafePathWithinRoot(vhostsDir, $"{domain}.vhost");
                    if (File.Exists(vhostFile))
                    {
                        result.Warnings.Add($"Virtual host for {domain} already exists and will be overwritten");
                    }
                }
            }

            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating virtual host for {Domain}", domain);
            result.Errors.Add($"Validation error: {ex.Message}");
            result.IsValid = false;
            return result;
        }
    }

    public async Task<IEnumerable<VirtualHostInfo>> GetVirtualHostsAsync()
    {
        try
        {
            var virtualHosts = new List<VirtualHostInfo>();
            var apachePath = await FindApacheInstallationPathAsync();
            
            if (string.IsNullOrEmpty(apachePath))
                return virtualHosts;

            var vhostsDir = Path.Combine(apachePath, "conf", "vhosts");
            if (!Directory.Exists(vhostsDir))
                return virtualHosts;

            var vhostFiles = Directory.GetFiles(vhostsDir, "*.vhost");
            
            foreach (var file in vhostFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var vhostInfo = ParseVirtualHostContent(content, file);
                    if (vhostInfo != null)
                    {
                        virtualHosts.Add(vhostInfo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing virtual host file: {File}", file);
                }
            }

            return virtualHosts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting virtual hosts");
            return [];
        }
    }

    #region Private Methods

    private async Task<string> FindApacheInstallationPathAsync()
    {
        try
        {
            // Get Apache service from database instead of trying to discover it
            var apacheServices = await _serviceService.GetServicesByTypeAsync(ModStack.Common.ServiceType.Apache);
            var installedApache = apacheServices.FirstOrDefault(s => s.IsInstalled);
            
            if (installedApache != null)
            {
                var apachePath = installedApache.BinaryPath;
                _logger.LogInformation("Found Apache service in database: {ServiceName} at {Path}", installedApache.Name, apachePath);
                
                // Verify the Apache executable exists
                var executablePath = ModStack.Core.Extensions.ServiceTypeExtensions.GetExecutablePath(ModStack.Common.ServiceType.Apache, apachePath);
                if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
                {
                    _logger.LogInformation("Apache executable found at: {ExecutablePath}", executablePath);
                    return apachePath;
                }
                else
                {
                    _logger.LogWarning("Apache executable not found at expected path: {ExecutablePath}", executablePath);
                }
            }
            else
            {
                _logger.LogWarning("No installed Apache service found in database");
            }
            
            // Fallback to discovery if database lookup fails
            _logger.LogInformation("Falling back to Apache discovery...");
            return FindApacheInstallationPath();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding Apache installation path from database");
            return FindApacheInstallationPath();
        }
    }

    private string FindApacheInstallationPath()
    {
        var discoveryPaths = _pathConfigurationService.GetServiceDiscoveryPaths("Apache");
        
        _logger.LogInformation("Apache discovery paths: {Paths}", string.Join(", ", discoveryPaths));
        
        foreach (var path in discoveryPaths)
        {
            _logger.LogInformation("Checking Apache path: {Path}", path);
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "bin", "httpd.exe")))
            {
                _logger.LogInformation("Found Apache installation at: {Path}", path);
                return path;
            }
        }

        _logger.LogWarning("Apache installation not found in any discovery path");
        return string.Empty;
    }

    private static string DetermineDocumentRoot(string projectPath)
    {
        var publicPath = Path.Combine(projectPath, "public");
        return Directory.Exists(publicPath) ? publicPath : projectPath;
    }

    private async Task EnsureIndexFileExistsAsync(string documentRoot, string domain)
    {
        try
        {
            Directory.CreateDirectory(documentRoot);
            
            var indexHtml = Path.Combine(documentRoot, "index.html");
            var indexPhp = Path.Combine(documentRoot, "index.php");
            
            if (File.Exists(indexHtml) || File.Exists(indexPhp))
                return;

            var htmlContent = GenerateDefaultIndexContent(domain, documentRoot);
            await File.WriteAllTextAsync(indexHtml, htmlContent);
            
            _logger.LogDebug("Created default index.html for {Domain}", domain);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create index file for {Domain}", domain);
        }
    }

    private async Task<string> GenerateVirtualHostContentAsync(
        string domain, 
        string documentRoot, 
        List<string>? serverAliases, 
        int port, 
        bool enableSSL, 
        long? projectId)
    {
        var aliases = serverAliases?.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList() ?? new List<string>();
        if (!aliases.Contains($"www.{domain}", StringComparer.OrdinalIgnoreCase))
        {
            aliases.Add($"www.{domain}");
        }

        var normalizedDocumentRoot = documentRoot.Replace("\\", "/");
        var phpWrapperPath = await GetPhpWrapperPathAsync(projectId);
        var hasPhpWrapper = !string.IsNullOrEmpty(phpWrapperPath);

        if (hasPhpWrapper)
        {
            LogService.Info($"PHP wrapper resolved for virtual host '{domain}': {phpWrapperPath}");
        }
        else
        {
            LogService.Warning($"PHP wrapper not found for virtual host '{domain}'. PHP files will not be executed.");
        }

        // Derive FcgidMaxRequestLen from the PHP service's post_max_size/upload_max_filesize
        var fcgidMaxRequestLen = await GetFcgidMaxRequestLenFromPhpConfigAsync(projectId);

        var builder = new StringBuilder();
        builder.AppendLine(BuildFcgidVirtualHostBlock(domain, normalizedDocumentRoot, aliases, phpWrapperPath, port, fcgidMaxRequestLen: fcgidMaxRequestLen));

        if (enableSSL)
        {
            builder.AppendLine();
            builder.AppendLine(BuildFcgidVirtualHostBlock(domain, normalizedDocumentRoot, aliases, phpWrapperPath, 443, ssl: true, fcgidMaxRequestLen: fcgidMaxRequestLen));
        }

        return builder.ToString();
    }

    private async Task<(string? CertificatePath, string? KeyPath)> GenerateSSLCertificateAsync(string domain, string vhostsDir)
    {
        try
        {
            SecurityValidator.ValidateHostnameOrThrow(domain, nameof(domain));
            // SSL certificates are stored directly in vhosts directory (not in ssl subfolder)
            var certPath = SecurityValidator.ValidateSafePathWithinRoot(vhostsDir, $"{domain}.crt");
            var keyPath = SecurityValidator.ValidateSafePathWithinRoot(vhostsDir, $"{domain}.key");

            var success = await _sslCertificateService.GenerateSelfSignedCertificateAsync(domain, certPath, keyPath, 365);

            if (success)
            {
                _logger.LogInformation("SSL certificate generated for {Domain}", domain);
                return (certPath, keyPath);
            }
            else
            {
                _logger.LogWarning("Failed to generate SSL certificate for {Domain}", domain);
                return (null, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SSL certificate for {Domain}", domain);
            return (null, null);
        }
    }

    private async Task EnsureApacheIncludesVhostsAsync(string apachePath)
    {
        try
        {
            LogService.Info($"Checking Apache configuration at: {apachePath}");
            var confPath = Path.Combine(apachePath, "conf", "httpd.conf");
            if (!File.Exists(confPath))
            {
                LogService.Warning($"Apache httpd.conf not found at: {confPath}");
                return;
            }

            LogService.Info($"Reading Apache configuration file: {confPath}");
            var content = await File.ReadAllTextAsync(confPath);
            var modified = false;
            
            // Ensure IncludeOptional directive for vhosts
            if (!content.Contains("Include conf/vhosts/*.vhost") && !content.Contains("IncludeOptional conf/vhosts/*.vhost"))
            {
                LogService.Info("Adding vhosts include directive to Apache configuration");
                content += "\n# ModStack Virtual Hosts\nIncludeOptional conf/vhosts/*.vhost\n";
                modified = true;
                LogService.Info("Added vhosts include directive to Apache configuration");
            }
            
            if (modified)
            {
                await File.WriteAllTextAsync(confPath, content);
            }

            // Create default catch-all virtual host to prevent cross-project serving.
            // Apache falls back to the first VirtualHost for unmatched domains, so
            // 000-default.vhost (sorted first alphabetically) catches unmatched requests
            // instead of serving another project's content.
            await EnsureDefaultCatchAllVhostAsync(apachePath);
        }
        catch (Exception ex)
        {
            LogService.Error($"Error ensuring Apache includes vhosts: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a 000-default.vhost catch-all that sorts first alphabetically,
    /// preventing Apache from serving another project's content for unmatched domains.
    /// The port 443 block includes a self-signed localhost SSL certificate so Apache
    /// can complete the TLS handshake — without this, ALL HTTPS projects would break.
    /// </summary>
    private async Task EnsureDefaultCatchAllVhostAsync(string apachePath)
    {
        try
        {
            var vhostsDir = Path.Combine(apachePath, "conf", "vhosts");
            Directory.CreateDirectory(vhostsDir);

            var defaultVhostPath = Path.Combine(vhostsDir, "000-default.vhost");
            var htdocsPath = Path.Combine(apachePath, "htdocs").Replace("\\", "/");

            // Generate a self-signed certificate for localhost if it doesn't exist.
            // This is required for the 443 catch-all to work — Apache needs a valid
            // SSL config on the first vhost for port 443, otherwise it can't do the
            // TLS handshake and all HTTPS projects will get ERR_SSL_PROTOCOL_ERROR.
            var certPath = Path.Combine(vhostsDir, "000-default.crt");
            var keyPath = Path.Combine(vhostsDir, "000-default.key");

            if (!File.Exists(certPath) || !File.Exists(keyPath))
            {
                try
                {
                    var success = await _sslCertificateService.GenerateSelfSignedCertificateAsync(
                        "localhost", certPath, keyPath, 3650); // 10 year validity
                    if (success)
                    {
                        LogService.Info("Generated self-signed SSL certificate for default catch-all vhost");
                    }
                    else
                    {
                        LogService.Warning("Failed to generate SSL certificate for default catch-all vhost");
                    }
                }
                catch (Exception ex)
                {
                    LogService.Warning($"Could not generate default SSL certificate: {ex.Message}");
                }
            }

            var sslCertExists = File.Exists(certPath) && File.Exists(keyPath);
            var certPathNormalized = certPath.Replace("\\", "/");
            var keyPathNormalized = keyPath.Replace("\\", "/");

            var builder = new StringBuilder();
            builder.AppendLine("# ModStack Default Catch-All Virtual Host");
            builder.AppendLine("# This file MUST sort first alphabetically (000-) to be the default vhost.");
            builder.AppendLine("# It prevents Apache from serving another project's content when a domain");
            builder.AppendLine("# does not match any configured virtual host.");
            builder.AppendLine();

            // HTTP catch-all
            builder.AppendLine("<VirtualHost 127.0.0.1:80>");
            builder.AppendLine($"    DocumentRoot \"{htdocsPath}\"");
            builder.AppendLine("    ServerName localhost");
            builder.AppendLine();
            builder.AppendLine($"    <Directory \"{htdocsPath}\">");
            builder.AppendLine("        Options Indexes FollowSymLinks");
            builder.AppendLine("        AllowOverride All");
            builder.AppendLine("        Require all granted");
            builder.AppendLine("    </Directory>");
            builder.AppendLine("</VirtualHost>");
            builder.AppendLine();

            // HTTPS catch-all (only if cert was generated successfully)
            if (sslCertExists)
            {
                builder.AppendLine("<VirtualHost 127.0.0.1:443>");
                builder.AppendLine($"    DocumentRoot \"{htdocsPath}\"");
                builder.AppendLine("    ServerName localhost");
                builder.AppendLine();
                builder.AppendLine("    SSLEngine On");
                builder.AppendLine($"    SSLCertificateFile \"{certPathNormalized}\"");
                builder.AppendLine($"    SSLCertificateKeyFile \"{keyPathNormalized}\"");
                builder.AppendLine();
                builder.AppendLine($"    <Directory \"{htdocsPath}\">");
                builder.AppendLine("        Options Indexes FollowSymLinks");
                builder.AppendLine("        AllowOverride All");
                builder.AppendLine("        Require all granted");
                builder.AppendLine("    </Directory>");
                builder.AppendLine("</VirtualHost>");
            }

            await File.WriteAllTextAsync(defaultVhostPath, builder.ToString());
            LogService.Info($"Default catch-all virtual host written to: {defaultVhostPath} (SSL: {sslCertExists})");
        }
        catch (Exception ex)
        {
            LogService.Warning($"Could not create default catch-all virtual host: {ex.Message}");
        }
    }

    private static async Task<(bool IsValid, string Details)> TestAndRestartApacheAsync(string apachePath)
    {
        try
        {
            LogService.Info($"Starting Apache configuration test at: {apachePath}");
            // Test configuration
            var httpdPath = Path.Combine(apachePath, "bin", "httpd.exe");
            if (!File.Exists(httpdPath))
            {
                var msg = $"Apache httpd.exe not found at: {httpdPath}";
                LogService.Warning(msg);
                return (false, msg);
            }

            LogService.Info($"Testing Apache configuration with: {httpdPath} -t");
            var testProcess = new System.Diagnostics.ProcessStartInfo
            {
                FileName = httpdPath,
                Arguments = "-t",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(testProcess);
            if (process != null)
            {
                LogService.Info("Waiting for Apache configuration test to complete...");
                
                // Add timeout to prevent hanging
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    LogService.Info($"Apache configuration test completed. Exit code: {process.ExitCode}");
                    LogService.Info($"Apache test output: {output}");
                    if (!string.IsNullOrEmpty(error))
                    {
                        LogService.Info($"Apache test error: {error}");
                    }

                    if (process.ExitCode == 0)
                    {
                        LogService.Info("Apache configuration test passed");
                        LogService.Info("Apache will be restarted by CreateProjectViewModel after virtual host update");
                        return (true, "Syntax OK");
                    }
                    else
                    {
                        LogService.Warning($"Apache configuration test failed. Exit code: {process.ExitCode}");
                        LogService.Warning($"Apache test error output: {error}");
                        LogService.Warning($"Apache test standard output: {output}");
                        var details = !string.IsNullOrWhiteSpace(error) ? error.Trim() : output.Trim();
                        return (false, string.IsNullOrEmpty(details) ? $"Exit code {process.ExitCode}" : details);
                    }
                }
                catch (OperationCanceledException)
                {
                    LogService.Warning("Apache configuration test timed out after 10 seconds");
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            LogService.Info("Killed hanging Apache configuration test process");
                        }
                    }
                    catch (Exception killEx)
                    {
                        LogService.Error($"Error killing Apache process: {killEx.Message}");
                    }
                    return (false, "Apache configuration validation timed out.");
                }
            }
            else
            {
                LogService.Error("Failed to start Apache configuration test process");
                return (false, "Failed to start Apache configuration test process.");
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Error testing Apache configuration: {ex.Message}");
            return (false, ex.Message);
        }
    }

    private static string GenerateDefaultIndexContent(string domain, string documentRoot)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{domain} - Welcome</title>
    <style>
        body {{ 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; 
            max-width: 800px; 
            margin: 0 auto; 
            padding: 2rem; 
            background: #f8f9fa;
        }}
        .header {{ 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
            color: white; 
            padding: 2rem; 
            border-radius: 8px; 
            margin-bottom: 2rem; 
            text-align: center;
        }}
        .info {{ 
            background: white; 
            padding: 1.5rem; 
            border-radius: 8px; 
            box-shadow: 0 2px 4px rgba(0,0,0,0.1); 
        }}
        .success {{ color: #28a745; font-weight: bold; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>🚀 Welcome to {domain}!</h1>
        <p>Your virtual host is working correctly</p>
    </div>
    
    <div class=""info"">
        <h3>📋 Virtual Host Information</h3>
        <p><strong>Domain:</strong> {domain}</p>
        <p><strong>Document Root:</strong> {documentRoot}</p>
        <p><strong>Status:</strong> <span class=""success"">✅ Active</span></p>
        
        <h3>🔧 Next Steps</h3>
        <ul>
            <li>Edit this index.html file to customize your site</li>
            <li>Add CSS, JavaScript, and other assets</li>
            <li>Configure PHP if needed</li>
            <li>Set up SSL certificates for HTTPS</li>
        </ul>
        
        <p><em>This page was automatically generated by ModStack</em></p>
    </div>
</body>
</html>";
    }

    private VirtualHostInfo? ParseVirtualHostContent(string content, string filePath)
    {
        try
        {
            var info = new VirtualHostInfo
            {
                ConfigFilePath = filePath
            };

            // Extract domain from ServerName
            var serverNameMatch = ServerNamePattern().Match(content);
            if (serverNameMatch.Success)
            {
                info.Domain = serverNameMatch.Groups[1].Value;
            }

            // Extract document root
            var docRootMatch = DocumentRootPattern().Match(content);
            if (docRootMatch.Success)
            {
                info.DocumentRoot = docRootMatch.Groups[1].Value;
            }

            // Extract port
            var portMatch = VirtualHostPortPattern().Match(content);
            if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out var port))
            {
                info.Port = port;
            }

            // Check for SSL
            info.SSLEnabled = content.Contains("SSLEngine on");

            // Extract server aliases
            var aliasMatches = ServerAliasPattern().Matches(content);
            foreach (System.Text.RegularExpressions.Match match in aliasMatches)
            {
                info.ServerAliases.Add(match.Groups[1].Value);
            }

            return string.IsNullOrEmpty(info.Domain) ? null : info;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing virtual host content from {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Fixes virtual host files with invalid directives (like ProxyFCGIBackendType)
    /// </summary>
    public async Task<int> FixInvalidVirtualHostDirectivesAsync()
    {
        try
        {
            var apachePath = await FindApacheInstallationPathAsync();
            if (string.IsNullOrEmpty(apachePath))
            {
                LogService.Warning("Apache installation not found, cannot fix virtual host directives");
                return 0;
            }

            var vhostsDir = Path.Combine(apachePath, "conf", "vhosts");
            if (!Directory.Exists(vhostsDir))
            {
                return 0;
            }

            var vhostFiles = Directory.GetFiles(vhostsDir, "*.vhost");
            var fixedCount = 0;

            foreach (var vhostFile in vhostFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(vhostFile);
                    var originalContent = content;
                    
                    // Remove ProxyFCGIBackendType directive (not available in all Apache versions)
                    if (content.Contains("ProxyFCGIBackendType"))
                    {
                        // Remove the line containing ProxyFCGIBackendType
                        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        var fixedLines = lines.Where(line => !line.Trim().StartsWith("ProxyFCGIBackendType", StringComparison.OrdinalIgnoreCase)).ToArray();
                        content = string.Join(Environment.NewLine, fixedLines);
                        
                        LogService.Info($"Removed ProxyFCGIBackendType directive from: {Path.GetFileName(vhostFile)}");
                    }
                    
                    // Remove ProxyFCGISetEnvIf directive (causes DNS lookup errors - not a valid directive)
                    if (content.Contains("ProxyFCGISetEnvIf"))
                    {
                        // Remove the line containing ProxyFCGISetEnvIf
                        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        var fixedLines = lines.Where(line => !line.Trim().StartsWith("ProxyFCGISetEnvIf", StringComparison.OrdinalIgnoreCase)).ToArray();
                        content = string.Join(Environment.NewLine, fixedLines);
                        
                        LogService.Info($"Removed ProxyFCGISetEnvIf directive from: {Path.GetFileName(vhostFile)} (causes DNS lookup errors)");
                    }
                    
                    // Fix malformed proxy URLs (e.g., "127.0.0.1:9000c:" caused by ProxyFCGISetEnvIf)
                    if (content.Contains("proxy:fcgi://") || content.Contains("ProxyPass"))
                    {
                        content = Regex.Replace(content, @"^\s*ProxyPassMatch[^\r\n]*\r?\n?", string.Empty, RegexOptions.Multiline);
                        content = Regex.Replace(content, @"^\s*ProxyPass[^\r\n]*\r?\n?", string.Empty, RegexOptions.Multiline);
                        content = Regex.Replace(content, @"<FilesMatch\s+""\\\.php\$"">[\s\S]*?</FilesMatch>\s*", string.Empty, RegexOptions.IgnoreCase);
                        LogService.Info($"Removed legacy Proxy/SetHandler directives from: {Path.GetFileName(vhostFile)}");
                    }
                    
                    // Only write if content changed
                    if (content != originalContent)
                    {
                        await File.WriteAllTextAsync(vhostFile, content);
                        LogService.Info($"Fixed virtual host file: {vhostFile}");
                        fixedCount++;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Warning($"Error fixing virtual host file: {vhostFile}: {ex.Message}");
                }
            }

            if (fixedCount > 0)
            {
                LogService.Info($"Fixed {fixedCount} virtual host file(s) with invalid directives");
                // Restart Apache to apply changes
                await TestAndRestartApacheAsync(apachePath);
            }

            return fixedCount;
        }
        catch (Exception ex)
        {
            LogService.Error("Error fixing invalid virtual host directives", ex);
            return 0;
        }
    }

    /// <summary>
    /// Cleans up orphaned virtual host files (files that reference non-existent DocumentRoots)
    /// </summary>
    public async Task<int> CleanupOrphanedVirtualHostsAsync()
    {
        try
        {
            var apachePath = await FindApacheInstallationPathAsync();
            if (string.IsNullOrEmpty(apachePath))
            {
                LogService.Warning("Apache installation not found, cannot cleanup orphaned virtual hosts");
                return 0;
            }

            var vhostsDir = Path.Combine(apachePath, "conf", "vhosts");
            if (!Directory.Exists(vhostsDir))
            {
                return 0;
            }

            var vhostFiles = Directory.GetFiles(vhostsDir, "*.vhost");
            var cleanedCount = 0;

            foreach (var vhostFile in vhostFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(vhostFile);
                    
                    // Extract DocumentRoot from the file
                    var docRootMatch = DocumentRootPattern().Match(content);
                    if (!docRootMatch.Success)
                    {
                        continue;
                    }

                    var documentRoot = docRootMatch.Groups[1].Value.Replace("/", "\\");
                    
                    // Check if DocumentRoot exists
                    if (!Directory.Exists(documentRoot))
                    {
                        LogService.Warning($"Found orphaned virtual host file: {Path.GetFileName(vhostFile)} - DocumentRoot does not exist: {documentRoot}");
                        
                        // Extract domain name from filename
                        var domain = Path.GetFileNameWithoutExtension(vhostFile);
                        
                        // Delete the virtual host file
                        File.Delete(vhostFile);
                        LogService.Info($"Deleted orphaned virtual host file: {vhostFile}");
                        cleanedCount++;
                        
                        // Also try to remove from database if it exists
                        try
                        {
                            await RemoveVirtualHostAsync(domain);
                        }
                        catch
                        {
                            // Ignore database errors - file is already deleted
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.Warning($"Error processing virtual host file: {vhostFile}: {ex.Message}");
                }
            }

            if (cleanedCount > 0)
            {
                LogService.Info($"Cleaned up {cleanedCount} orphaned virtual host file(s)");
                // Restart Apache to apply changes
                await TestAndRestartApacheAsync(apachePath);
            }

            return cleanedCount;
        }
        catch (Exception ex)
        {
            LogService.Error("Error cleaning up orphaned virtual hosts", ex);
            return 0;
        }
    }

    #endregion

    private string BuildFcgidVirtualHostBlock(
        string domain,
        string documentRoot,
        List<string> aliases,
        string? phpWrapperPath,
        int port,
        bool ssl = false,
        long? fcgidMaxRequestLen = null)
    {
        SecurityValidator.ValidateHostnameOrThrow(domain, nameof(domain));
        foreach (var alias in aliases)
        {
            SecurityValidator.ValidateHostnameOrThrow(alias, nameof(aliases));
        }

        // Default to 1GB if not provided
        var maxRequestLen = fcgidMaxRequestLen ?? 1073741824;

        var builder = new StringBuilder();
        builder.AppendLine($"<VirtualHost 127.0.0.1:{port}>");
        builder.AppendLine($"    DocumentRoot \"{documentRoot}\"");
        builder.AppendLine($"    ServerName {domain}");

        foreach (var alias in aliases.Where(a => !string.Equals(a, domain, StringComparison.OrdinalIgnoreCase)))
        {
            builder.AppendLine($"    ServerAlias {alias}");
        }

        builder.AppendLine("    DirectoryIndex index.php index.html index.htm");

        if (!string.IsNullOrEmpty(phpWrapperPath))
        {
            builder.AppendLine($"    FcgidMaxRequestLen {maxRequestLen}");
            builder.AppendLine("    FcgidIOTimeout 7200");
            builder.AppendLine("    FcgidConnectTimeout 7200");
            builder.AppendLine("    FcgidBusyTimeout 7200");
            builder.AppendLine("    FcgidIdleTimeout 7200");
            builder.AppendLine("    FcgidProcessLifeTime 7200");
            builder.AppendLine($"    FcgidWrapper \"{phpWrapperPath}\" .php");
        }
        else
        {
            builder.AppendLine("    # PHP wrapper not found - PHP files will not be processed");
        }

        builder.AppendLine($"    <Directory \"{documentRoot}\">");
        builder.AppendLine(!string.IsNullOrEmpty(phpWrapperPath)
            ? "        AddHandler fcgid-script .php"
            : "        # AddHandler fcgid-script .php");
        builder.AppendLine("        Options all");
        builder.AppendLine("        AllowOverride all");
        builder.AppendLine("        Require all granted");
        builder.AppendLine("    </Directory>");

        if (ssl)
        {
            builder.AppendLine("    SSLEngine On");
            builder.AppendLine($"    SSLCertificateFile \"conf/vhosts/{domain}.crt\"");
            builder.AppendLine($"    SSLCertificateKeyFile \"conf/vhosts/{domain}.key\"");
        }

        builder.Append("</VirtualHost>");
        return builder.ToString();
    }

}
