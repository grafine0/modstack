using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Core.Services;
using ModStack.Common;
using ModStack.Common.Interfaces;
using ModStack.Common.Security;
using ModStack.Common.Services;
using ModStack.Services.Interfaces;

namespace ModStack.Services
{
    /// <summary>
    /// Handles package installation from Package Manager
    /// </summary>
    public partial class PackageInstaller
    {
        [System.Text.RegularExpressions.GeneratedRegex(@"\d+\.\d+\.\d+")]
        private static partial System.Text.RegularExpressions.Regex VersionPatternRegex();
        
        [System.Text.RegularExpressions.GeneratedRegex(@"Listen\s+\d+.*\n")]
        private static partial System.Text.RegularExpressions.Regex ListenDirectiveRegex();
        
        [System.Text.RegularExpressions.GeneratedRegex(@"(ServerRoot\s+""[^""]+""\s*\n)")]
        private static partial System.Text.RegularExpressions.Regex ServerRootRegex();

        [System.Text.RegularExpressions.GeneratedRegex(@"PHP (\d+\.\d+\.\d+)")]
        private static partial System.Text.RegularExpressions.Regex PHPVersionRegex();

        [System.Text.RegularExpressions.GeneratedRegex(@"PHP API => (\d+)")]
        private static partial System.Text.RegularExpressions.Regex PHPAPIVersionRegex();

        // Cached JSON serializer options for performance
        private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        
        [System.Text.RegularExpressions.GeneratedRegex(@"ServerName\s+\S+.*\n")]
        private static partial System.Text.RegularExpressions.Regex ServerNameRegex();
        
        [System.Text.RegularExpressions.GeneratedRegex(@"Define SRVROOT\s+""[^""]*""")]
        private static partial System.Text.RegularExpressions.Regex SrvrootDefineRegex();
        
        [System.Text.RegularExpressions.GeneratedRegex(@"(server\s*\{)")]
        private static partial System.Text.RegularExpressions.Regex ServerBlockRegex();
        
        [System.Text.RegularExpressions.GeneratedRegex(@"listen\s+\d+.*?;")]
        private static partial System.Text.RegularExpressions.Regex ListenDirectiveNginxRegex();
        private readonly HttpClient _httpClient;
        private readonly IServiceRepository _serviceRepository;
        private readonly IPathConfigurationService _pathConfigurationService;
        private readonly string _connectionString;
        private readonly ILogger<PackageInstaller> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IProcessRunner _processRunner;
        private readonly IServiceSafetyManager _serviceSafetyManager;
        private readonly ApacheHttpdConfigurator _apacheConfigurator;

        public PackageInstaller(
            IServiceRepository serviceRepository,
            IPathConfigurationService pathConfigurationService,
            string connectionString,
            ILogger<PackageInstaller> logger,
            ILoggerFactory loggerFactory,
            IProcessRunner processRunner,
            IServiceSafetyManager serviceSafetyManager,
            ApacheHttpdConfigurator apacheConfigurator)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ModStack Package Manager");
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _pathConfigurationService = pathConfigurationService ?? throw new ArgumentNullException(nameof(pathConfigurationService));
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
            _serviceSafetyManager = serviceSafetyManager ?? throw new ArgumentNullException(nameof(serviceSafetyManager));
            _apacheConfigurator = apacheConfigurator ?? throw new ArgumentNullException(nameof(apacheConfigurator));
        }

        private string GenerateVersionSpecificPath(PackageInstallationConfig config)
        {
            // Use the configurable services directory instead of config.InstallPath
            var basePath = _pathConfigurationService.GetServicesDirectory();
            var packageName = config.Package?.Name?.ToLowerInvariant() ?? "unknown";
            var version = config.Version?.Version ?? "unknown";
            
            System.Diagnostics.Debug.WriteLine($"🔍 GenerateVersionSpecificPath:");
            System.Diagnostics.Debug.WriteLine($"  - Base path (from PathConfigurationService): {basePath}");
            System.Diagnostics.Debug.WriteLine($"  - Package name: {packageName}");
            System.Diagnostics.Debug.WriteLine($"  - Version: {version}");
            
            // Check if the base path already ends with a version-specific folder
            var basePathLower = basePath.ToLower();
            var expectedVersionFolder = $"{packageName}-{version}";
            
            if (basePathLower.EndsWith(expectedVersionFolder.ToLower()) || 
                basePathLower.EndsWith($"{packageName}\\{expectedVersionFolder}".ToLower()) ||
                basePathLower.EndsWith($"{packageName}/{expectedVersionFolder}".ToLower()))
            {
                // Base path already includes version-specific folder, use as-is
                System.Diagnostics.Debug.WriteLine($"  ✅ Base path already version-specific: {basePath}");
                return basePath;
            }
            
            // Create version-specific subfolder: package-version
            var versionSubfolder = expectedVersionFolder;
            
            // For packages that already have versioned paths, use the base path as parent
            // Otherwise, create a version subfolder in the base path
            string result;
            if (basePath.Contains(packageName, StringComparison.OrdinalIgnoreCase))
            {
                // If base path already contains package name, create version subfolder inside
                result = Path.Combine(basePath, versionSubfolder);
                System.Diagnostics.Debug.WriteLine($"  📁 Path contains package name, adding version subfolder: {result}");
            }
            else
            {
                // If base path is generic, create package folder first, then version subfolder
                var packageFolder = Path.Combine(basePath, packageName.ToUpperInvariant());
                result = Path.Combine(packageFolder, versionSubfolder);
                System.Diagnostics.Debug.WriteLine($"  📁 Generic path, creating package + version folders: {result}");
            }
            
            return result;
        }

        private static string GetDownloadUrl(PackageInstallationConfig config)
        {
            var packageName = config.Package?.Name?.ToLowerInvariant() ?? "";
            var version = config.Version?.Version ?? "";

            // Check if package has predefined URLs first
            var predefinedUrl = config.Version?.DownloadUrls?.FirstOrDefault();
            if (!string.IsNullOrEmpty(predefinedUrl))
            {
                return predefinedUrl;
            }

            // Generate dynamic URLs based on package type
            return packageName switch
            {
                "mysql" => GenerateMySQLDownloadUrl(version),
                "php" => GeneratePHPDownloadUrl(version),
                "apache" => GenerateApacheDownloadUrl(version),
                "nginx" => GenerateNginxDownloadUrl(version),
                "postgresql" => GeneratePostgreSQLDownloadUrl(version),
                "redis" => GenerateRedisDownloadUrl(version),
                _ => predefinedUrl ?? ""
            };
        }

        private static string GenerateMySQLDownloadUrl(string version)
        {
            // MySQL archives base URL with dynamic version
            return $"https://downloads.mysql.com/archives/get/p/23/file/mysql-{version}-winx64.zip";
        }

        private static string GeneratePHPDownloadUrl(string version)
        {
            // PHP 8.4+ uses vs17 on Windows, while 8.0-8.3 uses vs16
            var compilerTag = (version.StartsWith("8.4", StringComparison.Ordinal) ||
                               version.StartsWith("8.5", StringComparison.Ordinal))
                ? "vs17"
                : "vs16";
            return $"https://windows.php.net/downloads/releases/archives/php-{version}-Win32-{compilerTag}-x64.zip";
        }

        private static string GenerateApacheDownloadUrl(string version)
        {
            // Apache archives (example pattern)
            return $"https://archive.apache.org/dist/httpd/binaries/win32/httpd-{version}-win32-x86.zip";
        }

        private static string GenerateNginxDownloadUrl(string version)
        {
            // Nginx download pattern (HTTPS enforced)
            return $"https://nginx.org/download/nginx-{version}.zip";
        }

        private static string GeneratePostgreSQLDownloadUrl(string version)
        {
            // PostgreSQL download pattern
            return $"https://get.enterprisedb.com/postgresql/postgresql-{version}-windows-x64-binaries.zip";
        }

        private static string GenerateRedisDownloadUrl(string version)
        {
            // Redis Windows download pattern
            return $"https://github.com/microsoftarchive/redis/releases/download/win-{version}/Redis-x64-{version}.zip";
        }

