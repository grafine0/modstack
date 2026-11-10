using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using ModStack.Common.Models;
using ModStack.Common.Services;
using ModStack.Data.Interfaces;

namespace ModStack.Services
{
    /// <summary>
    /// Manages Apache modules - enabling, disabling, and synchronization
    /// </summary>
    public class ApacheModuleManager
    {
        private readonly IApacheModuleRepository _moduleRepository;
        private static readonly SemaphoreSlim _configLock = new(1, 1);

        public ApacheModuleManager(IApacheModuleRepository moduleRepository)
        {
            _moduleRepository = moduleRepository;
        }

        /// <summary>
        /// Synchronizes Apache modules with the file system and httpd.conf
        /// </summary>
        public async Task<bool> SynchronizeModulesAsync(string apachePath, string apacheVersion)
        {
            try
            {
                LogService.Info($"Starting Apache module synchronization for {apachePath}");

                var httpdConfPath = Path.Combine(apachePath, "conf", "httpd.conf");
                var modulesPath = Path.Combine(apachePath, "modules");

                if (!File.Exists(httpdConfPath))
                {
                    LogService.Error($"Apache configuration file not found: {httpdConfPath}");
                    return false;
                }

                if (!Directory.Exists(modulesPath))
                {
                    LogService.Error($"Apache modules directory not found: {modulesPath}");
                    return false;
                }

                // Get available modules from file system
                var availableModules = GetAvailableModulesFromFileSystem(modulesPath);
                
                // Get enabled modules from httpd.conf
                var enabledModules = await GetEnabledModulesFromConfig(httpdConfPath);

                // Update database with current state
                await UpdateModulesInDatabase(availableModules, enabledModules, apacheVersion);

                LogService.Info("Apache module synchronization completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Error("Error synchronizing Apache modules", ex);
                return false;
            }
        }

        /// <summary>
        /// Enables an Apache module
        /// </summary>
        public async Task<bool> EnableModuleAsync(long moduleId, string apachePath)
        {
            try
            {
                var module = await _moduleRepository.GetByIdAsync(moduleId);
                if (module == null)
                {
                    LogService.Error($"Apache module with ID {moduleId} not found");
                    return false;
                }

                if (!module.IsAvailable)
                {
                    LogService.Error($"Apache module {module.ModuleName} is not available");
                    return false;
                }

                var httpdConfPath = Path.Combine(apachePath, "conf", "httpd.conf");
                var success = await EnableModuleInConfig(httpdConfPath, module);

                if (success)
                {
                    await _moduleRepository.UpdateEnabledStatusAsync(moduleId, true);
                    LogService.Info($"Successfully enabled Apache module {module.ModuleName}");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogService.Error($"Error enabling Apache module {moduleId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Disables an Apache module
        /// </summary>
        public async Task<bool> DisableModuleAsync(long moduleId, string apachePath)
        {
            try
            {
                var module = await _moduleRepository.GetByIdAsync(moduleId);
                if (module == null)
                {
                    LogService.Error($"Apache module with ID {moduleId} not found");
                    return false;
                }

                if (module.IsRequired)
                {
                    LogService.Warning($"Cannot disable required Apache module {module.ModuleName}");
                    return false;
                }

                var httpdConfPath = Path.Combine(apachePath, "conf", "httpd.conf");
                var success = await DisableModuleInConfig(httpdConfPath, module);

                if (success)
                {
                    await _moduleRepository.UpdateEnabledStatusAsync(moduleId, false);
                    LogService.Info($"Successfully disabled Apache module {module.ModuleName}");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogService.Error($"Error disabling Apache module {moduleId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets Apache installation path for a specific version
        /// </summary>
        public string? GetApacheInstallationPath(string apacheVersion)
        {
            var possiblePaths = new[]
            {
                $@"C:\ModStack\Services\Apache\apache-{apacheVersion}",
                $@"C:\ModStack\Services\Apache\{apacheVersion}",
                $@"C:\Apache24",
                $@"C:\xampp\apache",
                $@"C:\wamp\bin\apache\apache{apacheVersion}",
                $@"C:\Program Files\Apache Software Foundation\Apache2.4"
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "bin", "httpd.exe")))
                {
                LogService.Info($"Found Apache installation at {path}");
                    return path;
                }
            }

            LogService.Warning($"Apache installation not found for version {apacheVersion}");
            return null;
        }

        private Dictionary<string, string> GetAvailableModulesFromFileSystem(string modulesPath)
        {
            var modules = new Dictionary<string, string>();

            try
            {
                var moduleFiles = Directory.GetFiles(modulesPath, "mod_*.so");
                LogService.Info($"Found {moduleFiles.Length} module files in {modulesPath}");
                
                foreach (var filePath in moduleFiles)
                {
                    var fileName = Path.GetFileName(filePath);
                    var moduleName = GetModuleNameFromFileName(fileName);
                    
                    if (!string.IsNullOrEmpty(moduleName))
                    {
                        modules[moduleName] = $"modules/{fileName}";
                        LogService.Info($"Discovered module: {fileName} -> {moduleName}");
                    }
                    else
                    {
                        LogService.Warning($"Could not convert filename to module name: {fileName}");
                    }
                }

                LogService.Info($"Found {modules.Count} available Apache modules in {modulesPath}");
                
                // Log proxy modules specifically
                var proxyModules = modules.Keys.Where(k => k.Contains("proxy", StringComparison.OrdinalIgnoreCase)).ToList();
                if (proxyModules.Any())
                {
                    LogService.Info($"Proxy modules discovered: {string.Join(", ", proxyModules)}");
                }
                else
                {
                    LogService.Warning($"No proxy modules found in {modulesPath}");
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"Error scanning Apache modules directory {modulesPath}", ex);
            }

            return modules;
        }

        private async Task<HashSet<string>> GetEnabledModulesFromConfig(string httpdConfPath)
        {
            var enabledModules = new HashSet<string>();

            try
            {
                var configContent = await File.ReadAllTextAsync(httpdConfPath);
                var loadModulePattern = @"^\s*LoadModule\s+(\w+)\s+";
                var matches = Regex.Matches(configContent, loadModulePattern, RegexOptions.Multiline);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        enabledModules.Add(match.Groups[1].Value);
                    }
                }

                LogService.Info($"Found {enabledModules.Count} enabled modules in {httpdConfPath}");
            }
            catch (Exception ex)
            {
                LogService.Error($"Error reading Apache configuration {httpdConfPath}", ex);
            }

            return enabledModules;
        }

        private async Task UpdateModulesInDatabase(Dictionary<string, string> availableModules, HashSet<string> enabledModules, string apacheVersion)
        {
            try
            {
                // Get existing modules from database
                var existingModules = await _moduleRepository.GetAllAsync();
                var existingModuleDict = existingModules.ToDictionary(m => m.ModuleName, m => m);
                
                LogService.Info($"Updating modules in database. Existing: {existingModuleDict.Count}, Available: {availableModules.Count}");

                var createdCount = 0;
                var updatedCount = 0;

                foreach (var (moduleName, fileName) in availableModules)
                {
                    var isEnabled = enabledModules.Contains(moduleName);
                    var moduleInfo = GetModuleInfo(moduleName);

                    if (existingModuleDict.TryGetValue(moduleName, out var existingModule))
                    {
                        // Update existing module
                        existingModule.IsEnabled = isEnabled;
                        existingModule.IsAvailable = true;
                        existingModule.ApacheVersion = apacheVersion;
                        existingModule.FileName = fileName;
                        
                        await _moduleRepository.UpdateAsync(existingModule);
                        updatedCount++;
                    }
                    else
                    {
                        // Create new module
                        var newModule = new ApacheModule
                        {
                            Name = moduleInfo.Name,
                            ModuleName = moduleName,
                            FileName = fileName,
                            Description = moduleInfo.Description,
                            Category = moduleInfo.Category,
                            IsEnabled = isEnabled,
                            IsRequired = moduleInfo.IsRequired,
                            IsAvailable = true,
                            ApacheVersion = apacheVersion
                        };

                        await _moduleRepository.CreateAsync(newModule);
                        createdCount++;
                        LogService.Info($"Created new module in database: {moduleName} ({moduleInfo.Name})");
                    }
                }

                LogService.Info($"Module database update complete. Created: {createdCount}, Updated: {updatedCount}");

                // Mark modules as unavailable if they don't exist in file system
                foreach (var existingModule in existingModules)
                {
                    if (!availableModules.ContainsKey(existingModule.ModuleName))
                    {
                        await _moduleRepository.UpdateAvailabilityStatusAsync(existingModule.Id, false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Error updating modules in database", ex);
                throw;
            }
        }

        private async Task<bool> EnableModuleInConfig(string httpdConfPath, ApacheModule module)
        {
            await _configLock.WaitAsync();
            try
            {
                var configContent = await File.ReadAllTextAsync(httpdConfPath);
                var loadModuleLine = $"LoadModule {module.ModuleName} {module.FileName}";
                var commentedPattern = $@"^\s*#\s*LoadModule\s+{Regex.Escape(module.ModuleName)}\s+";

                // Check if module is already enabled
                if (configContent.Contains(loadModuleLine) && !Regex.IsMatch(configContent, commentedPattern, RegexOptions.Multiline))
                {
                    LogService.Info($"Apache module {module.ModuleName} is already enabled");
                    return true;
                }

                // Uncomment if commented
                if (Regex.IsMatch(configContent, commentedPattern, RegexOptions.Multiline))
                {
                    configContent = Regex.Replace(configContent, commentedPattern, $"LoadModule {module.ModuleName} ", RegexOptions.Multiline);
                }
                else
                {
                    // Add new LoadModule line after other LoadModule directives
                    var lastLoadModulePattern = @"(.*LoadModule\s+\w+\s+[^\r\n]+)";
                    var lastMatch = Regex.Matches(configContent, lastLoadModulePattern, RegexOptions.Multiline).LastOrDefault();
                    
                    if (lastMatch != null)
                    {
                        var insertPosition = lastMatch.Index + lastMatch.Length;
                        configContent = configContent.Insert(insertPosition, $"\nLoadModule {module.ModuleName} {module.FileName}");
                    }
                    else
                    {
                        // Fallback: add at the beginning of the file
                        configContent = $"LoadModule {module.ModuleName} {module.FileName}\n" + configContent;
                    }
                }

                await File.WriteAllTextAsync(httpdConfPath, configContent);
                LogService.Info($"Enabled Apache module {module.ModuleName} in configuration");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Error($"Error enabling Apache module {module.ModuleName} in configuration", ex);
                return false;
            }
            finally
            {
                _configLock.Release();
            }
        }

        private async Task<bool> DisableModuleInConfig(string httpdConfPath, ApacheModule module)
        {
            await _configLock.WaitAsync();
            try
            {
                var configContent = await File.ReadAllTextAsync(httpdConfPath);
                var loadModulePattern = $@"^\s*LoadModule\s+{Regex.Escape(module.ModuleName)}\s+";

                if (Regex.IsMatch(configContent, loadModulePattern, RegexOptions.Multiline))
                {
                    configContent = Regex.Replace(configContent, loadModulePattern, $"#LoadModule {module.ModuleName} ", RegexOptions.Multiline);
                    await File.WriteAllTextAsync(httpdConfPath, configContent);
                    LogService.Info($"Disabled Apache module {module.ModuleName} in configuration");
                    return true;
                }
                else
                {
                    LogService.Warning($"Apache module {module.ModuleName} was not found in configuration");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"Error disabling Apache module {module.ModuleName} in configuration", ex);
                return false;
            }
            finally
            {
                _configLock.Release();
            }
        }

        private string GetModuleNameFromFileName(string fileName)
        {
            // Convert mod_ssl.so to ssl_module
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            if (baseName.StartsWith("mod_"))
            {
                return baseName.Substring(4) + "_module";
            }
            return baseName + "_module";
        }

        /// <summary>
        /// Populates the database with default Apache modules if empty
        /// </summary>
        public async Task<bool> InitializeDefaultModulesAsync(string apachePath, string apacheVersion)
        {
            try
            {
                LogService.Info($"Initializing default Apache modules for {apacheVersion}");

                // Check if modules already exist
                var existingModules = await _moduleRepository.GetAllAsync();
                if (existingModules.Any())
                {
                    LogService.Info("Apache modules already exist in database, skipping initialization");
                    return true;
                }

                // Get available modules from file system
                var modulesPath = Path.Combine(apachePath, "modules");
                Debug.WriteLine($"🔍 Scanning modules directory: {modulesPath}");
                var availableModules = GetAvailableModulesFromFileSystem(modulesPath);
                Debug.WriteLine($"🔍 Found {availableModules.Count} available modules");

                // Get currently enabled modules
                var httpdConfPath = Path.Combine(apachePath, "conf", "httpd.conf");
                Debug.WriteLine($"🔍 Reading Apache config: {httpdConfPath}");
                var enabledModules = await GetEnabledModulesFromConfig(httpdConfPath);
                Debug.WriteLine($"🔍 Found {enabledModules.Count} enabled modules");

                // Create default module entries
                Debug.WriteLine($"🔧 Creating module entries in database...");
                var createdCount = 0;
                
                foreach (var (moduleName, fileName) in availableModules)
                {
                    try
                    {
                        var moduleInfo = GetModuleInfo(moduleName);
                        var isEnabled = enabledModules.Contains(moduleName);

                        var module = new ApacheModule
                        {
                            Name = moduleInfo.Name,
                            ModuleName = moduleName,
                            FileName = fileName,
                            Description = moduleInfo.Description,
                            Category = moduleInfo.Category,
                            IsEnabled = isEnabled,
                            IsRequired = moduleInfo.IsRequired,
                            IsAvailable = true,
                            ApacheVersion = apacheVersion,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };

                        await _moduleRepository.CreateAsync(module);
                        createdCount++;
                        Debug.WriteLine($"✅ Created module: {moduleName} ({moduleInfo.Name}) - {(isEnabled ? "Enabled" : "Disabled")}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ Failed to create module {moduleName}: {ex.Message}");
                        LogService.Error($"Failed to create module {moduleName}: {ex.Message}");
                    }
                }

                Debug.WriteLine($"🎉 Successfully created {createdCount} of {availableModules.Count} Apache modules");
                LogService.Info($"Successfully initialized {createdCount} of {availableModules.Count} Apache modules");
                return createdCount > 0;
            }
            catch (Exception ex)
            {
                LogService.Error("Error initializing default Apache modules", ex);
                return false;
            }
        }

        private (string Name, string Description, string Category, bool IsRequired) GetModuleInfo(string moduleName)
        {
            // Comprehensive Apache module information
            var moduleInfo = moduleName.ToLowerInvariant() switch
            {
                "ssl_module" => ("SSL", "Secure Sockets Layer support for HTTPS", "Security", false),
                "rewrite_module" => ("URL Rewrite", "URL rewriting engine for clean URLs", "Core", true),
                "headers_module" => ("Headers", "HTTP header manipulation", "Core", true),
                "deflate_module" => ("Deflate", "Content compression for better performance", "Performance", false),
                "expires_module" => ("Expires", "HTTP Expires header control", "Performance", false),
                "mime_module" => ("MIME", "MIME type mapping", "Core", true),
                "dir_module" => ("Directory", "Directory listing and index files", "Core", true),
                "autoindex_module" => ("Auto Index", "Automatic directory indexing", "Core", false),
                "negotiation_module" => ("Content Negotiation", "Content negotiation based on client preferences", "Core", true),
                "setenvif_module" => ("Set Environment", "Set environment variables based on request", "Core", true),
                "auth_basic_module" => ("Basic Authentication", "Basic HTTP authentication", "Security", false),
                "auth_digest_module" => ("Digest Authentication", "Digest HTTP authentication", "Security", false),
                "authz_core_module" => ("Authorization Core", "Core authorization framework", "Security", true),
                "authz_host_module" => ("Host Authorization", "Host-based access control", "Security", true),
                "authz_user_module" => ("User Authorization", "User-based access control", "Security", false),
                "authn_core_module" => ("Authentication Core", "Core authentication framework", "Security", true),
                "authn_file_module" => ("File Authentication", "File-based authentication", "Security", false),
                "access_compat_module" => ("Access Compatibility", "Compatibility for 2.2-style access control", "Compatibility", false),
                "proxy_module" => ("Proxy", "HTTP proxy and gateway functionality", "Proxy", false),
                "proxy_ajp_module" => ("Proxy AJP", "AJP proxy support", "Proxy", false),
                "proxy_balancer_module" => ("Proxy Balancer", "Load balancing for proxy", "Proxy", false),
                "proxy_connect_module" => ("Proxy Connect", "CONNECT method proxy support", "Proxy", false),
                "proxy_express_module" => ("Proxy Express", "Express proxy support", "Proxy", false),
                "proxy_fcgi_module" => ("Proxy FastCGI", "FastCGI proxy support for PHP", "PHP", false),
                "proxy_ftp_module" => ("Proxy FTP", "FTP proxy support", "Proxy", false),
                "proxy_hcheck_module" => ("Proxy Health Check", "Health check for proxy backends", "Proxy", false),
                "proxy_html_module" => ("Proxy HTML", "HTML proxy support", "Proxy", false),
                "proxy_http_module" => ("Proxy HTTP", "HTTP proxy support", "Proxy", false),
                "proxy_http2_module" => ("Proxy HTTP/2", "HTTP/2 proxy support", "Proxy", false),
                "proxy_scgi_module" => ("Proxy SCGI", "SCGI proxy support", "Proxy", false),
                "proxy_uwsgi_module" => ("Proxy uWSGI", "uWSGI proxy support", "Proxy", false),
                "proxy_wstunnel_module" => ("Proxy WebSocket", "WebSocket tunnel proxy support", "Proxy", false),
                "cgi_module" => ("CGI", "Common Gateway Interface support", "CGI", false),
                "fcgid_module" => ("FastCGI", "FastCGI process manager", "PHP", false),
                "php_module" => ("PHP", "PHP scripting language support", "PHP", false),
                "status_module" => ("Server Status", "Server status and information", "Monitoring", false),
                "info_module" => ("Server Info", "Server configuration information", "Monitoring", false),
                "log_config_module" => ("Log Config", "Logging configuration", "Core", true),
                "logio_module" => ("Log I/O", "Input/output logging", "Monitoring", false),
                "env_module" => ("Environment", "Environment variable manipulation", "Core", true),
                "unique_id_module" => ("Unique ID", "Unique request identifier", "Monitoring", false),
                "userdir_module" => ("User Directories", "User home directory mapping", "Core", false),
                "alias_module" => ("Alias", "URL aliasing and redirection", "Core", true),
                "speling_module" => ("Spelling", "URL spelling correction", "Utility", false),
                "usertrack_module" => ("User Tracking", "User session tracking via cookies", "Monitoring", false),
                "dav_module" => ("WebDAV", "Web Distributed Authoring and Versioning", "WebDAV", false),
                "dav_fs_module" => ("WebDAV Filesystem", "WebDAV filesystem provider", "WebDAV", false),
                "dav_lock_module" => ("WebDAV Locking", "WebDAV locking support", "WebDAV", false),
                "vhost_alias_module" => ("Virtual Host Alias", "Mass virtual hosting", "Virtual Hosts", false),
                "actions_module" => ("Actions", "Execute scripts based on file type", "Core", true),
                "include_module" => ("Server Side Includes", "Server-side includes processing", "Core", false),
                "filter_module" => ("Filter", "Content filtering framework", "Core", false),
                "substitute_module" => ("Substitute", "Content substitution", "Utility", false),
                "sed_module" => ("Stream Editor", "Stream editing filter", "Utility", false),
                "charset_lite_module" => ("Character Set", "Character set translation", "Internationalization", false),
                _ => (moduleName.Replace("_module", "").Replace("_", " "), $"Apache module: {moduleName}", "Other", false)
            };

            return moduleInfo;
        }
    }
}