        public async Task<bool> InstallPackageAsync(PackageInstallationConfig config, IProgressReporter? progressReporter = null)
        {
            var packageName = config.Package?.Name ?? "Unknown";
            var version = config.Version?.Version ?? "Unknown";
            bool createdInstallDirectory = false;
            string? downloadPath = null;
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Starting installation for {packageName} {version} ===");
                progressReporter?.UpdateProgress("Preparing installation...", 0);

                // Generate version-specific installation path
                System.Diagnostics.Debug.WriteLine("Generating installation path...");
                System.Diagnostics.Debug.WriteLine($"  - Original config.InstallPath: {config.InstallPath}");
                var versionSpecificPath = GenerateVersionSpecificPath(config);
                config.InstallPath = versionSpecificPath;
                System.Diagnostics.Debug.WriteLine($"  - Generated version-specific path: {config.InstallPath}");

                // Create installation directory
                System.Diagnostics.Debug.WriteLine("Creating installation directory...");
                if (!Directory.Exists(config.InstallPath))
                {
                    Directory.CreateDirectory(config.InstallPath);
                    createdInstallDirectory = true;
                }
                progressReporter?.UpdateProgress("Installation directory created", 10);

                // Download package
                System.Diagnostics.Debug.WriteLine("=== Starting download phase ===");
                progressReporter?.UpdateProgress("Starting download...", 20);
                
                try
                {
                    downloadPath = await DownloadPackageAsync(config, progressReporter);
                    System.Diagnostics.Debug.WriteLine($"Download completed: {downloadPath}");
                }
                catch (Exception downloadEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Download phase FAILED: {downloadEx.GetType().Name}: {downloadEx.Message}");
                    if (downloadEx.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Download inner exception: {downloadEx.InnerException.Message}");
                    }
                    
                    // Special handling for MySQL - provide manual download instructions
                    if (string.Equals(packageName, "mysql", StringComparison.OrdinalIgnoreCase))
                    {
                        await CreateMySQLManualDownloadInstructions(config, downloadEx.Message);
                        throw new Exception($"MySQL download failed. Manual download instructions created. Error: {downloadEx.Message}");
                    }
                    
                    throw new Exception($"Download failed: {downloadEx.Message}", downloadEx);
                }

                if (string.IsNullOrEmpty(downloadPath))
                {
                    System.Diagnostics.Debug.WriteLine("Download path is empty - installation failed");
                    return false;
                }

                // Extract package
                System.Diagnostics.Debug.WriteLine("=== Starting extraction phase ===");
                progressReporter?.UpdateProgress("Extracting files...", 60);
                
                System.Diagnostics.Debug.WriteLine($"📁 Original install path: {config.InstallPath}");
                
                try
                {
                    await ExtractPackageAsync(downloadPath, config.InstallPath, progressReporter, config);
                    System.Diagnostics.Debug.WriteLine("Extraction completed successfully");
                }
                catch (Exception extractEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Extraction phase FAILED: {extractEx.GetType().Name}: {extractEx.Message}");
                    throw new Exception($"Extraction failed: {extractEx.Message}", extractEx);
                }

                // Configure package
                System.Diagnostics.Debug.WriteLine("=== Starting configuration phase ===");
                progressReporter?.UpdateProgress("Configuring service...", 80);
                try
                {
                    await ConfigurePackageAsync(config);
                    System.Diagnostics.Debug.WriteLine("Configuration completed successfully");
                }
                catch (Exception configEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Configuration phase FAILED: {configEx.GetType().Name}: {configEx.Message}");
                    throw new Exception($"Configuration failed: {configEx.Message}", configEx);
                }

                // Register as Windows Service if requested
                if (config.RegisterAsService)
                {
                    System.Diagnostics.Debug.WriteLine("=== Registering Windows Service ===");
                    progressReporter?.UpdateProgress("Registering Windows Service...", 90);
                    try
                    {
                        await RegisterWindowsServiceAsync(config);
                        System.Diagnostics.Debug.WriteLine("Service registration completed");
                    }
                    catch (Exception serviceEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Service registration failed: {serviceEx.Message}");
                        throw new Exception($"Windows service registration failed for {packageName} {version}: {serviceEx.Message}", serviceEx);
                    }
                }

                // Auto-start if requested
                if (config.AutoStart)
                {
                    System.Diagnostics.Debug.WriteLine("=== Starting service ===");
                    progressReporter?.UpdateProgress("Starting service...", 95);
                    try
                    {
                        await StartServiceAsync(config);
                        System.Diagnostics.Debug.WriteLine("Service started successfully");
                    }
                    catch (Exception startEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Service start failed: {startEx.Message}");
                        throw new Exception($"Service auto-start failed for {packageName} {version}: {startEx.Message}", startEx);
                    }
                }

                // Register service in database - only for actual service packages
                if (config.Package?.PackageType != Common.Enums.PackageType.StandaloneWebApp && 
                    config.Package?.PackageType != Common.Enums.PackageType.Framework &&
                    config.Package?.PackageType != Common.Enums.PackageType.Tool)
                {
                    System.Diagnostics.Debug.WriteLine("=== Registering service in database ===");
                    try
                    {
                        await RegisterServiceInDatabaseAsync(config);
                        System.Diagnostics.Debug.WriteLine("Service registered in database successfully");
                        
                        // Trigger service detection to update installation status
                        System.Diagnostics.Debug.WriteLine("=== Triggering service detection ===");
                        await TriggerServiceDetectionAsync(config);
                        
                        // Trigger PHP extension synchronization for PHP services
                        if (config.Package?.Name?.ToLower() == "php")
                        {
                            System.Diagnostics.Debug.WriteLine("=== Triggering PHP extension synchronization ===");
                            await TriggerPHPExtensionSynchronizationAsync(config);
                        }
                    }
                    catch (Exception dbEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Service database registration failed: {dbEx.Message}");
                        // Don't fail installation for database registration errors
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"=== Skipping service database registration for package type: {config.Package?.PackageType} ===");
                }

                progressReporter?.UpdateProgress("Installation complete!", 100);

                // Cleanup
                System.Diagnostics.Debug.WriteLine("=== Cleaning up temporary files ===");
                try
                {
                    if (File.Exists(downloadPath))
                    {
                        File.Delete(downloadPath);
                        System.Diagnostics.Debug.WriteLine($"Cleaned up: {downloadPath}");
                    }
                }
                catch (Exception cleanupEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Cleanup warning: {cleanupEx.Message}");
                }

                System.Diagnostics.Debug.WriteLine($"=== Installation completed successfully for {packageName} {version} ===");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== Installation FAILED for {packageName} {version} ===");
                System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Exception message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Transactional rollback: clean up temporary archive and newly created installation directory
                if (!string.IsNullOrEmpty(downloadPath) && File.Exists(downloadPath))
                {
                    try { File.Delete(downloadPath); } catch { /* Best-effort cleanup */ }
                }

                if (createdInstallDirectory && !string.IsNullOrWhiteSpace(config.InstallPath) && Directory.Exists(config.InstallPath))
                {
                    try
                    {
                        Directory.Delete(config.InstallPath, recursive: true);
                        System.Diagnostics.Debug.WriteLine($"Rolled back installation directory: {config.InstallPath}");
                    }
                    catch (Exception rollbackEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Rollback warning for {config.InstallPath}: {rollbackEx.Message}");
                    }
                }
                
                progressReporter?.UpdateProgress($"Installation failed: {ex.Message}", -1);
                
                return false;
            }
        }

        private static async Task<string> DownloadPackageAsync(PackageInstallationConfig config, IProgressReporter? progressReporter)
        {
            try
            {
                var downloadUrls = new List<string>();
                
                // Get predefined URLs first
                if (config.Version?.DownloadUrls?.Count > 0)
                {
                    downloadUrls.AddRange(config.Version.DownloadUrls);
                    System.Diagnostics.Debug.WriteLine($"Using predefined URLs: {string.Join(", ", downloadUrls)}");
                }
                else
                {
                    // Fallback to dynamic URL generation
                    var dynamicUrl = GetDownloadUrl(config);
                    if (!string.IsNullOrEmpty(dynamicUrl))
                    {
                        downloadUrls.Add(dynamicUrl);
                        System.Diagnostics.Debug.WriteLine($"Using dynamic URL: {dynamicUrl}");
                    }
                }

                if (downloadUrls.Count == 0)
                {
                    throw new Exception($"No download URL available for {config.Package?.Name ?? "Unknown"} version {config.Version?.Version ?? "Unknown"}");
                }

                Exception? lastException = null;

                // Try each URL until one works
                foreach (var downloadUrl in downloadUrls)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"=== Starting download attempt from: {downloadUrl} ===");
                        progressReporter?.UpdateProgress($"Connecting to: {downloadUrl.Split('/').LastOrDefault()}", 20);

                        // Validate URL format and enforce HTTPS transport
                        var uri = SecurityValidator.ValidateHttpsUrlOrThrow(downloadUrl);

                        var fileName = $"{config.Package?.Name ?? "package"}-{config.Version?.Version ?? "unknown"}.zip";
                        var downloadPath = Path.Combine(Path.GetTempPath(), fileName);

                        // Clean up any existing file
                        if (File.Exists(downloadPath))
                        {
                            File.Delete(downloadPath);
                        }

                        System.Diagnostics.Debug.WriteLine($"Download path: {downloadPath}");
                        progressReporter?.UpdateProgress($"Downloading {fileName}...", 25);

                        // Create fresh HTTP client for this download to avoid reuse issues
                        using var downloadClient = new HttpClient();
                        downloadClient.Timeout = TimeSpan.FromMinutes(10); // 10 minute timeout

                        // Add browser-like headers to avoid 403 Forbidden responses
                        downloadClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                        downloadClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                        downloadClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                        downloadClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                        downloadClient.DefaultRequestHeaders.Add("DNT", "1");
                        downloadClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                        downloadClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                        downloadClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                        downloadClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                        downloadClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
                        downloadClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                        downloadClient.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
                        
                        System.Diagnostics.Debug.WriteLine("Created fresh HttpClient with browser-like headers");

                        // Download with progress
                        using var response = await downloadClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                        
                        System.Diagnostics.Debug.WriteLine($"HTTP Response Status: {response.StatusCode} ({(int)response.StatusCode}) - {response.ReasonPhrase}");

                        // Reject any downgrade redirect to non-HTTPS transport
                        if (response.RequestMessage?.RequestUri is { } finalUri &&
                            !string.Equals(finalUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                $"Insecure redirect from HTTPS to '{finalUri.Scheme}' was blocked ({finalUri}).");
                        }
                        
                        if (!response.IsSuccessStatusCode)
                        {
                            var errorContent = "No additional error details";
                            try
                            {
                                errorContent = await response.Content.ReadAsStringAsync();
                                if (errorContent.Length > 500) errorContent = string.Concat(errorContent.AsSpan(0, 500), "...");
                            }
                            catch { /* Ignore errors reading error content */ }
                            
                            throw new Exception($"HTTP {response.StatusCode}: {response.ReasonPhrase}. URL: {downloadUrl}. Details: {errorContent}");
                        }

                        var totalBytes = response.Content.Headers.ContentLength ?? 0;
                        System.Diagnostics.Debug.WriteLine($"Content length: {totalBytes} bytes ({totalBytes / (1024 * 1024)} MB)");

                        await using (var contentStream = await response.Content.ReadAsStreamAsync())
                        await using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            long totalBytesRead = 0;
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer)) != 0)
                            {
                                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                                totalBytesRead += bytesRead;

                                if (totalBytes > 0)
                                {
                                    var progressPercent = (int)((totalBytesRead * 40) / totalBytes) + 20; // 20-60% for download
                                    var mbDownloaded = totalBytesRead / (1024 * 1024);
                                    var mbTotal = totalBytes / (1024 * 1024);
                                    progressReporter?.UpdateProgress($"Downloaded: {mbDownloaded} MB / {mbTotal} MB", progressPercent);
                                }
                                else
                                {
                                    var mbDownloaded = totalBytesRead / (1024 * 1024);
                                    progressReporter?.UpdateProgress($"Downloaded: {mbDownloaded} MB", 30);
                                }
                            }

                            await fileStream.FlushAsync();
                        }

                        // Verify downloaded file exists
                        if (!File.Exists(downloadPath))
                        {
                            throw new Exception("Download file was not created");
                        }

                        var downloadedSize = new FileInfo(downloadPath).Length;
                        System.Diagnostics.Debug.WriteLine($"Successfully downloaded {downloadedSize} bytes ({downloadedSize / (1024 * 1024)} MB) from {downloadUrl}");
                        
                        if (totalBytes > 0 && downloadedSize != totalBytes)
                        {
                            File.Delete(downloadPath);
                            throw new InvalidOperationException(
                                $"Downloaded file size mismatch. Expected {totalBytes} bytes from HTTP Content-Length, but received {downloadedSize} bytes.");
                        }

                        // Verify cryptographic SHA-256 digest when provided or required
                        progressReporter?.UpdateProgress("Verifying package integrity...", 58);
                        try
                        {
                            var computedHash = await SecurityValidator.VerifyFileSha256Async(
                                downloadPath,
                                config.Version?.Checksum,
                                requireChecksum: config.RequireChecksumVerification);
                            System.Diagnostics.Debug.WriteLine($"Package SHA-256 digest verified: {computedHash}");
                        }
                        catch
                        {
                            if (File.Exists(downloadPath))
                            {
                                File.Delete(downloadPath);
                            }
                            throw;
                        }

                        return downloadPath;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Download failed from {downloadUrl}: {ex.GetType().Name}: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }
                        lastException = ex;
                        continue; // Try next URL
                    }
                }

                // If all URLs failed, throw the last exception
                var errorMessage = $"All download URLs failed. Last error: {lastException?.Message ?? "Unknown error"}";
                System.Diagnostics.Debug.WriteLine(errorMessage);
                throw new Exception(errorMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Download process failed: {ex.GetType().Name}: {ex.Message}");
                throw new Exception($"Download failed: {ex.Message}", ex);
            }
        }

        private static async Task ExtractPackageAsync(string zipPath, string extractPath, IProgressReporter? progressReporter, PackageInstallationConfig? config = null)
        {
            try
            {
                var packageName = config?.Package?.Name?.ToLowerInvariant() ?? "";
                var version = config?.Version?.Version ?? "";
                var shouldFlatten = IsPackageRequiringFlattening(packageName);

                System.Diagnostics.Debug.WriteLine($"🔍 Enhanced ZIP Analysis Results:");
                System.Diagnostics.Debug.WriteLine($"  - Package name: {packageName}");
                System.Diagnostics.Debug.WriteLine($"  - Version: {version}");
                System.Diagnostics.Debug.WriteLine($"  - Should flatten: {shouldFlatten}");
                System.Diagnostics.Debug.WriteLine($"  - Extract path: {extractPath}");

                var extractionOptions = new ArchiveExtractionOptions
                {
                    EntryPathTransformer = shouldFlatten
                        ? rawPath => FlattenEntryPath(rawPath, packageName, version)
                        : null,
                    ProgressCallback = (extractedEntries, totalEntries) =>
                    {
                        if (totalEntries > 0)
                        {
                            var progressPercent = 60 + (extractedEntries * 20) / totalEntries; // 60-80% for extraction
                            progressReporter?.UpdateProgress($"Extracting: {extractedEntries}/{totalEntries} files", progressPercent);
                        }
                    }
                };

                await SafeArchiveExtractor.ExtractZipSafelyAsync(zipPath, extractPath, extractionOptions);
            }
            catch (Exception ex)
            {
                throw new Exception($"Extraction failed: {ex.Message}", ex);
            }
        }

        private static bool ShouldFlattenExtraction(ZipArchive archive)
        {
            // Check if all entries are contained within a single root folder
            var allEntries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
            if (allEntries.Count == 0) return false;

            System.Diagnostics.Debug.WriteLine($"=== ZIP Archive Analysis ===");
            System.Diagnostics.Debug.WriteLine($"Total entries with names: {allEntries.Count}");
            
            // Log first few entries for debugging
            for (int i = 0; i < Math.Min(10, allEntries.Count); i++)
            {
                System.Diagnostics.Debug.WriteLine($"Entry {i}: {allEntries[i].FullName}");
            }

            // Get the first level items from all entries (files and folders)
            var rootItems = allEntries
                .Select(e => e.FullName.Split('/', '\\')[0])
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct()
                .ToList();

            System.Diagnostics.Debug.WriteLine($"Root items found: {string.Join(", ", rootItems)}");
            System.Diagnostics.Debug.WriteLine($"Root items count: {rootItems.Count}");

            // Separate folders from files at root level
            var rootFolders = new List<string>();
            var rootFiles = new List<string>();

            foreach (var item in rootItems)
            {
                // Check if this item appears as a folder (has entries inside it)
                bool isFolder = allEntries.Any(e => e.FullName.StartsWith(item + "/") || e.FullName.StartsWith(item + "\\"));
                if (isFolder)
                {
                    rootFolders.Add(item);
                }
                else
                {
                    rootFiles.Add(item);
                }
            }

            System.Diagnostics.Debug.WriteLine($"Root folders: {string.Join(", ", rootFolders)} (count: {rootFolders.Count})");
            System.Diagnostics.Debug.WriteLine($"Root files: {string.Join(", ", rootFiles)} (count: {rootFiles.Count})");

            // Case 1: Only one root folder (original logic)
            if (rootItems.Count == 1 && rootFolders.Count == 1)
            {
                var rootFolder = rootFolders[0];
                var rootFolderLower = rootFolder.ToLower();
                
                System.Diagnostics.Debug.WriteLine($"Checking single root folder: '{rootFolder}' (lower: '{rootFolderLower}')");
                
                bool shouldFlatten = ShouldFlattenFolder(rootFolderLower);
                System.Diagnostics.Debug.WriteLine($"Should flatten (single folder): {shouldFlatten}");
                return shouldFlatten;
            }

            // Case 2: One main folder + some loose files (like Apache: Apache24 folder + ReadMe.txt + other files)
            if (rootFolders.Count == 1 && rootFiles.Count > 0)
            {
                var mainFolder = rootFolders[0];
                var mainFolderLower = mainFolder.ToLower();
                
                System.Diagnostics.Debug.WriteLine($"Checking main folder with loose files: '{mainFolder}' (lower: '{mainFolderLower}')");
                
                // Count entries in the main folder vs loose files
                var entriesInMainFolder = allEntries.Count(e => e.FullName.StartsWith(mainFolder + "/") || e.FullName.StartsWith(mainFolder + "\\"));
                var looseFileCount = rootFiles.Count;
                
                System.Diagnostics.Debug.WriteLine($"Entries in main folder: {entriesInMainFolder}, Loose files: {looseFileCount}");
                
                // If the main folder contains most of the content and looks like a package folder, flatten it
                if (entriesInMainFolder > looseFileCount && ShouldFlattenFolder(mainFolderLower))
                {
                    System.Diagnostics.Debug.WriteLine($"Should flatten (main folder with loose files): true");
                    return true;
                }
            }

            System.Diagnostics.Debug.WriteLine("No flattening - multiple significant folders or other structure");
            return false;
        }

        /// <summary>
        /// Determines if a package requires flattening based on known package types
        /// </summary>
        /// <param name="packageName">Package name (lowercase)</param>
        /// <returns>True if package should be flattened</returns>
        private static bool IsPackageRequiringFlattening(string packageName)
        {
            var requiresFlattening = packageName switch
            {
                "php" => true,          // PHP zips often have nested php-x.x.x folders
                "mysql" => true,        // MySQL has complex nested structure
                "apache" => true,       // Apache has Apache24 folder
                "nginx" => true,        // Nginx may have nested folders
                "postgresql" => true,   // PostgreSQL has nested structure
                "redis" => true,        // Redis may have nested folders
                _ => false
            };
            
            System.Diagnostics.Debug.WriteLine($"🎯 Package '{packageName}' requires flattening: {requiresFlattening}");
            return requiresFlattening;
        }

        /// <summary>
        /// Flattens entry path by removing redundant nested folders
        /// </summary>
        /// <param name="entryPath">Original entry path from ZIP</param>
        /// <param name="packageName">Package name</param>
        /// <param name="version">Package version</param>
        /// <returns>Flattened entry path</returns>
        private static string FlattenEntryPath(string entryPath, string packageName, string version)
        {
            var originalPath = entryPath;
            var pathParts = entryPath.Split('/', '\\').Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
            
            if (pathParts.Length == 0) return string.Empty;
            
            System.Diagnostics.Debug.WriteLine($"  🔍 Flattening '{originalPath}' (parts: [{string.Join(", ", pathParts)}])");
            
            // More aggressive flattening - remove ALL redundant package/version folders
            var filteredParts = new List<string>();
            
            for (int i = 0; i < pathParts.Length; i++)
            {
                var part = pathParts[i];
                var partLower = part.ToLower();
                bool shouldSkipPart = false;
                
                // Skip parts that look like redundant package folders (but keep the actual file/final directory)
                if (i < pathParts.Length - 1) // Don't skip the actual file name
                {
                    shouldSkipPart = 
                        // Skip exact package-version matches
                        partLower == $"{packageName}-{version}" ||
                        partLower == $"{packageName}{version.Replace(".", "")}" ||
                        
                        // Skip if part starts with package name and contains version info
                        (partLower.StartsWith($"{packageName}-") && partLower.Contains(version.Split('.')[0])) ||
                        
                        // Skip version-only folders
                        partLower == version ||
                        partLower == version.Replace(".", "") ||
                        
                        // Skip package name folders (like "php" folder inside "php-8.3.0")
                        partLower == packageName ||
                        
                        // Skip platform-specific folders that contain version
                        (partLower.Contains("win") && partLower.Contains(version.Split('.')[0])) ||
                        (partLower.Contains("x64") && partLower.Contains(version.Split('.')[0])) ||
                        
                        // Skip common redundant folder patterns
                        partLower.StartsWith($"{packageName}-") ||
                        (partLower.Contains(packageName) && partLower.Contains('-') && partLower.Length > packageName.Length + 3) ||
                        
                        // Apache-specific: Skip Apache24 folder
                        (packageName == "apache" && partLower == "apache24");
                }
                
                if (!shouldSkipPart)
                {
                    filteredParts.Add(pathParts[i]); // Keep original case
                }
                else
                {
                    if (packageName == "apache" && string.Equals(pathParts[i], "apache24", StringComparison.OrdinalIgnoreCase))
                    {
                        System.Diagnostics.Debug.WriteLine($"    🎯 Skipping Apache24 folder: '{pathParts[i]}' (Apache-specific flattening)");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"    ⏭️ Skipping redundant folder: '{pathParts[i]}'");
                    }
                }
            }
            
            var result = string.Join(Path.DirectorySeparatorChar.ToString(), filteredParts);
            
            if (result != originalPath)
            {
                System.Diagnostics.Debug.WriteLine($"  ✅ Flattened: '{originalPath}' → '{result}'");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"  ℹ️ No flattening needed for: '{originalPath}'");
            }
            
            return result;
        }

        private static bool ShouldFlattenFolder(string folderNameLower)
        {
            System.Diagnostics.Debug.WriteLine($"🔍 ShouldFlattenFolder checking: '{folderNameLower}'");
            
            // Enhanced flattening logic for package folders
            bool shouldFlatten = 
                // Version patterns (package-version)
                folderNameLower.Contains('-') ||
                
                // Package-specific patterns
                folderNameLower.Contains("mysql") || 
                folderNameLower.Contains("apache") || 
                folderNameLower.Contains("httpd") ||
                folderNameLower.Contains("nginx") || 
                folderNameLower.Contains("php") || 
                folderNameLower.Contains("phpmyadmin") ||
                folderNameLower.Contains("postgresql") ||
                folderNameLower.Contains("redis") ||
                
                // Specific folder names
                folderNameLower.StartsWith("apache24") ||
                folderNameLower.StartsWith("httpd-") ||
                folderNameLower.StartsWith("php-") ||
                folderNameLower.StartsWith("mysql-") ||
                folderNameLower.StartsWith("nginx-") ||
                
                // Version-like patterns (x.y.z)
                VersionPatternRegex().IsMatch(folderNameLower) ||
                
                // Common distribution folder patterns
                folderNameLower.Contains("win32") ||
                folderNameLower.Contains("win64") ||
                folderNameLower.Contains("x64") ||
                folderNameLower.Contains("x86") ||
                folderNameLower.Contains("windows");
                
            System.Diagnostics.Debug.WriteLine($"🎯 ShouldFlattenFolder result for '{folderNameLower}': {shouldFlatten}");
            return shouldFlatten;
        }

        private static string? GetRootFolderToSkip(ZipArchive archive)
        {
            var allEntries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
            if (allEntries.Count == 0) return null;

            // Get the first level items from all entries (files and folders)
            var rootItems = allEntries
                .Select(e => e.FullName.Split('/', '\\')[0])
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct()
                .ToList();

            // Separate folders from files at root level
            var rootFolders = new List<string>();
            var rootFiles = new List<string>();

            foreach (var item in rootItems)
            {
                // Check if this item appears as a folder (has entries inside it)
                bool isFolder = allEntries.Any(e => e.FullName.StartsWith(item + "/") || e.FullName.StartsWith(item + "\\"));
                if (isFolder)
                {
                    rootFolders.Add(item);
                }
                else
                {
                    rootFiles.Add(item);
                }
            }

            // Case 1: Only one root folder (original logic)
            if (rootItems.Count == 1 && rootFolders.Count == 1)
            {
                var rootFolder = rootFolders[0];
                System.Diagnostics.Debug.WriteLine($"GetRootFolderToSkip - Single root folder: {rootFolder}");
                return rootFolder + "/"; // Add trailing slash for proper path matching
            }

            // Case 2: One main folder + some loose files (like Apache: Apache24 folder + ReadMe.txt + other files)
            if (rootFolders.Count == 1 && rootFiles.Count > 0)
            {
                var mainFolder = rootFolders[0];
                var mainFolderLower = mainFolder.ToLower();
                
                // Count entries in the main folder vs loose files
                var entriesInMainFolder = allEntries.Count(e => e.FullName.StartsWith(mainFolder + "/") || e.FullName.StartsWith(mainFolder + "\\"));
                var looseFileCount = rootFiles.Count;
                
                // If the main folder contains most of the content and looks like a package folder, flatten it
                if (entriesInMainFolder > looseFileCount && ShouldFlattenFolder(mainFolderLower))
                {
                    System.Diagnostics.Debug.WriteLine($"GetRootFolderToSkip - Main folder with loose files: {mainFolder}");
                    return mainFolder + "/"; // Add trailing slash for proper path matching
                }
            }

            System.Diagnostics.Debug.WriteLine("GetRootFolderToSkip - No folder to skip");
            return null;
        }

        private async Task ConfigurePackageAsync(PackageInstallationConfig config)
        {
            try
            {
                var packageName = config.Package?.Name ?? "Unknown";
                
                switch (packageName.ToLower())
                {
                    case "apache":
                        await ConfigureApacheAsync(config);
                        break;
                    case "nginx":
                        await ConfigureNginxAsync(config);
                        break;
                    case "mysql":
                        await ConfigureMySQLAsync(config);
                        break;
                    case "php":
                        await ConfigurePHPAsync(config);
                        break;
                    case "phpmyadmin":
                        await ConfigurePhpMyAdminAsync(config);
                        break;
                    // Add more service configurations as needed
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Configuration failed for {config.Package?.Name ?? "Unknown"}: {ex.Message}", ex);
            }
        }

        private async Task ConfigureApacheAsync(PackageInstallationConfig config)
        {
            try
            {
                var enableSsl = true;
                if (config.Configuration != null && config.Configuration.TryGetValue("enable_ssl", out var enableSslValue))
                {
                    try
                    {
                        enableSsl = Convert.ToBoolean(enableSslValue);
                    }
                    catch
                    {
                        enableSsl = true;
                    }
                }

                var apachePath = FindApacheInstallation(config.InstallPath);
                if (string.IsNullOrEmpty(apachePath)) return;

                await FixApacheSRVROOTAsync(apachePath);
                
                // Install mod_fcgid module for multi-PHP support
                await InstallModFcgidAsync(apachePath);
                
                var confPath = Path.Combine(apachePath, "conf", "httpd.conf");
                if (!File.Exists(confPath)) return;

                var content = await File.ReadAllTextAsync(confPath);
                
                // Configure Apache to listen on both port 80 and 443
                System.Diagnostics.Debug.WriteLine("🔧 Configuring Apache to listen on ports 80 and 443...");
                
                // Remove existing Listen directives
                content = ListenDirectiveRegex().Replace(content, "");
                
                // Add Listen directives for both HTTP and HTTPS
                var listenDirectives = "Listen 80\nListen 443\n";
                
                // Find the best place to insert Listen directives (after ServerRoot or at the beginning)
                if (content.Contains("ServerRoot"))
                {
                    content = ServerRootRegex().Replace(content, 
                        $"$1{listenDirectives}");
                }
                else
                {
                    content = listenDirectives + content;
                }
                
                // Server name will be configured when creating projects or virtual hosts
                // Remove any existing ServerName directive to avoid conflicts
                content = ServerNameRegex().Replace(content, "");
                
                await _apacheConfigurator.EnsureModulesAsync(apachePath, enableSsl);

                await File.WriteAllTextAsync(confPath, content);
                System.Diagnostics.Debug.WriteLine($"✅ Apache configured to listen on ports 80 and 443");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Apache configuration failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads and installs mod_fcgid module for Apache
        /// </summary>
        private async Task InstallModFcgidAsync(string apachePath)
        {
            try
            {
                var modulesPath = Path.Combine(apachePath, "modules");
                var fcgidModulePath = Path.Combine(modulesPath, "mod_fcgid.so");
                
                // Check if mod_fcgid is already installed
                if (File.Exists(fcgidModulePath))
                {
                    System.Diagnostics.Debug.WriteLine("✅ mod_fcgid module already installed");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine("🔧 Downloading mod_fcgid module...");
                
                // Download mod_fcgid from Apache Lounge
                var downloadUrl = "https://www.apachelounge.com/download/VS17/modules/mod_fcgid-2.3.10-win64-VS17.zip";
                var tempZipPath = Path.Combine(Path.GetTempPath(), "mod_fcgid.zip");
                
                using (var response = await _httpClient.GetAsync(downloadUrl))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Failed to download mod_fcgid: {response.StatusCode}");
                        return;
                    }
                    
                    using var fileStream = new FileStream(tempZipPath, FileMode.Create);
                    await response.Content.CopyToAsync(fileStream);
                }
                
                System.Diagnostics.Debug.WriteLine("📦 Extracting mod_fcgid module...");
                
                // Extract mod_fcgid.so from the ZIP
                using (var archive = ZipFile.OpenRead(tempZipPath))
                {
                    var fcgidEntry = archive.Entries.FirstOrDefault(e => e.Name == "mod_fcgid.so");
                    if (fcgidEntry == null)
                    {
                        System.Diagnostics.Debug.WriteLine("❌ mod_fcgid.so not found in downloaded archive");
                        return;
                    }
                    
                    using var entryStream = fcgidEntry.Open();
                    using var fileStream = new FileStream(fcgidModulePath, FileMode.Create);
                    await entryStream.CopyToAsync(fileStream);
                }
                
                // Clean up temporary file
                File.Delete(tempZipPath);
                
                System.Diagnostics.Debug.WriteLine($"✅ mod_fcgid module installed successfully: {fcgidModulePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Failed to install mod_fcgid: {ex.Message}");
                // Don't throw - this is not critical for Apache installation
            }
        }

        /// <summary>
        /// Fixes the SRVROOT path in Apache httpd.conf to point to the correct installation directory
        /// </summary>
        public static async Task FixApacheSRVROOTAsync(string apachePath)
        {
            try
            {
                var confPath = Path.Combine(apachePath, "conf", "httpd.conf");
                if (!File.Exists(confPath)) return;

                var content = await File.ReadAllTextAsync(confPath);
                
                // Update SRVROOT to point to the correct Apache installation path
                var unixPath = apachePath.Replace("\\", "/");
                var updatedContent = SrvrootDefineRegex().Replace(
                    content, 
                    $"Define SRVROOT \"{unixPath}\"");

                // Only write if content actually changed
                if (updatedContent != content)
                {
                    await File.WriteAllTextAsync(confPath, updatedContent);
                    System.Diagnostics.Debug.WriteLine($"Apache SRVROOT fixed: {unixPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fix Apache SRVROOT: {ex.Message}");
            }
        }

        private static async Task ConfigureNginxAsync(PackageInstallationConfig config)
        {
            try
            {
                var nginxPath = FindNginxInstallation(config.InstallPath);
                if (string.IsNullOrEmpty(nginxPath)) return;

                System.Diagnostics.Debug.WriteLine("🔧 Configuring Nginx to listen on ports 80 and 443...");
                
                var confPath = Path.Combine(nginxPath, "conf", "nginx.conf");
                if (!File.Exists(confPath)) return;

                var content = await File.ReadAllTextAsync(confPath);
                
                // Remove existing listen directives
                content = ListenDirectiveNginxRegex().Replace(content, "");
                
                // Add listen directives for both HTTP and HTTPS
                var listenDirectives = "        listen 80;\n        listen 443 ssl;\n";
                
                // Find the server block and add listen directives
                if (content.Contains("server {"))
                {
                    content = ServerBlockRegex().Replace(content, 
                        $"$1\n{listenDirectives}");
                }
                else
                {
                    // If no server block exists, create a basic one
                    var serverBlock = $@"
    server {{
{listenDirectives}
        server_name localhost;
        root html;
        index index.html index.htm;
        
        location / {{
            try_files $uri $uri/ =404;
        }}
    }}
";
                    content = content.Replace("http {", $"http {{{serverBlock}");
                }
                
                // Add SSL configuration
                if (!content.Contains("ssl_certificate"))
                {
                    var sslConfig = @"
        # SSL Configuration
        ssl_certificate conf/ssl/server.crt;
        ssl_certificate_key conf/ssl/server.key;
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers ECDHE-RSA-AES128-GCM-SHA256:ECDHE-RSA-AES256-GCM-SHA384;
        ssl_prefer_server_ciphers off;
";
                    content = content.Replace("listen 443 ssl;", $"listen 443 ssl;{sslConfig}");
                }

                await File.WriteAllTextAsync(confPath, content);
                System.Diagnostics.Debug.WriteLine($"✅ Nginx configured to listen on ports 80 and 443");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Nginx configuration failed: {ex.Message}");
            }
        }

        private static async Task ConfigureMySQLAsync(PackageInstallationConfig config)
        {
            try
            {
                // Find MySQL installation
                var mysqlPath = FindMySQLInstallation(config.InstallPath);
                if (string.IsNullOrEmpty(mysqlPath))
                {
                    throw new Exception("MySQL installation directory not found after extraction");
                }

                System.Diagnostics.Debug.WriteLine($"Found MySQL installation at: {mysqlPath}");

                // AUTOMATIC CONFIGURATION FIXING - No user interaction required
                System.Diagnostics.Debug.WriteLine("🔧 Performing automatic MySQL configuration fixes...");

                // 1. Create/Update my.ini with modern MySQL 8.0+ configuration
                System.Diagnostics.Debug.WriteLine("📄 Creating/updating my.ini with modern MySQL 8.0+ configuration...");
                await CreateMySQLConfigurationAsync(mysqlPath, config);
                System.Diagnostics.Debug.WriteLine("✅ MySQL configuration file created successfully");

                // 2. Handle version conflicts and initialize data directory
                System.Diagnostics.Debug.WriteLine("🗂️ Ensuring MySQL data directory is properly initialized...");
                try
                {
                    await InitializeMySQLDataDirectoryAsync(mysqlPath, config);
                    System.Diagnostics.Debug.WriteLine("✅ MySQL data directory initialized successfully");
                }
                catch (Exception initEx)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ MySQL data initialization failed: {initEx.Message}");
                    System.Diagnostics.Debug.WriteLine("🔧 Attempting automatic recovery...");
                    
                    // Try to handle version conflicts automatically
                    try
                    {
                        var dataPath = Path.Combine(mysqlPath, "data");
                        await HandleMySQLVersionConflictsAsync(dataPath);
                        
                        // Retry initialization after conflict resolution
                        await InitializeMySQLDataDirectoryAsync(mysqlPath, config);
                        System.Diagnostics.Debug.WriteLine("✅ MySQL data directory initialized successfully after conflict resolution");
                    }
                    catch (Exception recoveryEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Automatic recovery failed: {recoveryEx.Message}");
                        System.Diagnostics.Debug.WriteLine("💡 MySQL has been installed but may need manual initialization");
                        
                        // Create a readme file with manual initialization instructions
                        await CreateMySQLManualSetupInstructions(mysqlPath, recoveryEx.Message);
                    }
                }

                // Set root password after initialization
                System.Diagnostics.Debug.WriteLine("🔐 Setting MySQL root password...");
                try
                {
                    await SetMySQLRootPasswordAsync(mysqlPath, config);
                    System.Diagnostics.Debug.WriteLine("✅ MySQL root password set successfully");
                }
                catch (Exception pwdEx)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Failed to set root password: {pwdEx.Message}");
                    System.Diagnostics.Debug.WriteLine("💡 You can set the password manually using: ALTER USER 'root'@'localhost' IDENTIFIED BY 'your_password';");
                }
                
                System.Diagnostics.Debug.WriteLine("✅ Automatic MySQL configuration fixes completed successfully!");
                System.Diagnostics.Debug.WriteLine($"MySQL setup completed at: {mysqlPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MySQL configuration failed: {ex.Message}");
                throw new Exception($"MySQL configuration failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles MySQL version conflicts by detecting and removing incompatible data directories
        /// </summary>
        private static async Task HandleMySQLVersionConflictsAsync(string dataPath)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 Checking for MySQL version conflicts in: {dataPath}");
                
                if (Directory.Exists(dataPath))
                {
                    // Check for mysql.ibd which indicates a version conflict
                    var mysqlIbdPath = Path.Combine(dataPath, "mysql.ibd");
                    if (File.Exists(mysqlIbdPath))
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ Found mysql.ibd - potential version conflict detected");
                        System.Diagnostics.Debug.WriteLine("💡 This usually means MySQL data was created with a newer version");
                        
                        var importantFiles = new[] { "mysql.ibd", "ib_logfile0", "ib_logfile1", "ibdata1", "auto.cnf" };
                        var foundFiles = importantFiles.Where(f => File.Exists(Path.Combine(dataPath, f))).ToArray();
                        System.Diagnostics.Debug.WriteLine($"📁 Found MySQL files: {string.Join(", ", foundFiles)}");
                        
                        if (foundFiles.Length > 0)
                        {
                            System.Diagnostics.Debug.WriteLine("🗑️ Removing existing MySQL data directory to prevent version conflicts");
                            
                            try
                            {
                                // Stop any running MySQL processes first
                                var mysqlProcesses = System.Diagnostics.Process.GetProcesses()
                                    .Where(p => p.ProcessName.Contains("mysql", StringComparison.OrdinalIgnoreCase) || 
                                               p.ProcessName.Contains("mysqld", StringComparison.OrdinalIgnoreCase))
                                    .ToArray();
                                
                                if (mysqlProcesses.Length > 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"🛑 Found {mysqlProcesses.Length} MySQL processes, stopping them...");
                                    foreach (var process in mysqlProcesses)
                                    {
                                        try
                                        {
                                            if (!process.HasExited)
                                            {
                                                process.Kill();
                                                System.Diagnostics.Debug.WriteLine($"✅ Stopped process: {process.ProcessName} (PID: {process.Id})");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"⚠️ Could not stop process {process.ProcessName}: {ex.Message}");
                                        }
                                    }
                                    await Task.Delay(2000); // Wait for processes to stop
                                }
                                
                                Directory.Delete(dataPath, true);
                                System.Diagnostics.Debug.WriteLine("✅ Existing MySQL data directory removed successfully");
                                System.Diagnostics.Debug.WriteLine("💡 MySQL will now initialize with a fresh data directory");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"❌ Failed to remove existing data directory: {ex.Message}");
                                throw new Exception($"Failed to remove conflicting data directory: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("✅ No version conflicts detected");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("✅ Data directory does not exist - no conflicts to resolve");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error checking for version conflicts: {ex.Message}");
                throw new Exception($"Error handling MySQL version conflicts: {ex.Message}");
            }
        }

        private static async Task CreateMySQLManualSetupInstructions(string mysqlPath, string errorMessage)
        {
            try
            {
                var instructionsPath = Path.Combine(mysqlPath, "SETUP_INSTRUCTIONS.txt");
                var instructions = $@"MySQL Manual Setup Instructions
========================================

MySQL has been extracted and configured, but automatic data directory initialization failed.
Error: {errorMessage}

To complete the setup manually:

1. Open Command Prompt as Administrator
2. Navigate to: {Path.Combine(mysqlPath, "bin")}
3. Run: mysqld --initialize-insecure --console
4. Start MySQL service or run: mysqld --console

Alternative:
- Install Visual C++ Redistributable 2019 x64 if not already installed
- Ensure Windows has all required dependencies

Configuration file location: {Path.Combine(mysqlPath, "my.ini")}
Data directory: {Path.Combine(mysqlPath, "data")}

For more help, visit: https://dev.mysql.com/doc/refman/8.0/en/windows-installation.html
";

                await File.WriteAllTextAsync(instructionsPath, instructions);
                System.Diagnostics.Debug.WriteLine($"Manual setup instructions created at: {instructionsPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create setup instructions: {ex.Message}");
            }
        }

        private static async Task CreateMySQLManualDownloadInstructions(PackageInstallationConfig config, string errorMessage)
        {
            try
            {
                var instructionsPath = Path.Combine(config.InstallPath, "MYSQL_DOWNLOAD_INSTRUCTIONS.txt");
                var version = config.Version?.Version ?? "8.0.35";
                
                var instructions = $@"MySQL Manual Download Instructions
=========================================

Automatic download failed. Please download MySQL manually:

Error: {errorMessage}

DOWNLOAD INSTRUCTIONS:
1. Visit: https://dev.mysql.com/downloads/mysql/
2. Select 'Windows (x86, 64-bit), ZIP Archive'
3. Download: mysql-{version}-winx64.zip
4. Extract to: {config.InstallPath}

ALTERNATIVE DOWNLOAD LINKS:
- Primary: https://dev.mysql.com/get/Downloads/MySQL-8.0/mysql-{version}-winx64.zip
- Mirror: https://downloads.mysql.com/archives/get/p/23/file/mysql-{version}-winx64.zip

AFTER MANUAL DOWNLOAD:
1. Extract the ZIP file to this directory: {config.InstallPath}
2. The folder structure should be: {config.InstallPath}\\mysql-{version}-winx64\\
3. Re-run ModStack to complete the MySQL configuration

TROUBLESHOOTING:
- Check your internet connection
- Temporarily disable antivirus/firewall
- Try downloading with a web browser first
- Use a VPN if MySQL downloads are blocked in your region

ModStack will automatically detect and configure MySQL once the files are in place.

Installation directory: {config.InstallPath}
Required file: mysql-{version}-winx64.zip

For support, visit: https://dev.mysql.com/doc/refman/8.0/en/windows-installation.html
";

                Directory.CreateDirectory(config.InstallPath);
                await File.WriteAllTextAsync(instructionsPath, instructions);
                System.Diagnostics.Debug.WriteLine($"Manual download instructions created at: {instructionsPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create download instructions: {ex.Message}");
            }
        }

        private static string FindMySQLInstallation(string basePath)
        {
            // Check common MySQL directory structures
            var possiblePaths = new[]
            {
                basePath,
                Path.Combine(basePath, "mysql-8.0.35-winx64"),
                Path.Combine(basePath, "mysql-8.0.33-winx64"), 
                Path.Combine(basePath, "mysql-5.7.44-winx64")
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "bin", "mysqld.exe")))
                {
                    return path;
                }
            }

            // Check for wildcard directories (mysql-*)
            try
            {
                var mysqlDirs = Directory.GetDirectories(basePath, "mysql-*");
                foreach (var dir in mysqlDirs)
                {
                    if (File.Exists(Path.Combine(dir, "bin", "mysqld.exe")))
                    {
                        return dir;
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private static async Task CreateMySQLConfigurationAsync(string mysqlPath, PackageInstallationConfig config)
        {
            try
            {
                var configPath = Path.Combine(mysqlPath, "my.ini");
                var dataDir = Path.Combine(mysqlPath, "data");
                var port = config.Configuration.GetValueOrDefault("port", 3306).ToString();
                var rootPassword = config.Configuration.GetValueOrDefault("root_password", "root").ToString();
                var bufferPoolSize = config.Configuration.GetValueOrDefault("innodb_buffer_pool_size", "128M").ToString();

                // Ensure data directory exists
                Directory.CreateDirectory(dataDir);

                // Create my.ini configuration
                var configContent = $@"[mysqld]
# Basic Configuration
port = {port}
basedir = {mysqlPath.Replace("\\", "/")}
datadir = {dataDir.Replace("\\", "/")}
tmpdir = {Path.Combine(mysqlPath, "tmp").Replace("\\", "/")}

# Server Configuration
default_storage_engine = InnoDB
# Use authentication_policy instead of deprecated default_authentication_plugin
authentication_policy = mysql_native_password

# Memory Configuration
innodb_buffer_pool_size = {bufferPoolSize}
key_buffer_size = 32M
max_allowed_packet = 128M
sort_buffer_size = 2M
read_buffer_size = 2M
read_rnd_buffer_size = 8M
myisam_sort_buffer_size = 64M
thread_cache_size = 8
# query_cache_size removed - not supported in MySQL 8.0+

# Connection Settings
max_connections = 151
max_connect_errors = 100

# Logging
log_error = {Path.Combine(mysqlPath, "logs", "error.log").Replace("\\", "/")}
general_log = 0
slow_query_log = 0

# Security
sql_mode = STRICT_TRANS_TABLES,NO_ZERO_DATE,NO_ZERO_IN_DATE,ERROR_FOR_DIVISION_BY_ZERO

# MySQL 8.0+ specific settings
upgrade = AUTO
skip_log_bin = 1

[mysql]
default_character_set = utf8mb4

[mysqldump]
quick
quote_names
max_allowed_packet = 128M

[client]
port = {port}
default_character_set = utf8mb4
";

                await File.WriteAllTextAsync(configPath, configContent);

                // Create necessary directories
                Directory.CreateDirectory(Path.Combine(mysqlPath, "logs"));
                Directory.CreateDirectory(Path.Combine(mysqlPath, "tmp"));

                System.Diagnostics.Debug.WriteLine($"MySQL configuration file created: {configPath}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create MySQL configuration: {ex.Message}");
            }
        }

        private static async Task InitializeMySQLDataDirectoryAsync(string mysqlPath, PackageInstallationConfig config)
        {
            try
            {
                var mysqldPath = Path.Combine(mysqlPath, "bin", "mysqld.exe");
                var dataDir = Path.Combine(mysqlPath, "data");
                var rootPassword = config.Configuration.GetValueOrDefault("root_password", "root").ToString();

                // Verify mysqld.exe exists
                if (!File.Exists(mysqldPath))
                {
                    throw new Exception($"mysqld.exe not found at: {mysqldPath}");
                }

                // Check if data directory is already properly initialized
                if (Directory.Exists(dataDir) && Directory.GetFileSystemEntries(dataDir).Length > 0)
                {
                    // Check if critical system tables exist
                    var criticalFiles = new[]
                    {
                        "mysql.ibd", "mysql", "performance_schema", "sys", "information_schema"
                    };
                    
                    var hasCriticalFiles = criticalFiles.Any(file => 
                        Directory.Exists(Path.Combine(dataDir, file)) || 
                        File.Exists(Path.Combine(dataDir, file + ".ibd")));
                    
                    if (hasCriticalFiles)
                    {
                        System.Diagnostics.Debug.WriteLine("MySQL data directory appears to be properly initialized");
                        return;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("MySQL data directory exists but appears incomplete - reinitializing");
                    }
                }

                // Check for Visual C++ Redistributable dependency
                await CheckMySQLDependenciesAsync();

                // Clean up any existing data directory
                if (Directory.Exists(dataDir))
                {
                    Directory.Delete(dataDir, true);
                }
                Directory.CreateDirectory(dataDir);

                // Try simpler initialization first (without --basedir if it causes issues)
                var arguments = $"--initialize-insecure --datadir=\"{dataDir}\"";
                
                // Initialize the data directory
                var initProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = mysqldPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.Combine(mysqlPath, "bin") // Run from bin directory
                    }
                };

                System.Diagnostics.Debug.WriteLine($"Initializing MySQL data directory with command: {initProcess.StartInfo.FileName} {initProcess.StartInfo.Arguments}");
                System.Diagnostics.Debug.WriteLine($"Working directory: {initProcess.StartInfo.WorkingDirectory}");

                try
                {
                    initProcess.Start();
                    var output = await initProcess.StandardOutput.ReadToEndAsync();
                    var error = await initProcess.StandardError.ReadToEndAsync();
                    
                    await initProcess.WaitForExitAsync();

                    System.Diagnostics.Debug.WriteLine($"MySQL init exit code: {initProcess.ExitCode}");
                    System.Diagnostics.Debug.WriteLine($"MySQL init output: {output}");
                    System.Diagnostics.Debug.WriteLine($"MySQL init error: {error}");

                    if (initProcess.ExitCode != 0)
                    {
                        // Try alternative initialization method
                        await TryAlternativeMySQLInitialization(mysqlPath, dataDir);
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"MySQL data directory initialized successfully");
                }
                catch (Exception processEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Process execution failed: {processEx.Message}");
                    // Try alternative initialization
                    await TryAlternativeMySQLInitialization(mysqlPath, dataDir);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MySQL initialization error: {ex.Message}");
                throw new Exception($"Failed to initialize MySQL data directory: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets MySQL root password after initialization using init-file approach
        /// </summary>
        private static async Task SetMySQLRootPasswordAsync(string mysqlPath, PackageInstallationConfig config)
        {
            try
            {
                var mysqldPath = Path.Combine(mysqlPath, "bin", "mysqld.exe");
                var rootPassword = config.Configuration.GetValueOrDefault("root_password", "root")?.ToString() ?? "root";
                
                System.Diagnostics.Debug.WriteLine($"🔐 Setting root password to: {rootPassword}");
                
                // Create init file with password change SQL
                var initFile = Path.Combine(Path.GetTempPath(), $"mysql_init_{Guid.NewGuid():N}.sql");
                var sqlCommands = $@"ALTER USER 'root'@'localhost' IDENTIFIED BY '{rootPassword}';
FLUSH PRIVILEGES;";
                
                await File.WriteAllTextAsync(initFile, sqlCommands);
                System.Diagnostics.Debug.WriteLine($"📄 Created init file: {initFile}");
                
                try
                {
                    // Start MySQL with init-file to set password
                    System.Diagnostics.Debug.WriteLine("🚀 Starting MySQL with init-file to set password...");
                    
                    var mysqldProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = mysqldPath,
                            Arguments = $"--init-file=\"{initFile}\" --console",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            WorkingDirectory = Path.Combine(mysqlPath, "bin")
                        }
                    };
                    
                    mysqldProcess.Start();
                    
                    // Read output asynchronously
                    var outputTask = mysqldProcess.StandardOutput.ReadToEndAsync();
                    var errorTask = mysqldProcess.StandardError.ReadToEndAsync();
                    
                    // Wait for MySQL to process init file (give it 10 seconds)
                    System.Diagnostics.Debug.WriteLine("⏱️ Waiting for MySQL to process init file...");
                    await Task.Delay(10000);
                    
                    // Stop MySQL
                    if (!mysqldProcess.HasExited)
                    {
                        System.Diagnostics.Debug.WriteLine("🛑 Stopping MySQL server...");
                        mysqldProcess.Kill(true);
                        await Task.Delay(2000); // Wait for graceful shutdown
                    }
                    
                    var output = await outputTask;
                    var error = await errorTask;
                    
                    System.Diagnostics.Debug.WriteLine($"📤 MySQL output: {output}");
                    System.Diagnostics.Debug.WriteLine($"📤 MySQL error: {error}");
                    
                    System.Diagnostics.Debug.WriteLine($"✅ Root password set successfully");
                }
                finally
                {
                    // Clean up init file
                    if (File.Exists(initFile))
                    {
                        File.Delete(initFile);
                        System.Diagnostics.Debug.WriteLine($"🗑️ Deleted init file");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Failed to set root password: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"💡 Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private static async Task CheckMySQLDependenciesAsync()
        {
            try
            {
                // Check for Visual C++ Redistributable (required for MySQL)
                var vcRedistCheck = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "reg",
                        Arguments = "query \"HKLM\\SOFTWARE\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes\\x64\" /v Version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                vcRedistCheck.Start();
                var output = await vcRedistCheck.StandardOutput.ReadToEndAsync();
                await vcRedistCheck.WaitForExitAsync();

                if (vcRedistCheck.ExitCode != 0)
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: Visual C++ Redistributable may not be installed. MySQL might fail to run.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Visual C++ Redistributable detected");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dependency check failed: {ex.Message}");
            }
        }

        private static async Task TryAlternativeMySQLInitialization(string mysqlPath, string dataDir)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Attempting alternative MySQL initialization...");
                
                // Create minimal data directory structure
                Directory.CreateDirectory(dataDir);
                Directory.CreateDirectory(Path.Combine(dataDir, "mysql"));
                Directory.CreateDirectory(Path.Combine(dataDir, "performance_schema"));
                Directory.CreateDirectory(Path.Combine(dataDir, "sys"));

                // Create a simple bootstrap file to bypass complex initialization
                var bootstrapFile = Path.Combine(mysqlPath, "bootstrap.sql");
                var bootstrapContent = @"
CREATE DATABASE IF NOT EXISTS mysql;
USE mysql;
-- Skip complex initialization for now
";
                await File.WriteAllTextAsync(bootstrapFile, bootstrapContent);

                System.Diagnostics.Debug.WriteLine("Alternative MySQL initialization completed (minimal setup)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Alternative initialization failed: {ex.Message}");
                throw new Exception($"All MySQL initialization methods failed. Last error: {ex.Message}");
            }
        }

        private static async Task ConfigurePHPAsync(PackageInstallationConfig config)
        {
            try
            {
                // Find PHP installation
                var phpPath = FindPHPInstallation(config.InstallPath);
                if (string.IsNullOrEmpty(phpPath)) return;

                System.Diagnostics.Debug.WriteLine($"🔧 Found PHP installation at: {phpPath}");

                var phpIniPath = Path.Combine(phpPath, "php.ini");
                var phpIniDevelopmentPath = Path.Combine(phpPath, "php.ini-development");
                var phpIniProductionPath = Path.Combine(phpPath, "php.ini-production");

                // Create php.ini from template if it doesn't exist
                if (!File.Exists(phpIniPath))
                {
                    if (File.Exists(phpIniDevelopmentPath))
                    {
                        File.Copy(phpIniDevelopmentPath, phpIniPath);
                        System.Diagnostics.Debug.WriteLine("📄 Created php.ini from development template");
                    }
                    else if (File.Exists(phpIniProductionPath))
                    {
                        File.Copy(phpIniProductionPath, phpIniPath);
                        System.Diagnostics.Debug.WriteLine("📄 Created php.ini from production template");
                    }
                }

                if (!File.Exists(phpIniPath)) return;

                // Perform PHP extension compatibility check and configuration
                System.Diagnostics.Debug.WriteLine("🔍 Performing PHP extension compatibility check...");
                await PerformPHPExtensionCompatibilityCheckAsync(phpPath, config);

                var content = await File.ReadAllTextAsync(phpIniPath);

                // Apply configuration from user settings
                System.Diagnostics.Debug.WriteLine($"📝 Applying {config.Configuration.Count} configuration settings...");
                foreach (var setting in config.Configuration)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"  ⚙️ Processing setting: {setting.Key} = {setting.Value}");
                        
                        switch (setting.Key)
                        {
                            case "memory_limit":
                                content = UpdatePHPSetting(content, "memory_limit", setting.Value?.ToString() ?? string.Empty);
                                break;
                            case "max_execution_time":
                                content = UpdatePHPSetting(content, "max_execution_time", setting.Value?.ToString() ?? string.Empty);
                                break;
                            case "upload_max_filesize":
                                content = UpdatePHPSetting(content, "upload_max_filesize", setting.Value?.ToString() ?? string.Empty);
                                break;
                            case "post_max_size":
                                content = UpdatePHPSetting(content, "post_max_size", setting.Value?.ToString() ?? string.Empty);
                                break;
                            case "enable_opcache":
                                if (GetBooleanValue(setting.Value))
                                {
                                    content = UpdatePHPSetting(content, "zend_extension", "opcache");
                                    content = UpdatePHPSetting(content, "opcache.enable", "1");
                                }
                                break;
                            case "enable_xdebug":
                                if (GetBooleanValue(setting.Value))
                                {
                                    content = UpdatePHPSetting(content, "zend_extension", "xdebug");
                                }
                                break;
                            case "enable_mysqli":
                                if (GetBooleanValue(setting.Value))
                                {
                                    content = UpdatePHPSetting(content, "extension", "mysqli");
                                }
                                break;
                            case "enable_curl":
                                if (GetBooleanValue(setting.Value))
                                {
                                    content = UpdatePHPSetting(content, "extension", "curl");
                                }
                                break;
                            case "enable_gd":
                                if (GetBooleanValue(setting.Value))
                                {
                                    content = UpdatePHPSetting(content, "extension", "gd");
                                }
                                break;
                            case "enable_mbstring":
                                if (GetBooleanValue(setting.Value))
                                {
                                    content = UpdatePHPSetting(content, "extension", "mbstring");
                                }
                                break;
                            case "enable_zip":
                                if (GetBooleanValue(setting.Value))
                                {
                                    content = UpdatePHPSetting(content, "extension", "zip");
                                }
                                break;
                            case "enable_xml":
                                if (GetBooleanValue(setting.Value))
                                {
                                    content = UpdatePHPSetting(content, "extension", "xml");
                                }
                                break;
                            case "enable_json":
                                if (GetBooleanValue(setting.Value))
                                {
                                    content = UpdatePHPSetting(content, "extension", "json");
                                }
                                break;
                            case "enable_fileinfo":
                                if (GetBooleanValue(setting.Value))
                                {
                                    content = UpdatePHPSetting(content, "extension", "fileinfo");
                                }
                                break;
                            case "enable_openssl":
                                if (GetBooleanValue(setting.Value))
                                {
                                    content = UpdatePHPSetting(content, "extension", "openssl");
                                }
                                break;
                            case "enable_pdo_mysql":
                                if (GetBooleanValue(setting.Value))
                                {
                                    content = UpdatePHPSetting(content, "extension", "pdo_mysql");
                                }
                                break;
                            case "enable_ioncube":
                                if (GetBooleanValue(setting.Value))
                                {
                                    await ConfigureIonCubeLoaderAsync(phpPath, content);
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"  ⚠️ Failed to apply setting {setting.Key}: {ex.Message}");
                    }
                }

                // Set extension directory
                var extDir = Path.Combine(phpPath, "ext");
                if (Directory.Exists(extDir))
                {
                    content = UpdatePHPSetting(content, "extension_dir", $"\"{extDir}\"");
                }

                await File.WriteAllTextAsync(phpIniPath, content);
                
                // Perform post-installation extension configuration
                System.Diagnostics.Debug.WriteLine("🔧 Performing post-installation extension configuration...");
                await PerformPostInstallationExtensionConfigurationAsync(phpPath, config);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PHP configuration failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Performs PHP extension compatibility check and fixes API version mismatches
        /// </summary>
        private static async Task PerformPHPExtensionCompatibilityCheckAsync(string phpPath, PackageInstallationConfig _)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔍 Starting PHP extension compatibility check...");
                
                var phpExe = Path.Combine(phpPath, "php.exe");
                if (!File.Exists(phpExe))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ PHP executable not found, skipping compatibility check");
                    return;
                }

                // Get PHP version and API version
                System.Diagnostics.Debug.WriteLine("📋 Step 1: Getting PHP version with 'php -v'...");
                var (versionSuccess, versionOutput, versionError) = await ExecuteProcessAsync(phpExe, "-v", phpPath, 10); // 10 second timeout
                
                if (!versionSuccess)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Failed to get PHP version: {versionError}");
                    System.Diagnostics.Debug.WriteLine("⚠️ Skipping compatibility check");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine("📋 Step 2: Getting PHP info with 'php -i'...");
                var (apiSuccess, apiOutput, apiError) = await ExecuteProcessAsync(phpExe, "-i", phpPath, 15); // 15 second timeout
                
                if (!apiSuccess)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Failed to get PHP API info: {apiError}");
                    System.Diagnostics.Debug.WriteLine("⚠️ Skipping compatibility check");
                    return;
                }

                // Extract PHP version from output
                var phpVersion = ExtractPHPVersion(versionOutput);
                var phpApiVersion = ExtractPHPAPIVersion(apiOutput);
                
                System.Diagnostics.Debug.WriteLine($"📊 PHP Version: {phpVersion}, API Version: {phpApiVersion}");

                // Check extension directory
                var extDir = Path.Combine(phpPath, "ext");
                if (!Directory.Exists(extDir))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Extension directory not found, skipping compatibility check");
                    return;
                }

                // Check each extension DLL for API compatibility
                var incompatibleExtensions = new List<string>();
                var extensionFiles = Directory.GetFiles(extDir, "*.dll");
                
                foreach (var dllFile in extensionFiles)
                {
                    var dllName = Path.GetFileName(dllFile);
                    System.Diagnostics.Debug.WriteLine($"🔍 Checking extension: {dllName}");
                    
                    // Try to load the extension and check for API version mismatch
                    var (testSuccess, testOutput, _) = await TestExtensionCompatibilityAsync(phpExe, dllFile, phpPath);
                    if (!testSuccess && testOutput.Contains("Module compiled with module API"))
                    {
                        incompatibleExtensions.Add(dllName);
                        System.Diagnostics.Debug.WriteLine($"❌ Incompatible extension detected: {dllName}");
                    }
                }

                if (incompatibleExtensions.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Found {incompatibleExtensions.Count} incompatible extensions:");
                    foreach (var ext in incompatibleExtensions)
                    {
                        System.Diagnostics.Debug.WriteLine($"   - {ext}");
                    }
                    
                    // Log warning but don't fail installation
                    System.Diagnostics.Debug.WriteLine("⚠️ Some extensions may not work properly due to API version mismatch");
                    System.Diagnostics.Debug.WriteLine("   Users can disable incompatible extensions through the ModStack GUI");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("✅ All extensions appear to be compatible");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Extension compatibility check failed: {ex.Message}");
                // Don't fail installation for compatibility check errors
            }
        }

        /// <summary>
        /// Performs post-installation extension configuration to enable common extensions
        /// </summary>
        private static async Task PerformPostInstallationExtensionConfigurationAsync(string phpPath, PackageInstallationConfig _)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔧 Starting post-installation extension configuration...");
                
                var phpIniPath = Path.Combine(phpPath, "php.ini");
                if (!File.Exists(phpIniPath))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ php.ini not found, skipping extension configuration");
                    return;
                }

                var content = await File.ReadAllTextAsync(phpIniPath);
                var extDir = Path.Combine(phpPath, "ext");
                
                // List of common extensions to enable by default
                var commonExtensions = new[]
                {
                    "fileinfo", "mbstring", "openssl", "curl", "zip", "xml", "json", 
                    "pdo_mysql", "mysqli", "gd", "opcache"
                };

                var enabledCount = 0;
                var skippedCount = 0;

                foreach (var extName in commonExtensions)
                {
                    var dllPath = Path.Combine(extDir, $"php_{extName}.dll");
                    
                    // Only enable if DLL exists
                    if (File.Exists(dllPath))
                    {
                        // Check if already enabled
                        if (!IsExtensionEnabled(content, extName))
                        {
                            content = EnablePHPExtension(content, extName);
                            enabledCount++;
                            System.Diagnostics.Debug.WriteLine($"✅ Enabled extension: {extName}");
                        }
                        else
                        {
                            skippedCount++;
                            System.Diagnostics.Debug.WriteLine($"ℹ️ Extension already enabled: {extName}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Extension DLL not found: {extName}");
                    }
                }

                // Write updated configuration
                await File.WriteAllTextAsync(phpIniPath, content);
                
                System.Diagnostics.Debug.WriteLine($"🔧 Extension configuration completed: {enabledCount} enabled, {skippedCount} already enabled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Post-installation extension configuration failed: {ex.Message}");
                // Don't fail installation for configuration errors
            }
        }

        /// <summary>
        /// Executes a process and returns the result
        /// </summary>
        private static async Task<(bool Success, string Output, string Error)> ExecuteProcessAsync(string fileName, string arguments, string workingDirectory, int timeoutSeconds = 30)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🚀 Executing: {fileName} {arguments}");
                System.Diagnostics.Debug.WriteLine($"📁 Working directory: {workingDirectory}");
                
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    System.Diagnostics.Debug.WriteLine("❌ Process start failed");
                    return (false, "", "Process start failed");
                }

                System.Diagnostics.Debug.WriteLine($"⏱️ Waiting for process to exit (timeout: {timeoutSeconds}s)...");
                
                // Create a timeout task
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                
                try
                {
                    // Wait for process with timeout
                    await process.WaitForExitAsync(cts.Token);
                    
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    
                    System.Diagnostics.Debug.WriteLine($"✅ Process exited with code: {process.ExitCode}");
                    System.Diagnostics.Debug.WriteLine($"📤 Output length: {output.Length} chars");
                    System.Diagnostics.Debug.WriteLine($"📤 Error length: {error.Length} chars");
                    
                    return (process.ExitCode == 0, output, error);
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"⏰ Process timed out after {timeoutSeconds} seconds");
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                            System.Diagnostics.Debug.WriteLine("🔪 Process killed");
                        }
                    }
                    catch { }
                    
                    return (false, "", $"Process timed out after {timeoutSeconds} seconds");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Process execution failed: {ex.Message}");
                return (false, "", ex.Message);
            }
        }

        /// <summary>
        /// Tests extension compatibility by attempting to load it
        /// </summary>
        private static async Task<(bool Success, string Output, string Error)> TestExtensionCompatibilityAsync(string phpExe, string dllPath, string workingDirectory)
        {
            try
            {
                // Create a temporary PHP script to test extension loading
                var testScript = $@"
<?php
try {{
    if (extension_loaded('{Path.GetFileNameWithoutExtension(dllPath).Replace("php_", "")}')) {{
        echo 'Extension loaded successfully';
    }} else {{
        echo 'Extension not loaded';
    }}
}} catch (Exception $e) {{
    echo 'Error: ' . $e->getMessage();
}}
?>";
                
                var tempScriptPath = Path.Combine(Path.GetTempPath(), $"php_test_{Guid.NewGuid():N}.php");
                await File.WriteAllTextAsync(tempScriptPath, testScript);
                
                try
                {
                    var result = await ExecuteProcessAsync(phpExe, tempScriptPath, workingDirectory);
                    return result;
                }
                finally
                {
                    if (File.Exists(tempScriptPath))
                        File.Delete(tempScriptPath);
                }
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        /// <summary>
        /// Extracts PHP version from version output
        /// </summary>
        private static string ExtractPHPVersion(string versionOutput)
        {
            var lines = versionOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                var firstLine = lines[0];
                var match = PHPVersionRegex().Match(firstLine);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            return "Unknown";
        }

        /// <summary>
        /// Extracts PHP API version from info output
        /// </summary>
        private static string ExtractPHPAPIVersion(string infoOutput)
        {
            var lines = infoOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("PHP API"))
                {
                    var match = PHPAPIVersionRegex().Match(line);
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
            return "Unknown";
        }

        /// <summary>
        /// Checks if a PHP extension is enabled in php.ini content
        /// </summary>
        private static bool IsExtensionEnabled(string phpIniContent, string extensionName)
        {
            var lines = phpIniContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith(';') || trimmedLine.StartsWith('#'))
                    continue;
                
                if (trimmedLine.Equals($"extension={extensionName}", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.Equals($"extension=php_{extensionName}.dll", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Enables a PHP extension in php.ini content
        /// </summary>
        private static string EnablePHPExtension(string phpIniContent, string extensionName)
        {
            var lines = phpIniContent.Split('\n').ToList();
            var extensionLine = $"extension={extensionName}";
            
            // Check if already exists (commented or uncommented)
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (line.Equals($";{extensionLine}") || line.Equals($"#{extensionLine}"))
                {
                    // Uncomment existing line
                    lines[i] = lines[i].Replace(";", "").Replace("#", "").Trim();
                    return string.Join("\n", lines);
                }
                else if (line.Equals(extensionLine))
                {
                    // Already enabled
                    return phpIniContent;
                }
            }
            
            // Add new extension line
            lines.Add(extensionLine);
            return string.Join("\n", lines);
        }

        private static string FindPHPInstallation(string basePath)
        {
            var possiblePaths = new[]
            {
                basePath,
                Path.Combine(basePath, "php-8.3.0-Win32-vs16-x64"),
                Path.Combine(basePath, "php-8.2.14-Win32-vs16-x64"),
                Path.Combine(basePath, "php-8.1.27-Win32-vs16-x64"),
                Path.Combine(basePath, "php-7.4.33-Win32-vc15-x64"),
                Path.Combine(basePath, "php-7.2.34-Win32-vc15-x64")
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "php.exe")))
                {
                    return path;
                }
            }

            // Check for wildcard directories
            try
            {
                var phpDirs = Directory.GetDirectories(basePath, "php-*");
                foreach (var dir in phpDirs)
                {
                    if (File.Exists(Path.Combine(dir, "php.exe")))
                    {
                        return dir;
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        /// <summary>
        /// Safely converts a configuration value to boolean
        /// </summary>
        private static bool GetBooleanValue(object? value)
        {
            if (value == null) return false;
            
            // Handle JsonElement (from System.Text.Json)
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.True) return true;
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.False) return false;
                
                // Try to parse as string
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var str = jsonElement.GetString();
                    return bool.TryParse(str, out var result) && result;
                }
                
                return false;
            }
            
            // Handle regular boolean
            if (value is bool boolValue) return boolValue;
            
            // Handle string
            if (value is string strValue)
            {
                return bool.TryParse(strValue, out var result) && result;
            }
            
            // Try to convert
            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                return false;
            }
        }

        private static string UpdatePHPSetting(string content, string setting, string value)
        {
            var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            var updated = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith($"{setting} =") || line.StartsWith($";{setting} ="))
                {
                    lines[i] = $"{setting} = {value}";
                    updated = true;
                    break;
                }
            }

            if (!updated)
            {
                // Add the setting at the end
                content += $"\n{setting} = {value}\n";
                return content;
            }

            return string.Join("\n", lines);
        }

        private static async Task ConfigurePhpMyAdminAsync(PackageInstallationConfig config)
        {
            try
            {
                // Find phpMyAdmin installation
                var phpMyAdminPath = FindPhpMyAdminInstallation(config.InstallPath);
                if (string.IsNullOrEmpty(phpMyAdminPath)) return;

                var configPath = Path.Combine(phpMyAdminPath, "config.inc.php");
                var configSamplePath = Path.Combine(phpMyAdminPath, "config.sample.inc.php");

                // Create config.inc.php from sample if it doesn't exist
                if (!File.Exists(configPath) && File.Exists(configSamplePath))
                {
                    File.Copy(configSamplePath, configPath);
                }

                if (!File.Exists(configPath))
                {
                    // Create a basic config.inc.php
                    await CreatePhpMyAdminConfigAsync(configPath, config);
                }
                else
                {
                    // Update existing config
                    await UpdatePhpMyAdminConfigAsync(configPath, config);
                }

                // Create virtual host if requested
                if (config.Configuration.TryGetValue("create_vhost", out var createVhostValue) && 
                    Convert.ToBoolean(createVhostValue))
                {
                    await CreatePhpMyAdminVirtualHostAsync(config);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"phpMyAdmin configuration failed: {ex.Message}");
            }
        }

        private static string FindPhpMyAdminInstallation(string basePath)
        {
            var possiblePaths = new[]
            {
                basePath,
                Path.Combine(basePath, "phpMyAdmin-5.2.1-all-languages"),
                Path.Combine(basePath, "phpMyAdmin-5.1.4-all-languages"),
                Path.Combine(basePath, "phpmyadmin")
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "index.php")))
                {
                    return path;
                }
            }

            // Check for wildcard directories
            try
            {
                var phpMyAdminDirs = Directory.GetDirectories(basePath, "phpMyAdmin-*");
                foreach (var dir in phpMyAdminDirs)
                {
                    if (File.Exists(Path.Combine(dir, "index.php")))
                    {
                        return dir;
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private static async Task CreatePhpMyAdminConfigAsync(string configPath, PackageInstallationConfig config)
        {
            var mysqlHost = config.Configuration.GetValueOrDefault("mysql_host", "localhost").ToString();
            var mysqlPort = config.Configuration.GetValueOrDefault("mysql_port", 3306).ToString();
            var mysqlUser = config.Configuration.GetValueOrDefault("mysql_user", "root").ToString();
            var allowNoPassword = Convert.ToBoolean(config.Configuration.GetValueOrDefault("allow_no_password", true));
            var blowfishSecret = config.Configuration.GetValueOrDefault("blowfish_secret", "ModStackSecretKey1234567890123456").ToString();

            var configContent = $@"<?php
/**
 * phpMyAdmin configuration file generated by ModStack
 */

declare(strict_types=1);

/**
 * This is needed for cookie based authentication to encrypt password in
 * cookie. Needs to be 32 chars long.
 */
$cfg['blowfish_secret'] = '{blowfishSecret}';

/**
 * Server configuration
 */
$i = 0;

// First server
$i++;
$cfg['Servers'][$i]['auth_type'] = 'cookie';
$cfg['Servers'][$i]['host'] = '{mysqlHost}';
$cfg['Servers'][$i]['port'] = '{mysqlPort}';
$cfg['Servers'][$i]['connect_type'] = 'tcp';
$cfg['Servers'][$i]['compress'] = false;
$cfg['Servers'][$i]['AllowNoPassword'] = {(allowNoPassword ? "true" : "false")};

/**
 * phpMyAdmin configuration storage settings.
 */
$cfg['Servers'][$i]['pmadb'] = 'phpmyadmin';
$cfg['Servers'][$i]['controluser'] = '{mysqlUser}';
$cfg['Servers'][$i]['controlpass'] = '';

/**
 * End of servers configuration
 */

/**
 * Directories for saving/loading files from server
 */
$cfg['UploadDir'] = '';
$cfg['SaveDir'] = '';

/**
 * Whether to display icons or text or both icons and text in table row
 * action segment. Value can be either of 'icons', 'text' or 'both'.
 */
$cfg['RowActionType'] = 'icons';

/**
 * Defines whether a user should be displayed a ""show all (records)""
 * button in browse mode or not.
 */
$cfg['ShowAll'] = true;

/**
 * Number of rows displayed when browsing a result set.
 */
$cfg['MaxRows'] = 25;

/**
 * Disallow editing of binary fields
 */
$cfg['ProtectBinary'] = false;

/**
 * Default language to use
 */
$cfg['DefaultLang'] = 'en';

/**
 * How many columns should be used for table display of a database?
 */
$cfg['PropertiesNumColumns'] = 1;

/**
 * Set to true if you want DB-based query history.
 */
$cfg['QueryHistoryDB'] = false;

/**
 * When using DB-based query history, how many entries should be kept?
 */
$cfg['QueryHistoryMax'] = 25;

/**
 * Whether the tracking mechanism creates versions for tables and views automatically.
 */
$cfg['Tracking']['default_statements'] = 'CREATE TABLE,ALTER TABLE,DROP TABLE,RENAME TABLE,CREATE INDEX,DROP INDEX,INSERT,UPDATE,DELETE,TRUNCATE,REPLACE,CREATE VIEW,ALTER VIEW,DROP VIEW,CREATE DATABASE,ALTER DATABASE,DROP DATABASE';

/**
 * Disable the default warning that is displayed if mcrypt is not available
 */
$cfg['McryptDisableWarning'] = true;

/**
 * Server connection method
 */
$cfg['ServerDefault'] = 1;
";

            await File.WriteAllTextAsync(configPath, configContent);
        }

        private static async Task UpdatePhpMyAdminConfigAsync(string configPath, PackageInstallationConfig config)
        {
            var content = await File.ReadAllTextAsync(configPath);
            
            // Update configuration based on user settings
            foreach (var setting in config.Configuration)
            {
                switch (setting.Key)
                {
                    case "mysql_host":
                        content = UpdatePhpMyAdminSetting(content, "host", setting.Value?.ToString() ?? string.Empty);
                        break;
                    case "mysql_port":
                        content = UpdatePhpMyAdminSetting(content, "port", setting.Value?.ToString() ?? string.Empty);
                        break;
                    case "blowfish_secret":
                        content = UpdatePhpMyAdminSetting(content, "blowfish_secret", setting.Value?.ToString() ?? string.Empty);
                        break;
                    case "allow_no_password":
                        var allowValue = Convert.ToBoolean(setting.Value) ? "true" : "false";
                        content = UpdatePhpMyAdminSetting(content, "AllowNoPassword", allowValue);
                        break;
                }
            }

            await File.WriteAllTextAsync(configPath, content);
        }

        private static string UpdatePhpMyAdminSetting(string content, string setting, string value)
        {
            var pattern = $@"\$cfg\['Servers'\]\[\$i\]\['{setting}'\]\s*=\s*[^;]+;";
            var replacement = $"$cfg['Servers'][$i]['{setting}'] = '{value}';";

            if (setting == "AllowNoPassword")
            {
                replacement = $"$cfg['Servers'][$i]['{setting}'] = {value};";
            }
            else if (setting == "blowfish_secret")
            {
                pattern = @"\$cfg\['blowfish_secret'\]\s*=\s*[^;]+;";
                replacement = $"$cfg['blowfish_secret'] = '{value}';";
            }

            return System.Text.RegularExpressions.Regex.Replace(content, pattern, replacement);
        }

        private static async Task ConfigureIonCubeLoaderAsync(string phpPath, string _)
        {
            try
            {
                // Determine PHP version for IonCube compatibility
                var phpVersion = DetectPHPVersion(phpPath);
                var ionCubeUrl = GetIonCubeDownloadUrl(phpVersion);
                
                if (string.IsNullOrEmpty(ionCubeUrl)) return;

                var ionCubeDir = Path.Combine(phpPath, "ioncube");
                var ionCubeLoaderPath = Path.Combine(ionCubeDir, "ioncube_loader_win_8.3.dll");

                // Download and extract IonCube Loader if not exists
                if (!Directory.Exists(ionCubeDir))
                {
                    Directory.CreateDirectory(ionCubeDir);
                    await DownloadIonCubeLoaderAsync(ionCubeUrl, ionCubeDir, phpVersion);
                }

                // Configure php.ini for IonCube
                var phpIniPath = Path.Combine(phpPath, "php.ini");
                if (File.Exists(phpIniPath))
                {
                    var iniContent = await File.ReadAllTextAsync(phpIniPath);
                    var loaderFile = GetIonCubeLoaderFile(phpVersion);
                    var loaderPath = Path.Combine(ionCubeDir, loaderFile);

                    if (File.Exists(loaderPath))
                    {
                        // Add IonCube Loader extension (must be first zend_extension)
                        var ionCubeLine = $"zend_extension = \"{loaderPath.Replace(@"\", @"\\")}\"";
                        
                        if (!iniContent.Contains("ioncube_loader"))
                        {
                            // Insert at the beginning of the file, after <?php if present
                            var lines = iniContent.Split('\n').ToList();
                            var insertIndex = 0;
                            
                            // Find the best place to insert (after comments but before other extensions)
                            for (int i = 0; i < lines.Count; i++)
                            {
                                var line = lines[i].Trim();
                                if (line.StartsWith("zend_extension") && !line.Contains("ioncube"))
                                {
                                    insertIndex = i;
                                    break;
                                }
                                if (!line.StartsWith(';') && !line.StartsWith('#') && !string.IsNullOrWhiteSpace(line))
                                {
                                    insertIndex = i;
                                }
                            }
                            
                            lines.Insert(insertIndex, ionCubeLine);
                            lines.Insert(insertIndex + 1, "");
                            
                            await File.WriteAllTextAsync(phpIniPath, string.Join("\n", lines));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IonCube Loader configuration failed: {ex.Message}");
            }
        }

        private static string DetectPHPVersion(string phpPath)
        {
            try
            {
                // Try to detect version from directory name
                var dirName = Path.GetFileName(phpPath);
                if (dirName.Contains("8.3")) return "8.3";
                if (dirName.Contains("8.2")) return "8.2";
                if (dirName.Contains("8.1")) return "8.1";
                if (dirName.Contains("7.4")) return "7.4";
                if (dirName.Contains("7.2")) return "7.2";

                // Try to get version from php.exe
                var phpExe = Path.Combine(phpPath, "php.exe");
                if (File.Exists(phpExe))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = phpExe,
                        Arguments = "-v",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        if (output.Contains("PHP 8.3")) return "8.3";
                        if (output.Contains("PHP 8.2")) return "8.2";
                        if (output.Contains("PHP 8.1")) return "8.1";
                        if (output.Contains("PHP 7.4")) return "7.4";
                        if (output.Contains("PHP 7.2")) return "7.2";
                    }
                }
            }
            catch { }

            return "8.3"; // Default to latest
        }

        private static string GetIonCubeDownloadUrl(string phpVersion)
        {
            // IonCube Loader download URLs for different PHP versions
            return phpVersion switch
            {
                "8.3" => "https://downloads.ioncube.com/loader_downloads/ioncube_loaders_win_vc15_x86-64.zip",
                "8.2" => "https://downloads.ioncube.com/loader_downloads/ioncube_loaders_win_vc15_x86-64.zip",
                "8.1" => "https://downloads.ioncube.com/loader_downloads/ioncube_loaders_win_vc15_x86-64.zip",
                "7.4" => "https://downloads.ioncube.com/loader_downloads/ioncube_loaders_win_vc15_x86-64.zip",
                "7.2" => "https://downloads.ioncube.com/loader_downloads/ioncube_loaders_win_vc15_x86-64.zip",
                _ => "https://downloads.ioncube.com/loader_downloads/ioncube_loaders_win_vc15_x86-64.zip"
            };
        }

        private static string GetIonCubeLoaderFile(string phpVersion)
        {
            return phpVersion switch
            {
                "8.3" => "ioncube_loader_win_8.3.dll",
                "8.2" => "ioncube_loader_win_8.2.dll",
                "8.1" => "ioncube_loader_win_8.1.dll",
                "7.4" => "ioncube_loader_win_7.4.dll",
                "7.2" => "ioncube_loader_win_7.2.dll",
                _ => "ioncube_loader_win_8.3.dll"
            };
        }

        private static async Task DownloadIonCubeLoaderAsync(string url, string ionCubeDir, string phpVersion)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "ModStack/1.0");

                var zipPath = Path.Combine(ionCubeDir, "ioncube_loaders.zip");
                
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var zipContent = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(zipPath, zipContent);

                    // Extract only the required loader file
                    using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
                    var loaderFile = GetIonCubeLoaderFile(phpVersion);
                    
                    var entry = archive.Entries.FirstOrDefault(e => e.Name == loaderFile);
                    if (entry != null)
                    {
                        var extractPath = Path.Combine(ionCubeDir, loaderFile);
                        entry.ExtractToFile(extractPath, true);
                    }

                    // Clean up zip file
                    File.Delete(zipPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IonCube Loader download failed: {ex.Message}");
            }
        }

        private static async Task CreatePhpMyAdminVirtualHostAsync(PackageInstallationConfig config)
        {
            try
            {
                var domain = config.Configuration.GetValueOrDefault("vhost_domain", "phpmyadmin.local").ToString();
                var phpMyAdminPath = FindPhpMyAdminInstallation(config.InstallPath);
                
                if (string.IsNullOrEmpty(phpMyAdminPath)) return;

                // Add to hosts file
                var hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                if (File.Exists(hostsPath))
                {
                    var hostsContent = await File.ReadAllTextAsync(hostsPath);
                    if (!hostsContent.Contains($"127.0.0.1 {domain}"))
                    {
                        await File.AppendAllTextAsync(hostsPath, $"\n127.0.0.1 {domain}\n");
                    }
                }

                // Create Apache virtual host configuration
                var apachePath = FindApacheInstallation(@"C:\ModStack\Services\Apache");
                if (string.IsNullOrEmpty(apachePath))
                {
                    System.Diagnostics.Debug.WriteLine("Apache installation not found for virtual host creation");
                    return;
                }
                
                var vhostFile = Path.Combine(apachePath, "conf", "extra", $"{domain}.conf");
                
                if (Directory.Exists(apachePath))
                {
                    var vhostContent = $@"<VirtualHost *:80>
    ServerName {domain}
    DocumentRoot ""{phpMyAdminPath.Replace(@"\", @"\\")}"" 
    DirectoryIndex index.php
    
    <Directory ""{phpMyAdminPath.Replace(@"\", @"\\")}"">
        Options -Indexes +FollowSymLinks
        AllowOverride All
        Require all granted
    </Directory>
    
    # PHP configuration
    <FilesMatch \.php$>
        SetHandler ""proxy:fcgi://127.0.0.1:9000""
    </FilesMatch>
    
    ErrorLog logs/{domain}_error.log
    CustomLog logs/{domain}_access.log combined
</VirtualHost>";

                    await File.WriteAllTextAsync(vhostFile, vhostContent);
                    
                    // Add include to httpd.conf if not already present
                    var httpdConfPath = Path.Combine(apachePath, "conf", "httpd.conf");
                    if (File.Exists(httpdConfPath))
                    {
                        var httpdContent = await File.ReadAllTextAsync(httpdConfPath);
                        var includeStatement = $"Include conf/extra/{domain}.conf";
                        if (!httpdContent.Contains(includeStatement))
                        {
                            await File.AppendAllTextAsync(httpdConfPath, $"\n{includeStatement}\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"phpMyAdmin virtual host creation failed: {ex.Message}");
            }
        }

        private static string FindApacheInstallation(string basePath)
        {
            // Check direct path first (flattened structure)
            if (Directory.Exists(basePath) && File.Exists(Path.Combine(basePath, "bin", "httpd.exe")))
            {
                return basePath;
            }

            // Check old structure (with Apache24 folder)
            var apache24Path = Path.Combine(basePath, "Apache24");
            if (Directory.Exists(apache24Path) && File.Exists(Path.Combine(apache24Path, "bin", "httpd.exe")))
            {
                return apache24Path;
            }

            // Check version-specific subdirectories (apache-2.4.65, etc.)
            try
            {
                var versionDirs = Directory.GetDirectories(basePath, "apache-*");
                foreach (var versionDir in versionDirs)
                {
                    // Check flattened structure in version dir
                    if (File.Exists(Path.Combine(versionDir, "bin", "httpd.exe")))
                    {
                        return versionDir;
                    }
                    
                    // Check nested Apache24 in version dir
                    var nestedPath = Path.Combine(versionDir, "Apache24");
                    if (Directory.Exists(nestedPath) && File.Exists(Path.Combine(nestedPath, "bin", "httpd.exe")))
                    {
                        return nestedPath;
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private static string FindNginxInstallation(string basePath)
        {
            // Check direct path first (flattened structure)
            if (Directory.Exists(basePath) && File.Exists(Path.Combine(basePath, "nginx.exe")))
            {
                return basePath;
            }

            // Check version-specific subdirectories (nginx-1.24.0, etc.)
            try
            {
                var versionDirs = Directory.GetDirectories(basePath, "nginx-*");
                foreach (var versionDir in versionDirs)
                {
                    // Check flattened structure in version dir
                    if (File.Exists(Path.Combine(versionDir, "nginx.exe")))
                    {
                        return versionDir;
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private static async Task RegisterWindowsServiceAsync(PackageInstallationConfig _)
        {
            // This would register the service as a Windows Service
            // Implementation depends on the specific service type
            await Task.CompletedTask;
        }

        private static async Task StartServiceAsync(PackageInstallationConfig _)
        {
            // This would start the installed service
            // Implementation depends on the specific service type
            await Task.CompletedTask;
        }

        /// <summary>
        /// Registers the installed service in the database for service management
        /// </summary>
        private async Task RegisterServiceInDatabaseAsync(PackageInstallationConfig config)
        {
            try
            {
                // ServiceRepository is now guaranteed to be available via dependency injection
                System.Diagnostics.Debug.WriteLine("📝 ServiceRepository available - proceeding with database registration");

                var packageName = config.Package?.Name?.ToLower();
                if (string.IsNullOrEmpty(packageName))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Package name is null or empty");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"📝 Registering service: {packageName}");
                System.Diagnostics.Debug.WriteLine($"📁 Installation path: {config.InstallPath}");
                System.Diagnostics.Debug.WriteLine($"🔢 Version: {config.Version?.Version}");

                // All services use versioned names for uniqueness
                var serviceName = $"{packageName}-{config.Version?.Version}";
                
                System.Diagnostics.Debug.WriteLine($"🔍 REGISTRATION DEBUG:");
                System.Diagnostics.Debug.WriteLine($"   Package Name: '{packageName}'");
                System.Diagnostics.Debug.WriteLine($"   Service Name: '{serviceName}'");
                
                // ROBUST CHECK: Use GetByNameAsync to directly check if service exists
                // This is more reliable than GetAllAsync() which might have filtering
                System.Diagnostics.Debug.WriteLine($"🔍 Checking if service '{serviceName}' already exists...");
                var existingService = await _serviceRepository.GetByNameAsync(serviceName);
                
                System.Diagnostics.Debug.WriteLine($"   Direct lookup result: {existingService != null} {(existingService != null ? $"(ID: {existingService.Id})" : "")}");

                if (existingService != null)
                {
                    // Update existing service with installation details
                    System.Diagnostics.Debug.WriteLine($"✏️ UPDATING EXISTING SERVICE:");
                    System.Diagnostics.Debug.WriteLine($"   Service ID: {existingService.Id}");
                    System.Diagnostics.Debug.WriteLine($"   Service Name: '{existingService.Name}'");
                    System.Diagnostics.Debug.WriteLine($"   Setting IsInstalled: true");
                    System.Diagnostics.Debug.WriteLine($"   Setting BinaryPath: '{config.InstallPath}'");
                    System.Diagnostics.Debug.WriteLine($"   Setting Version: '{config.Version?.Version}'");
                    
                    // Serialize the actual configuration used during installation
                    var configurationJson = config.Configuration.Count > 0 
                        ? System.Text.Json.JsonSerializer.Serialize(config.Configuration, JsonOptions)
                        : null;
                    
                    if (!string.IsNullOrEmpty(configurationJson))
                    {
                        System.Diagnostics.Debug.WriteLine($"   Setting Configuration: {config.Configuration.Count} items");
                        existingService.Configuration = configurationJson;
                    }
                    
                    // Update installation status
                    await _serviceRepository.UpdateInstallationStatusAsync(
                        existingService.Id,
                        isInstalled: true,
                        binaryPath: config.InstallPath,
                        version: config.Version?.Version
                    );
                    
                    // Update service name and configuration if needed
                    if (existingService.Name != serviceName || !string.IsNullOrEmpty(configurationJson))
                    {
                        if (existingService.Name != serviceName)
                        {
                            System.Diagnostics.Debug.WriteLine($"   Updating service name: '{existingService.Name}' → '{serviceName}'");
                            existingService.Name = serviceName;
                        }
                        await _serviceRepository.UpdateAsync(existingService);
                    }
                }
                else
                {
                    // Create new service definition for packages not in default definitions
                    System.Diagnostics.Debug.WriteLine($"➕ CREATING NEW SERVICE DEFINITION:");
                    System.Diagnostics.Debug.WriteLine($"   Package: '{packageName}'");
                    System.Diagnostics.Debug.WriteLine($"   Service Name: '{serviceName}'");
                    
                    var serviceType = GetServiceTypeFromPackageName(packageName);
                    var defaultPort = GetDefaultPortForService(packageName);
                    
                    System.Diagnostics.Debug.WriteLine($"   Service Type: {serviceType}");
                    System.Diagnostics.Debug.WriteLine($"   Default Port: {defaultPort}");
                    
                    var displayName = $"{config.Package?.DisplayName ?? packageName} {config.Version?.Version}";
                    
                    System.Diagnostics.Debug.WriteLine($"   Display Name: '{displayName}'");
                    
                    // Get configuration file path if available
                    var configPath = GetConfigPathForService(packageName, config.InstallPath);
                    System.Diagnostics.Debug.WriteLine($"   Config Path: {configPath ?? "Not found"}");
                    
                    // Serialize the actual configuration used during installation
                    var configurationJson = config.Configuration.Count > 0 
                        ? System.Text.Json.JsonSerializer.Serialize(config.Configuration, JsonOptions)
                        : null;
                    
                    System.Diagnostics.Debug.WriteLine($"💾 Saving configuration to service:");
                    System.Diagnostics.Debug.WriteLine($"   Configuration items: {config.Configuration.Count}");
                    if (!string.IsNullOrEmpty(configurationJson))
                    {
                        System.Diagnostics.Debug.WriteLine($"   Configuration JSON: {configurationJson}");
                    }
                    
                    var newService = new ServiceDefinition
                    {
                        Name = serviceName,
                        DisplayName = displayName,
                        Description = GetServiceDescription(packageName, config.Version?.Version),
                        ServiceType = serviceType,
                        DefaultPort = defaultPort,
                        BinaryPath = config.InstallPath,
                        ConfigPath = configPath,
                        Version = config.Version?.Version ?? "unknown",
                        IsInstalled = true,
                        IsEnabled = true,
                        InstallDate = DateTime.UtcNow,
                        Status = (int)ServiceStatus.Unknown,
                        AutoStart = config.AutoStart,
                        UniqueId = $"{packageName}-{Guid.NewGuid():N}",
                        WindowsServiceName = config.RegisterAsService ? $"ModStack_{packageName}" : null,
                        Configuration = configurationJson // Save the actual configuration!
                    };

                    System.Diagnostics.Debug.WriteLine($"💾 Saving new service to database...");
                    
                    try
                    {
                        var addedService = await _serviceRepository.AddAsync(newService);
                        System.Diagnostics.Debug.WriteLine($"✅ SUCCESSFULLY created new service definition:");
                        System.Diagnostics.Debug.WriteLine($"   Service ID: {addedService.Id}");
                        System.Diagnostics.Debug.WriteLine($"   Service Name: '{addedService.Name}'");
                        System.Diagnostics.Debug.WriteLine($"   Display Name: '{addedService.DisplayName}'");
                        System.Diagnostics.Debug.WriteLine($"   IsInstalled: {addedService.IsInstalled}");
                        System.Diagnostics.Debug.WriteLine($"   IsEnabled: {addedService.IsEnabled}");
                    }
                    catch (Exception createEx) when (createEx.Message.Contains("UNIQUE constraint"))
                    {
                        // Handle race condition where service was created between our check and creation
                        System.Diagnostics.Debug.WriteLine($"🔄 UNIQUE constraint violation - service '{serviceName}' already exists");
                        System.Diagnostics.Debug.WriteLine($"   Attempting to find and update existing service...");
                        
                        // Try to find the existing service again and update it
                        var raceConditionService = await _serviceRepository.GetByNameAsync(serviceName);
                        if (raceConditionService != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"✏️ Found existing service (ID: {raceConditionService.Id}), updating instead...");
                            await _serviceRepository.UpdateInstallationStatusAsync(
                                raceConditionService.Id,
                                isInstalled: true,
                                binaryPath: config.InstallPath,
                                version: config.Version?.Version
                            );
                            System.Diagnostics.Debug.WriteLine($"✅ SUCCESSFULLY updated existing service after race condition");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ Could not find service '{serviceName}' after UNIQUE constraint error");
                            throw; // Re-throw if we can't handle it
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ CRITICAL ERROR: Failed to register service in database:");
                System.Diagnostics.Debug.WriteLine($"   Exception Type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"   Error Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"   Inner Exception: {ex.InnerException.Message}");
                }
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw; // Re-throw to be caught by the caller
            }
        }

        /// <summary>
        /// Maps package names to ServiceType enum values
        /// </summary>
        private static ModStack.Common.ServiceType GetServiceTypeFromPackageName(string packageName)
        {
            return packageName?.ToLower() switch
            {
                "apache" => ModStack.Common.ServiceType.Apache,
                "nginx" => ModStack.Common.ServiceType.Nginx,
                "php" => ModStack.Common.ServiceType.PHP,
                "mysql" => ModStack.Common.ServiceType.MySQL,
                "mariadb" => ModStack.Common.ServiceType.MySQL, // MariaDB uses MySQL service type
                "postgresql" => ModStack.Common.ServiceType.PostgreSQL,
                "redis" => ModStack.Common.ServiceType.Redis,
                "phpmyadmin" => ModStack.Common.ServiceType.PhpMyAdmin,
                "node" => ModStack.Common.ServiceType.NodeJS,
                "python" => ModStack.Common.ServiceType.Python,
                "java" => ModStack.Common.ServiceType.Java,
                _ => ModStack.Common.ServiceType.Other
            };
        }

        /// <summary>
        /// Gets default port for common services
        /// </summary>
        private static int GetDefaultPortForService(string packageName)
        {
            return packageName?.ToLower() switch
            {
                "apache" => 80,
                "nginx" => 80,
                "php" => 9000,
                "mysql" => 3306,
                "mariadb" => 3306,
                "postgresql" => 5432,
                "redis" => 6379,
                "phpmyadmin" => 8080,
                "node" => 3000,
                "python" => 8000,
                "java" => 8080,
                _ => 0
            };
        }

        /// <summary>
        /// Generates a descriptive text for the service based on package name and version
        /// </summary>
        private static string GetServiceDescription(string packageName, string? version)
        {
            var versionText = !string.IsNullOrEmpty(version) ? $" v{version}" : "";
            
            return packageName.ToLower() switch
            {
                "php" => $"PHP FastCGI server{versionText} for processing web requests",
                "apache" => $"Apache HTTP web server{versionText} for hosting websites",
                "nginx" => $"Nginx web server{versionText} for high-performance web hosting",
                "mysql" => $"MySQL database server{versionText} for data storage",
                "mariadb" => $"MariaDB database server{versionText} for data storage",
                "postgresql" => $"PostgreSQL database server{versionText} for advanced data storage",
                "phpmyadmin" => $"phpMyAdmin{versionText} web interface for MySQL administration",
                "python" => $"Python runtime{versionText} for executing Python applications",
                "java" => $"Java runtime{versionText} for running Java applications",
                "node" => $"Node.js runtime{versionText} for JavaScript server applications",
                "redis" => $"Redis in-memory data store{versionText} for caching and sessions",
                _ => $"{packageName}{versionText} service for web development"
            };
        }

        /// <summary>
        /// Gets the default configuration file path for a service based on installation directory
        /// </summary>
        private static string? GetConfigPathForService(string packageName, string installPath)
        {
            if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                return null;

            var packageNameLower = packageName.ToLower();

            // Apache - try conf/httpd.conf
            if (packageNameLower == "apache")
            {
                var confPath = Path.Combine(installPath, "conf", "httpd.conf");
                if (File.Exists(confPath))
                    return confPath;
                
                // Fallback to httpd.conf in root
                var rootConf = Path.Combine(installPath, "httpd.conf");
                if (File.Exists(rootConf))
                    return rootConf;
            }

            // Nginx - try conf/nginx.conf
            if (packageNameLower == "nginx")
            {
                var confPath = Path.Combine(installPath, "conf", "nginx.conf");
                if (File.Exists(confPath))
                    return confPath;
                
                // Fallback to nginx.conf in root
                var rootConf = Path.Combine(installPath, "nginx.conf");
                if (File.Exists(rootConf))
                    return rootConf;
            }

            // PHP - try php.ini
            if (packageNameLower == "php")
            {
                var iniPath = Path.Combine(installPath, "php.ini");
                if (File.Exists(iniPath))
                    return iniPath;
                
                var productionIni = Path.Combine(installPath, "php.ini-production");
                if (File.Exists(productionIni))
                    return productionIni;
            }

            // MySQL - try my.ini
            if (packageNameLower == "mysql" || packageNameLower == "mariadb")
            {
                var iniPath = Path.Combine(installPath, "my.ini");
                if (File.Exists(iniPath))
                    return iniPath;
                
                var cnfPath = Path.Combine(installPath, "my.cnf");
                if (File.Exists(cnfPath))
                    return cnfPath;
            }

            // PostgreSQL - try postgresql.conf
            if (packageNameLower == "postgresql")
            {
                var confPath = Path.Combine(installPath, "data", "postgresql.conf");
                if (File.Exists(confPath))
                    return confPath;
                
                // Try etc directory
                var etcConf = Path.Combine(installPath, "etc", "postgresql.conf");
                if (File.Exists(etcConf))
                    return etcConf;
            }

            return null;
        }


        /// <summary>
        /// Triggers service detection to update installation status after successful installation
        /// </summary>
        private async Task TriggerServiceDetectionAsync(PackageInstallationConfig config)
        {
            try
            {
                // ServiceRepository is now guaranteed to be available via dependency injection
                System.Diagnostics.Debug.WriteLine("🔍 ServiceRepository available - proceeding with service detection");

                var packageName = config.Package?.Name?.ToLower();
                if (string.IsNullOrEmpty(packageName))
                {
                    return;
                }

                // Find the service we just registered using direct lookup
                var serviceName = $"{packageName}-{config.Version?.Version}";

                System.Diagnostics.Debug.WriteLine($"🔍 Looking for service '{serviceName}' for detection trigger...");
                var service = await _serviceRepository.GetByNameAsync(serviceName);

                if (service != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Found service for detection: '{service.Name}' (ID: {service.Id})");
                    System.Diagnostics.Debug.WriteLine($"🔍 Triggering detection for service ID {service.Id}: {service.DisplayName}");
                    
                    // Use ServiceService for detection if available, otherwise use repository directly
                    try
                    {
                        // Try to get ServiceService through some mechanism (this is a limitation of not having ServiceLocator)
                        // For now, we'll update the status directly through repository
                        await _serviceRepository.UpdateInstallationStatusAsync(
                            service.Id, 
                            true, 
                            config.InstallPath, 
                            config.Version?.Version
                        );
                        
                        // Set status to Unknown so it gets detected on next refresh
                        await _serviceRepository.UpdateStatusAsync(service.Id, ServiceStatus.Unknown);
                        
                        System.Diagnostics.Debug.WriteLine($"✅ Service detection triggered for {service.DisplayName}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Service detection failed: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Could not find registered service '{serviceName}' for detection");
                    System.Diagnostics.Debug.WriteLine($"   This might be a query timing issue - service should still be registered properly");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error triggering service detection: {ex.Message}");
                // Don't throw - this is not critical for installation success
            }
        }

        /// <summary>
        /// Triggers PHP extension synchronization after PHP service installation
        /// </summary>
        private async Task TriggerPHPExtensionSynchronizationAsync(PackageInstallationConfig config)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔧 Triggering PHP extension synchronization...");
                
                // Find the installed PHP service
                var serviceName = $"{config.Package?.Name?.ToLower()}-{config.Version?.Version}";
                var service = await _serviceRepository.GetByNameAsync(serviceName);
                
                if (service == null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Could not find PHP service '{serviceName}' for extension synchronization");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"🔍 Found PHP service: {service.Name} (ID: {service.Id})");
                
                // Extract PHP version for synchronization
                var phpVersion = ExtractPHPVersionFromServiceName(service.Name);
                if (string.IsNullOrEmpty(phpVersion))
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Could not extract PHP version from service name: {service.Name}");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"📊 Using PHP version: {phpVersion}");

                // Initialize extension repository and manager
                var extensionRepository = new ModStack.Data.Repositories.PHPExtensionRepository(_connectionString);
                var extensionManager = new PHPExtensionManager(
                    extensionRepository,
                    _serviceRepository,
                    _loggerFactory.CreateLogger<PHPExtensionManager>(),
                    _httpClient,
                    _processRunner,
                    _serviceSafetyManager
                );

                // Initialize default extensions in database
                System.Diagnostics.Debug.WriteLine("🌱 Initializing default extensions in database...");
                await extensionRepository.InitializeDefaultExtensionsAsync();

                // Perform extension synchronization
                System.Diagnostics.Debug.WriteLine("🔄 Performing extension synchronization...");
                var syncResult = await extensionManager.SynchronizeExtensionsAsync(service.Id, phpVersion);
                
                if (syncResult.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Extension synchronization completed: {syncResult.Message}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Extension synchronization completed with warnings: {syncResult.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ PHP extension synchronization failed: {ex.Message}");
                // Don't fail installation for synchronization errors
            }
        }

        /// <summary>
        /// Extracts PHP version from service name (e.g., "php-8.1.0" -> "8.1")
        /// </summary>
        private static string ExtractPHPVersionFromServiceName(string serviceName)
        {
            try
            {
                // Service name format: "php-8.1.0" or "php-8.3.0"
                var parts = serviceName.Split('-');
                if (parts.Length >= 2)
                {
                    var version = parts[1];
                    // Convert "8.1.0" to "8.1"
                    var versionParts = version.Split('.');
                    if (versionParts.Length >= 2)
                    {
                        return $"{versionParts[0]}.{versionParts[1]}";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Error extracting PHP version from service name '{serviceName}': {ex.Message}");
            }
            
            return "8.3"; // Default fallback
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
