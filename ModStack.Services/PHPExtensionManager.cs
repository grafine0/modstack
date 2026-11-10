using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Common.Security;
using ModStack.Data.Interfaces;
using ModStack.Logging;
using ModStack.Services.Interfaces;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace ModStack.Services;

/// <summary>
/// Strongly-typed error codes for PHP extension operations
/// </summary>
public enum PHPExtensionErrorCode
{
    /// <summary>
    /// Unknown or unrecognized error code
    /// </summary>
    Unknown = 0,
    ServiceNotFound,
    ExtensionNotFound,
    AlreadyInstalled,
    NotInstalled,
    InvalidPath,
    DownloadFailed,
    InvalidHashAlgorithm,
    HashMismatch,
    HashValidationError,
    FileNotCreated,
    IniFileNotFound,
    ConfigCorrupted,
    ConfigUpdateFailed,
    ConfigRemovalFailed,
    FileRemovalFailed,
    InstallFilesFailed,
    InstallFailed,
    UninstallFailed,
    ToggleFailed,
    SyncFailed,
    Cancelled,
    BackupCleanupFailed,
    GlobalBackupCleanupFailed
}

/// <summary>
/// String constants for PHP extension error codes (JSON-friendly)
/// </summary>
public static class PHPExtensionErrorCodes
{
    public const string ServiceNotFound = "SERVICE_NOT_FOUND";
    public const string ExtensionNotFound = "EXTENSION_NOT_FOUND";
    public const string AlreadyInstalled = "ALREADY_INSTALLED";
    public const string NotInstalled = "NOT_INSTALLED";
    public const string InvalidPath = "INVALID_PATH";
    public const string DownloadFailed = "DOWNLOAD_FAILED";
    public const string InvalidHashAlgorithm = "INVALID_HASH_ALGORITHM";
    public const string HashMismatch = "HASH_MISMATCH";
    public const string HashValidationError = "HASH_VALIDATION_ERROR";
    public const string FileNotCreated = "FILE_NOT_CREATED";
    public const string IniFileNotFound = "INI_FILE_NOT_FOUND";
    public const string ConfigCorrupted = "CONFIG_CORRUPTED";
    public const string ConfigUpdateFailed = "CONFIG_UPDATE_FAILED";
    public const string ConfigRemovalFailed = "CONFIG_REMOVAL_FAILED";
    public const string FileRemovalFailed = "FILE_REMOVAL_FAILED";
    public const string InstallFilesFailed = "INSTALL_FILES_FAILED";
    public const string InstallFailed = "INSTALL_FAILED";
    public const string UninstallFailed = "UNINSTALL_FAILED";
    public const string ToggleFailed = "TOGGLE_FAILED";
    public const string SyncFailed = "SYNC_FAILED";
    public const string Cancelled = "CANCELLED";
    public const string BackupCleanupFailed = "BACKUP_CLEANUP_FAILED";
    public const string GlobalBackupCleanupFailed = "GLOBAL_BACKUP_CLEANUP_FAILED";
    
    /// <summary>
    /// Converts enum error code to string representation
    /// </summary>
    public static string ToString(PHPExtensionErrorCode errorCode) => errorCode switch
    {
        PHPExtensionErrorCode.Unknown => "UNKNOWN",
        PHPExtensionErrorCode.ServiceNotFound => ServiceNotFound,
        PHPExtensionErrorCode.ExtensionNotFound => ExtensionNotFound,
        PHPExtensionErrorCode.AlreadyInstalled => AlreadyInstalled,
        PHPExtensionErrorCode.NotInstalled => NotInstalled,
        PHPExtensionErrorCode.InvalidPath => InvalidPath,
        PHPExtensionErrorCode.DownloadFailed => DownloadFailed,
        PHPExtensionErrorCode.InvalidHashAlgorithm => InvalidHashAlgorithm,
        PHPExtensionErrorCode.HashMismatch => HashMismatch,
        PHPExtensionErrorCode.HashValidationError => HashValidationError,
        PHPExtensionErrorCode.FileNotCreated => FileNotCreated,
        PHPExtensionErrorCode.IniFileNotFound => IniFileNotFound,
        PHPExtensionErrorCode.ConfigCorrupted => ConfigCorrupted,
        PHPExtensionErrorCode.ConfigUpdateFailed => ConfigUpdateFailed,
        PHPExtensionErrorCode.ConfigRemovalFailed => ConfigRemovalFailed,
        PHPExtensionErrorCode.FileRemovalFailed => FileRemovalFailed,
        PHPExtensionErrorCode.InstallFilesFailed => InstallFilesFailed,
        PHPExtensionErrorCode.InstallFailed => InstallFailed,
        PHPExtensionErrorCode.UninstallFailed => UninstallFailed,
        PHPExtensionErrorCode.ToggleFailed => ToggleFailed,
        PHPExtensionErrorCode.SyncFailed => SyncFailed,
        PHPExtensionErrorCode.Cancelled => Cancelled,
        PHPExtensionErrorCode.BackupCleanupFailed => BackupCleanupFailed,
        PHPExtensionErrorCode.GlobalBackupCleanupFailed => GlobalBackupCleanupFailed,
        _ => throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, "Unknown error code")
    };
    
    /// <summary>
    /// Tries to parse string error code to enum with contextual logging for unknown codes
    /// </summary>
    public static bool TryParse(string errorCode, out PHPExtensionErrorCode result, ILogger? logger = null, bool isCriticalContext = false)
    {
        result = errorCode switch
        {
            "UNKNOWN" => PHPExtensionErrorCode.Unknown,
            ServiceNotFound => PHPExtensionErrorCode.ServiceNotFound,
            ExtensionNotFound => PHPExtensionErrorCode.ExtensionNotFound,
            AlreadyInstalled => PHPExtensionErrorCode.AlreadyInstalled,
            NotInstalled => PHPExtensionErrorCode.NotInstalled,
            InvalidPath => PHPExtensionErrorCode.InvalidPath,
            DownloadFailed => PHPExtensionErrorCode.DownloadFailed,
            InvalidHashAlgorithm => PHPExtensionErrorCode.InvalidHashAlgorithm,
            HashMismatch => PHPExtensionErrorCode.HashMismatch,
            HashValidationError => PHPExtensionErrorCode.HashValidationError,
            FileNotCreated => PHPExtensionErrorCode.FileNotCreated,
            IniFileNotFound => PHPExtensionErrorCode.IniFileNotFound,
            ConfigCorrupted => PHPExtensionErrorCode.ConfigCorrupted,
            ConfigUpdateFailed => PHPExtensionErrorCode.ConfigUpdateFailed,
            ConfigRemovalFailed => PHPExtensionErrorCode.ConfigRemovalFailed,
            FileRemovalFailed => PHPExtensionErrorCode.FileRemovalFailed,
            InstallFilesFailed => PHPExtensionErrorCode.InstallFilesFailed,
            InstallFailed => PHPExtensionErrorCode.InstallFailed,
            UninstallFailed => PHPExtensionErrorCode.UninstallFailed,
            ToggleFailed => PHPExtensionErrorCode.ToggleFailed,
            SyncFailed => PHPExtensionErrorCode.SyncFailed,
            Cancelled => PHPExtensionErrorCode.Cancelled,
            BackupCleanupFailed => PHPExtensionErrorCode.BackupCleanupFailed,
            GlobalBackupCleanupFailed => PHPExtensionErrorCode.GlobalBackupCleanupFailed,
            _ => PHPExtensionErrorCode.Unknown
        };
        
        var isKnown = result != PHPExtensionErrorCode.Unknown;
        
        // Log unknown error codes with configurable severity
        if (!isKnown && !string.IsNullOrEmpty(errorCode))
        {
            // Unknown error code encountered
            
            // Determine log level based on context and configuration
            if (isCriticalContext)
            {
                logger?.LogError("Critical unknown error code encountered: {ErrorCode}. This indicates a serious version mismatch or missing error code definition that could affect system stability.", errorCode);
            }
            else
            {
                logger?.LogWarning("Unknown error code encountered during parsing: {ErrorCode}. This may indicate a version mismatch or missing error code definition.", errorCode);
            }
        }
        
        return isKnown;
    }
}

/// <summary>
/// Result object for operations with detailed error information
/// </summary>
public record OperationResult(bool Success, string Message, string? ErrorCode = null);

/// <summary>
/// Result object for extension download operations
/// </summary>
public record ExtensionDownloadResult(bool Success, string Message, string? ErrorCode = null, byte[]? FileBytes = null) : OperationResult(Success, Message, ErrorCode);

/// <summary>
/// Structured result for backup cleanup operations
/// </summary>
public record CleanupResult(bool Success, string Message, string? ErrorCode = null, int DeletedCount = 0, int ScannedCount = 0) : OperationResult(Success, Message, ErrorCode);

/// <summary>
/// Static factory for creating OperationResult instances with consistent patterns
/// </summary>
public static class Result
{
    /// <summary>
    /// Creates a successful OperationResult
    /// </summary>
    public static OperationResult Success(string message) => new(true, message);
    
    /// <summary>
    /// Creates a failed OperationResult with error code (string)
    /// </summary>
    public static OperationResult Fail(string message, string errorCode) => new(false, message, errorCode);
    
    /// <summary>
    /// Creates a failed OperationResult with error code (enum)
    /// </summary>
    public static OperationResult Fail(string message, PHPExtensionErrorCode errorCode) => new(false, message, PHPExtensionErrorCodes.ToString(errorCode));
    
    /// <summary>
    /// Creates a failed OperationResult without error code (for backwards compatibility)
    /// </summary>
    public static OperationResult Fail(string message) => new(false, message);
}

/// <summary>
/// Static factory for creating ExtensionDownloadResult instances
/// </summary>
public static class DownloadResult
{
    /// <summary>
    /// Creates a successful ExtensionDownloadResult with file bytes
    /// </summary>
    public static ExtensionDownloadResult Success(string message, byte[] fileBytes) => new(true, message, null, fileBytes);
    
    /// <summary>
    /// Creates a failed ExtensionDownloadResult with error code (string)
    /// </summary>
    public static ExtensionDownloadResult Fail(string message, string errorCode) => new(false, message, errorCode);
    
    /// <summary>
    /// Creates a failed ExtensionDownloadResult with error code (enum)
    /// </summary>
    public static ExtensionDownloadResult Fail(string message, PHPExtensionErrorCode errorCode) => new(false, message, PHPExtensionErrorCodes.ToString(errorCode));
}

/// <summary>
/// Configuration options for PHP extension management
/// </summary>
public class PHPExtensionManagerOptions
{
    /// <summary>
    /// If true, removes extensions from database even if DLL exists but extension is not loaded in php -m.
    /// If false, only removes extensions if both DLL file is missing AND extension is not loaded.
    /// </summary>
    public bool StrictSynchronizationRemoval { get; set; } = false;
    
    /// <summary>
    /// Maximum age of backup files before they are automatically cleaned up
    /// </summary>
    public TimeSpan BackupRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
    
    /// <summary>
    /// If true, automatically cleans up old backup files during operations
    /// </summary>
    public bool AutoCleanupBackups { get; set; } = true;
    
    /// <summary>
    /// If true, creates backup of DLL files before removing them (allows recovery)
    /// </summary>
    public bool BackupDllsBeforeRemoval { get; set; } = true;
    
    /// <summary>
    /// Number of retry attempts for hash validation failures (corrupted downloads)
    /// </summary>
    public int HashValidationRetries { get; set; } = 1;
    
    /// <summary>
    /// Maximum degree of parallelism for global backup cleanup operations
    /// </summary>
    public int BackupCleanupMaxParallelism { get; set; } = Environment.ProcessorCount;
    
    /// <summary>
    /// Maximum number of backup files to keep per directory (prevents unlimited growth)
    /// </summary>
    public int MaxBackupsPerDirectory { get; set; } = 50;
    
    /// <summary>
    /// If true, performs cleanup on Dispose for safety even if AutoCleanupBackups is false
    /// </summary>
    public bool CleanupOnDispose { get; set; } = true;
    
    /// <summary>
    /// If true, logs all unknown error codes as Error level (useful for CI/CD or strict environments)
    /// If false, logs unknown codes as Warning level (default for development)
    /// </summary>
    public bool TreatUnknownErrorCodesAsCritical { get; set; } = false;
    
    /// <summary>
    /// If true, performs post-write normalization on php.ini files to clean up line endings and formatting
    /// This only removes truly excessive blank lines (3+ consecutive) to prevent file bloat
    /// </summary>
    public bool EnablePostWriteNormalization { get; set; } = true;
}

/// <summary>
/// Manages PHP extensions for specific PHP versions
/// </summary>
public class PHPExtensionManager(
    IPHPExtensionRepository extensionRepository,
    IServiceRepository serviceRepository,
    ILogger<PHPExtensionManager> logger,
    HttpClient httpClient,
    IProcessRunner processRunner,
    IServiceSafetyManager serviceSafetyManager,
    PHPExtensionManagerOptions? options = null) : IDisposable
{
    private readonly IPHPExtensionRepository _extensionRepository = extensionRepository;
    private readonly IServiceRepository _serviceRepository = serviceRepository;
    private readonly ILogger<PHPExtensionManager> _logger = logger;
    private readonly HttpClient _httpClient = httpClient;
    private readonly IProcessRunner _processRunner = processRunner;
    private readonly IServiceSafetyManager _serviceSafetyManager = serviceSafetyManager;
    private readonly SemaphoreSlim _phpIniLock = new(1, 1); // Ensure only one php.ini operation at a time
    private readonly PHPExtensionManagerOptions _options = options ?? new PHPExtensionManagerOptions();

    /// <summary>
    /// Gets all available extensions for a specific PHP version
    /// </summary>
    public async Task<List<PHPExtension>> GetAvailableExtensionsAsync(string phpVersion)
    {
        return await _extensionRepository.GetExtensionsForPHPVersionAsync(phpVersion);
    }

    /// <summary>
    /// Gets a specific extension by ID
    /// </summary>
    public async Task<PHPExtension?> GetExtensionByIdAsync(long extensionId)
    {
        return await _extensionRepository.GetExtensionByIdAsync(extensionId);
    }

    /// <summary>
    /// Gets installed extensions for a PHP service
    /// </summary>
    public async Task<List<InstalledPHPExtension>> GetInstalledExtensionsAsync(long serviceId, string? phpVersion = null)
    {
        if (string.IsNullOrEmpty(phpVersion))
        {
            return await _extensionRepository.GetInstalledExtensionsAsync(serviceId);
        }
        
        return await _extensionRepository.GetInstalledExtensionsForVersionAsync(serviceId, phpVersion);
    }

    /// <summary>
    /// Comprehensive check: Database + File + PHP loading
    /// </summary>
    public async Task<bool> IsExtensionActuallyInstalledAsync(long serviceId, long extensionId)
    {
        try
        {
            // Get extension details
            var extension = await _extensionRepository.GetExtensionByIdAsync(extensionId);
            if (extension == null) return false;

            // Get service details
            var service = await _serviceRepository.GetByIdAsync(serviceId, CancellationToken.None);
            if (service == null) return false;

            // Check 1: Database record exists
            var hasDbRecord = await _extensionRepository.IsExtensionInstalledAsync(serviceId, extensionId);
            
            // Check 2: DLL file exists (for built-in extensions)
            var hasDllFile = true; // Default to true for non-built-in extensions
            if (extension.IsBuiltIn && !string.IsNullOrEmpty(extension.DllName))
            {
                var servicePath = service.BinaryPath;
                var phpExe = GetPhpExecutablePath(servicePath);
                if (!string.IsNullOrEmpty(phpExe))
                {
                    var phpPath = Path.GetDirectoryName(phpExe);
                    if (!string.IsNullOrEmpty(phpPath))
                    {
                        var dllPath = Path.Combine(phpPath, "ext", extension.DllName);
                        hasDllFile = File.Exists(dllPath);
                        
                        LogService.Info($"🔍 Extension {extension.Name}: DLL exists at {dllPath}: {hasDllFile}");
                    }
                }
            }

            // Extension is "installed" if it has database record OR DLL file exists
            // This allows file-based detection even without database records
            return hasDbRecord || hasDllFile;
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Error checking extension installation: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Installs a PHP extension for a specific PHP version
    /// </summary>
    public async Task<OperationResult> InstallExtensionAsync(InstallPHPExtensionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            LogService.Info($"Installing PHP extension {request.ExtensionId} for service {request.ServiceId}");

            // Get the PHP service
            var service = await _serviceRepository.GetByIdAsync(request.ServiceId, cancellationToken);
            if (service == null)
            {
                LogService.Error($"PHP service {request.ServiceId} not found");
                return Result.Fail($"PHP service {request.ServiceId} not found", PHPExtensionErrorCodes.ServiceNotFound);
            }

            // Get the extension details
            var extension = await _extensionRepository.GetExtensionByIdAsync(request.ExtensionId);
            if (extension == null)
            {
                LogService.Error($"Extension {request.ExtensionId} not found");
                return Result.Fail($"Extension {request.ExtensionId} not found", PHPExtensionErrorCodes.ExtensionNotFound);
            }

            // Check if already installed
            var existing = await _extensionRepository.GetInstalledExtensionAsync(request.ServiceId, request.ExtensionId);
            if (existing != null)
            {
                // Extension is in database, check if it's enabled in php.ini
                var isEnabledInIni = await IsExtensionEnabledInPhpIniAsync(request.ServiceId, extension.Name, request.PHPVersion, cancellationToken);
                
                if (isEnabledInIni)
                {
                    LogService.Info($"Extension {extension.Name} is already installed and enabled for service {request.ServiceId}");
                    return Result.Success( $"Extension {extension.DisplayName} is already installed and enabled");
                }
                else
                {
                    // Extension is installed but not enabled, just enable it
                    LogService.Info($"Extension {extension.Name} is installed but not enabled, enabling it in php.ini");
                    var enableResult = await UpdatePHPConfiguration(service, extension, request.PHPVersion, request.EnableAfterInstall, cancellationToken);
                    if (!enableResult.Success)
                    {
                        LogService.Error($"Failed to enable extension {extension.Name} in php.ini: {enableResult.Message}");
                        return enableResult;
                    }
                    
                    // Update database to reflect enabled state
                    if (request.EnableAfterInstall)
                    {
                        await _extensionRepository.EnableExtensionAsync(request.ServiceId, request.ExtensionId);
                    }
                    else
                    {
                        await _extensionRepository.DisableExtensionAsync(request.ServiceId, request.ExtensionId);
                    }
                    
                    LogService.Info($"✅ Extension {extension.Name} enabled successfully");
                    LogService.Info($"ℹ️ Note: PHP restart may be required for extension to load properly");
                    return Result.Success( $"Extension {extension.DisplayName} enabled successfully. PHP restart may be required.");
                }
            }

            // Install the extension
            var installResult = await InstallExtensionFiles(service, extension, request.PHPVersion, cancellationToken);
            if (!installResult.Success)
            {
                LogService.Error($"Failed to install extension files for {extension.Name}: {installResult.Message}");
                return installResult;
            }

            // Update PHP configuration
            var configResult = await UpdatePHPConfiguration(service, extension, request.PHPVersion, request.EnableAfterInstall, cancellationToken);
            if (!configResult.Success)
            {
                return configResult;
            }

            // Record installation in database
            await _extensionRepository.InstallExtensionAsync(request);

            LogService.Info($"PHP extension {extension.Name} installed successfully for service {request.ServiceId}");
            return Result.Success($"Extension {extension.DisplayName} installed successfully");
        }
        catch (OperationCanceledException)
        {
            LogService.Warning($"Installation of PHP extension {request.ExtensionId} was cancelled");
            return Result.Fail( "Installation was cancelled", PHPExtensionErrorCodes.Cancelled);
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to install PHP extension {request.ExtensionId} for service {request.ServiceId}: {ex.Message}");
            return Result.Fail( ex.Message, PHPExtensionErrorCodes.InstallFailed);
        }
    }

    /// <summary>
    /// Uninstalls a PHP extension
    /// </summary>
    public async Task<OperationResult> UninstallExtensionAsync(long serviceId, long extensionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var installed = await _extensionRepository.GetInstalledExtensionAsync(serviceId, extensionId);
            if (installed == null)
            {
                LogService.Warning($"Extension {extensionId} is not installed for service {serviceId}");
                return Result.Fail( "Extension is not installed", PHPExtensionErrorCodes.NotInstalled);
            }

            var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
            if (service == null)
            {
                LogService.Error($"PHP service {serviceId} not found");
                return Result.Fail( $"PHP service {serviceId} not found", PHPExtensionErrorCodes.ServiceNotFound);
            }

            // Remove from PHP configuration
            var configResult = await RemoveFromPHPConfiguration(service, installed.Extension!, installed.PHPVersion, cancellationToken);
            if (!configResult.Success)
            {
                return configResult;
            }

            // Remove extension files (if not built-in)
            if (!installed.Extension!.IsBuiltIn)
            {
                var removeResult = await RemoveExtensionFiles(service, installed.Extension, installed.PHPVersion, cancellationToken);
                if (!removeResult.Success)
                {
                    return removeResult;
                }
            }

            // Remove from database
            await _extensionRepository.UninstallExtensionAsync(serviceId, extensionId);

            LogService.Info($"PHP extension {installed.Extension.Name} uninstalled successfully");
            return Result.Success( $"Extension {installed.Extension.DisplayName} uninstalled successfully");
        }
        catch (OperationCanceledException)
        {
            LogService.Warning($"Uninstallation of PHP extension {extensionId} was cancelled");
            return Result.Fail( "Uninstallation was cancelled", PHPExtensionErrorCodes.Cancelled);
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to uninstall PHP extension {extensionId} for service {serviceId}: {ex.Message}");
            return Result.Fail( ex.Message, PHPExtensionErrorCodes.UninstallFailed);
        }
    }

    /// <summary>
    /// Enables or disables a PHP extension
    /// </summary>
    public async Task<OperationResult> ToggleExtensionAsync(long serviceId, long extensionId, bool enable, CancellationToken cancellationToken = default)
    {
        try
        {
            // First check if extension is installed in database
            var installed = await _extensionRepository.GetInstalledExtensionAsync(serviceId, extensionId);
            
            // If not in database, check if it exists in file system
            if (installed == null)
            {
                var isActuallyInstalled = await IsExtensionActuallyInstalledAsync(serviceId, extensionId);
                if (!isActuallyInstalled)
                {
                    LogService.Error($"Extension {extensionId} is not installed for service {serviceId}");
                    return Result.Fail( "Extension is not installed", PHPExtensionErrorCodes.NotInstalled);
                }
                
                // Auto-install the extension since it exists in file system
                LogService.Info($"Auto-installing extension {extensionId} since it exists in file system");
                LogService.Info($"Service ID: {serviceId}, Extension ID: {extensionId}");
                
                // Verify the service exists first
                var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
                if (service == null)
                {
                    LogService.Error($"PHP service {serviceId} not found in Services table");
                    return Result.Fail( $"PHP service {serviceId} not found", PHPExtensionErrorCodes.ServiceNotFound);
                }
                
                LogService.Info($"Verified PHP service exists: {service.Name} (ID: {service.Id})");
                
                // First, ensure the extension exists in PHPExtensions table
                var extension = await _extensionRepository.GetExtensionByIdAsync(extensionId);
                if (extension == null)
                {
                    LogService.Error($"Extension {extensionId} not found in PHPExtensions table");
                    return Result.Fail( "Extension definition not found in database", PHPExtensionErrorCodes.ExtensionNotFound);
                }
                
                LogService.Info($"Found extension definition: {extension.Name} (ID: {extension.Id})");
                
                var installRequest = new InstallPHPExtensionRequest
                {
                    ServiceId = serviceId,
                    ExtensionId = extensionId,
                    PHPVersion = service.Version, // Use actual PHP version from service
                    EnableAfterInstall = false // Don't enable yet, we'll toggle it
                };
                
                var installResult = await InstallExtensionAsync(installRequest, cancellationToken);
                if (!installResult.Success)
                {
                    return Result.Fail( $"Failed to auto-install extension: {installResult.Message}", PHPExtensionErrorCodes.InstallFailed);
                }
                
                // Get the newly installed extension
                installed = await _extensionRepository.GetInstalledExtensionAsync(serviceId, extensionId);
                if (installed == null)
                {
                    return Result.Fail( "Failed to retrieve installed extension", PHPExtensionErrorCodes.InstallFailed);
                }
            }

            // Get service for configuration update (reuse if already fetched)
            var serviceForConfig = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
            if (serviceForConfig == null)
            {
                LogService.Error($"PHP service {serviceId} not found");
                return Result.Fail( $"PHP service {serviceId} not found", PHPExtensionErrorCodes.ServiceNotFound);
            }

            // Update PHP configuration
            var configResult = await UpdateExtensionInPHPConfiguration(serviceForConfig, installed.Extension!, installed.PHPVersion, enable, cancellationToken);
            if (!configResult.Success)
            {
                return configResult;
            }

            // Update database
            if (enable)
            {
                await _extensionRepository.EnableExtensionAsync(serviceId, extensionId);
            }
            else
            {
                await _extensionRepository.DisableExtensionAsync(serviceId, extensionId);
            }

            var action = enable ? "enabled" : "disabled";
            LogService.Info($"PHP extension {installed.Extension!.Name} {action} successfully");
            return Result.Success( $"Extension {installed.Extension.DisplayName} {action} successfully");
        }
        catch (OperationCanceledException)
        {
            LogService.Warning($"Toggle operation for PHP extension {extensionId} was cancelled");
            return Result.Fail( "Toggle operation was cancelled", PHPExtensionErrorCodes.Cancelled);
        }
        catch (Exception ex)
        {
            var actionText = enable ? "enable" : "disable";
            LogService.Error($"Failed to {actionText} PHP extension {extensionId}: {ex.Message}");
            return Result.Fail( ex.Message, PHPExtensionErrorCodes.ToggleFailed);
        }
    }

    /// <summary>
    /// Gets the PHP ini file path for a specific service and version
    /// </summary>
    public async Task<string?> GetPHPIniPathAsync(long serviceId, string _)
    {
        var service = await _serviceRepository.GetByIdAsync(serviceId, CancellationToken.None);
        if (service == null) 
        {
            LogService.Error($"PHP service {serviceId} not found");
            return null;
        }

        LogService.Info($"PHP service found - Name: {service.Name}, BinaryPath: {service.BinaryPath}");

        var servicePath = service.BinaryPath;
        var phpExe = GetPhpExecutablePath(servicePath);
        if (string.IsNullOrEmpty(phpExe)) 
        {
            LogService.Error($"Could not find PHP executable in service path: {servicePath}");
            return null;
        }
        
        var phpPath = Path.GetDirectoryName(phpExe);
        if (string.IsNullOrEmpty(phpPath))
        {
            LogService.Error($"Could not determine PHP directory from executable: {phpExe}");
            return null;
        }

        var phpIniPath = Path.Combine(phpPath, "php.ini");
        LogService.Info($"Constructed PHP ini path: {phpIniPath}");
        
        return phpIniPath;
    }
    
    /// <summary>
    /// Tries to parse string error code with instance-specific configuration
    /// </summary>
    public bool TryParseErrorCodeWithOptions(string errorCode, out PHPExtensionErrorCode result, bool isCriticalContext = false)
    {
        return PHPExtensionErrorCodes.TryParse(errorCode, out result, _logger, isCriticalContext || _options.TreatUnknownErrorCodesAsCritical);
    }
    
    /// <summary>
    /// Manually normalizes an existing php.ini file to clean up formatting and line endings
    /// This can be called to clean up files that were modified outside of the extension manager
    /// </summary>
    public async Task<OperationResult> CleanupPhpIniFileAsync(long serviceId, string phpVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            // Manual cleanup requested
            LogService.Info($"Manual php.ini cleanup requested for service {serviceId}, version {phpVersion}");
            
            var phpIniPath = await GetPHPIniPathAsync(serviceId, phpVersion);
            if (string.IsNullOrEmpty(phpIniPath) || !File.Exists(phpIniPath))
            {
                // PHP ini file not found
                return Result.Fail($"PHP ini file not found: {phpIniPath}", PHPExtensionErrorCodes.IniFileNotFound);
            }
            
            // Create or update single backup before cleanup
            var backupPath = $"{phpIniPath}.backup";
            await CreateSingleBackupAsync(phpIniPath, backupPath, cancellationToken);
            Debug.WriteLine($"📋 Ensured backup exists before cleanup: {backupPath}");
            LogService.Info($"Ensured backup exists before cleanup: {backupPath}");
            
            // Perform normalization
            var result = await NormalizePhpIniFileAsync(phpIniPath, cancellationToken);
            
            if (result.Success)
            {
                Debug.WriteLine($"✅ Manual php.ini cleanup completed successfully");
                LogService.Info($"Manual php.ini cleanup completed: {result.Message}");
                return Result.Success($"PHP ini file cleaned up successfully. {result.Message}. Backup maintained at: {backupPath}");
            }
            else
            {
                Debug.WriteLine($"❌ Manual php.ini cleanup failed: {result.Message}");
                return result;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error during manual php.ini cleanup: {ex.Message}");
            LogService.Error($"Error during manual php.ini cleanup for service {serviceId}: {ex.Message}");
            return Result.Fail($"Manual cleanup failed: {ex.Message}", PHPExtensionErrorCodes.ConfigUpdateFailed);
        }
    }

    #region Private Methods

    /// <summary>
    /// Executes a file operation with retry logic for resilience against file locks
    /// </summary>
    private static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3, int baseDelayMs = 100, CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await operation();
            }
            catch (IOException ex) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1); // Exponential backoff
                Debug.WriteLine($"🔄 File operation failed (attempt {attempt}/{maxRetries}), retrying in {delay}ms: {ex.Message}");
                LogService.Warning($"File operation failed (attempt {attempt}/{maxRetries}), retrying in {delay}ms: {ex.Message}");
                
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine($"🛑 Retry delay cancelled, aborting retry attempts");
                    throw;
                }
            }
        }
        
        // Final attempt without catch
        cancellationToken.ThrowIfCancellationRequested();
        return await operation();
    }

    /// <summary>
    /// Executes a file operation with retry logic (overload for operations without return value)
    /// </summary>
    private static async Task ExecuteWithRetryAsync(Func<Task> operation, int maxRetries = 3, int baseDelayMs = 100, CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async () => { await operation(); return true; }, maxRetries, baseDelayMs, cancellationToken);
    }

    private async Task<OperationResult> InstallExtensionFiles(ServiceDefinition service, PHPExtension extension, string phpVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            // Built-in extensions don't need file installation
            if (extension.IsBuiltIn)
            {
                Debug.WriteLine($"Extension {extension.Name} is built-in, no file installation needed");
                LogService.Info($"Extension {extension.Name} is built-in, no file installation needed");
                return Result.Success( "Built-in extension, no file installation needed");
            }

            // For external extensions, download and install DLL files
            if (!string.IsNullOrEmpty(extension.DownloadUrl))
            {
                var basePath = service.BinaryPath;
                var phpExe = GetPhpExecutablePath(basePath);
                if (string.IsNullOrEmpty(phpExe))
                {
                    LogService.Error($"❌ Could not find PHP executable for version {phpVersion}");
                    LogService.Error($"Could not find PHP executable in {basePath}");
                    return Result.Fail( $"Could not find PHP executable for version {phpVersion}", PHPExtensionErrorCodes.InvalidPath);
                }
                
                var phpPath = Path.GetDirectoryName(phpExe);
                if (string.IsNullOrEmpty(phpPath))
                {
                    LogService.Error($"❌ Could not determine PHP directory from executable: {phpExe}");
                    return Result.Fail( $"Could not determine PHP directory from executable", PHPExtensionErrorCodes.InvalidPath);
                }
                var extDir = Path.Combine(phpPath, "ext");
                
                Debug.WriteLine($"🔍 PHP path: {phpPath}");
                Debug.WriteLine($"🔍 Extension directory: {extDir}");
                
                if (!Directory.Exists(extDir))
                {
                    Debug.WriteLine($"📁 Creating extension directory: {extDir}");
                    Directory.CreateDirectory(extDir);
                }

                // Determine and validate DLL filename strictly within extDir
                var dllName = extension.DllName ?? $"php_{extension.Name}.dll";
                var dllPath = SecurityValidator.ValidateSafePathWithinRoot(extDir, dllName);
                
                // Download and verify the extension file
                var downloadResult = await DownloadAndVerifyExtensionAsync(extension, cancellationToken);
                if (!downloadResult.Success)
                {
                    return downloadResult;
                }
                
                // Write the extension file
                if (downloadResult.FileBytes is null)
                {
                    return Result.Fail("Downloaded file bytes are null", PHPExtensionErrorCodes.DownloadFailed);
                }
                
                var writeResult = await WriteExtensionFileAsync(dllPath, downloadResult.FileBytes, cancellationToken);
                if (!writeResult.Success)
                {
                    return writeResult;
                }
                
                Debug.WriteLine($"✅ Successfully downloaded and saved extension DLL: {dllPath}");
                Debug.WriteLine($"📊 File size: {downloadResult.FileBytes.Length} bytes");
                LogService.Info($"Successfully installed extension {extension.Name} files to {dllPath}, Size: {downloadResult.FileBytes.Length} bytes");
                
                return Result.Success( "Extension files installed successfully");
            }
            else
            {
                Debug.WriteLine($"⚠️ Extension {extension.Name} has no download URL, assuming local installation");
                LogService.Info($"Extension {extension.Name} has no download URL, assuming local installation");
                return Result.Success( "Extension has no download URL, assuming local installation");
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"❌ Installation cancelled for {extension.Name}");
            LogService.Warning($"Installation cancelled for extension {extension.Name}");
            return Result.Fail( "Installation was cancelled", PHPExtensionErrorCodes.Cancelled);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Exception installing extension files: {ex.Message}");
            LogService.Error($"Failed to install extension files for {extension.Name}: {ex.Message}");
            return Result.Fail( ex.Message, PHPExtensionErrorCodes.InstallFilesFailed);
        }
    }
    
    /// <summary>
    /// Downloads and verifies an extension file from the given URL with retry support
    /// </summary>
    private async Task<ExtensionDownloadResult> DownloadAndVerifyExtensionAsync(PHPExtension extension, CancellationToken cancellationToken = default)
    {
        var maxRetries = _options.HashValidationRetries + 1; // +1 for initial attempt
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Debug.WriteLine($"📥 Downloading {extension.Name} from: {extension.DownloadUrl} (attempt {attempt}/{maxRetries})");
                LogService.Info($"Downloading extension {extension.Name} from {extension.DownloadUrl} (attempt {attempt}/{maxRetries})");

                // Enforce HTTPS transport before downloading
                var validatedUri = SecurityValidator.ValidateHttpsUrlOrThrow(extension.DownloadUrl);

                // Download the extension file using injected HttpClient
                using var response = await _httpClient.GetAsync(validatedUri, cancellationToken);
                if (response.RequestMessage?.RequestUri is { } finalUri &&
                    !string.Equals(finalUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    return DownloadResult.Fail($"Insecure redirect to '{finalUri.Scheme}' blocked", PHPExtensionErrorCodes.DownloadFailed);
                }

                if (!response.IsSuccessStatusCode)
                {
                    LogService.Error($"Failed to download extension {extension.Name}: HTTP {response.StatusCode}");
                    return DownloadResult.Fail( $"Failed to download extension: HTTP {response.StatusCode}", PHPExtensionErrorCodes.DownloadFailed);
                }

                var fileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                Debug.WriteLine($"📥 Downloaded {fileBytes.Length} bytes for {extension.Name}");
                
                // Validate hash if provided (security check)
                if (!string.IsNullOrEmpty(extension.Hash) && !string.IsNullOrEmpty(extension.HashAlgorithm))
                {
                    Debug.WriteLine($"🔒 Validating {extension.HashAlgorithm} hash for {extension.Name} (attempt {attempt})");
                    LogService.Info($"Validating {extension.HashAlgorithm} hash for extension {extension.Name} (attempt {attempt})");
                    
                    try
                    {
                        HashAlgorithm hashAlgo;
                        try
                        {
                            hashAlgo = extension.HashAlgorithm.ToUpperInvariant() switch
                            {
                                "SHA256" or "SHA-256" => SHA256.Create(),
                                "SHA384" or "SHA-384" => SHA384.Create(),
                                "SHA512" or "SHA-512" => SHA512.Create(),
                                _ => throw new NotSupportedException($"Weak or unsupported hash algorithm '{extension.HashAlgorithm}'. Use SHA256, SHA384, or SHA512.")
                            };
                        }
                        catch (NotSupportedException)
                        {
                            Debug.WriteLine($"❌ Unsupported hash algorithm: {extension.HashAlgorithm}");
                            LogService.Error($"Unsupported hash algorithm {extension.HashAlgorithm} for extension {extension.Name}");
                            return DownloadResult.Fail( $"Unsupported hash algorithm: {extension.HashAlgorithm}", PHPExtensionErrorCodes.InvalidHashAlgorithm);
                        }
                        
                        using (hashAlgo)
                        {
                            var computedHash = BitConverter.ToString(hashAlgo.ComputeHash(fileBytes)).Replace("-", "").ToLower();
                            var expectedHash = extension.Hash.ToLower();
                            
                            if (computedHash != expectedHash)
                            {
                                Debug.WriteLine($"❌ {extension.HashAlgorithm} hash mismatch for {extension.Name} (attempt {attempt})");
                                Debug.WriteLine($"   Expected: {expectedHash}");
                                Debug.WriteLine($"   Computed: {computedHash}");
                                
                                if (attempt < maxRetries)
                                {
                                    Debug.WriteLine($"🔄 Hash validation failed, retrying download (attempt {attempt + 1}/{maxRetries})");
                                    LogService.Warning($"Hash validation failed for extension {extension.Name} (attempt {attempt}/{maxRetries}), retrying download. Expected: {expectedHash}, Computed: {computedHash}");
                                    
                                    // Use exponential backoff for retry delays (consistent with file retries)
                                    var delay = 1000 * (int)Math.Pow(2, attempt - 1); // 1000, 2000, 4000ms...
                                    Debug.WriteLine($"⏱️ Waiting {delay}ms before retry (exponential backoff)");
                                    LogService.Info($"Using exponential backoff delay: {delay}ms for retry attempt {attempt + 1}");
                                    
                                    await Task.Delay(delay, cancellationToken);
                                    continue; // Retry the download
                                }
                                else
                                {
                                    LogService.Error($"Hash validation failed for extension {extension.Name} after {maxRetries} attempts. Algorithm: {extension.HashAlgorithm}, Expected: {expectedHash}, Computed: {computedHash}");
                                    return DownloadResult.Fail( $"Hash validation failed for {extension.DisplayName} after {maxRetries} attempts", PHPExtensionErrorCodes.HashMismatch);
                                }
                            }
                            
                            Debug.WriteLine($"✅ {extension.HashAlgorithm} hash validation passed for {extension.Name}");
                            LogService.Info($"Hash validation passed for extension {extension.Name} using {extension.HashAlgorithm}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ Hash validation error for {extension.Name}: {ex.Message}");
                        LogService.Error($"Hash validation error for extension {extension.Name}: {ex.Message}");
                        return DownloadResult.Fail( $"Hash validation error: {ex.Message}", PHPExtensionErrorCodes.HashValidationError);
                    }
                }
                else
                {
                    Debug.WriteLine($"⚠️ No hash provided for {extension.Name} - skipping validation");
                    LogService.Warning($"No hash provided for extension {extension.Name} - skipping validation");
                }
                
                // If we reach here, download and validation were successful
                return DownloadResult.Success("Download and verification successful", fileBytes);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"❌ Download cancelled for {extension.Name}");
                LogService.Warning($"Download cancelled for extension {extension.Name}");
                return DownloadResult.Fail( "Download was cancelled", PHPExtensionErrorCodes.Cancelled);
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                Debug.WriteLine($"❌ Download failed (attempt {attempt}/{maxRetries}): {ex.Message}");
                LogService.Warning($"Download failed for extension {extension.Name} (attempt {attempt}/{maxRetries}), retrying: {ex.Message}");
                
                // Use exponential backoff for download retry delays
                var delay = 2000 * (int)Math.Pow(2, attempt - 1); // 2000, 4000, 8000ms...
                Debug.WriteLine($"⏱️ Waiting {delay}ms before download retry (exponential backoff)");
                LogService.Info($"Using exponential backoff delay: {delay}ms for download retry attempt {attempt + 1}");
                
                await Task.Delay(delay, cancellationToken);
                continue;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Exception downloading extension: {ex.Message}");
                LogService.Error($"Failed to download extension {extension.Name} after {maxRetries} attempts: {ex.Message}");
                return DownloadResult.Fail( ex.Message, PHPExtensionErrorCodes.DownloadFailed);
            }
        }
        
        // This should never be reached, but just in case
        LogService.Error($"Unexpected end of download loop for extension {extension.Name}");
        return DownloadResult.Fail( "Download failed unexpectedly", PHPExtensionErrorCodes.DownloadFailed);
    }
    
    /// <summary>
    /// Writes extension file bytes to the specified path with retry logic and backup of existing files
    /// </summary>
    private async Task<OperationResult> WriteExtensionFileAsync(string dllPath, byte[] fileBytes, CancellationToken cancellationToken = default)
    {
        string? backupPath = null;
        try
        {
            Debug.WriteLine($"💾 Writing extension file to: {dllPath}");
            LogService.Info($"Writing extension file to {dllPath}, Size: {fileBytes.Length} bytes");
            
            // Create backup of existing file if it exists
            if (File.Exists(dllPath))
            {
                backupPath = $"{dllPath}.backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                Debug.WriteLine($"📋 Creating backup of existing DLL: {backupPath}");
                LogService.Info($"Creating backup of existing DLL {dllPath} to {backupPath}");
                
                try
                {
                    File.Copy(dllPath, backupPath, overwrite: false);
                }
                catch (Exception backupEx)
                {
                    Debug.WriteLine($"⚠️ Failed to create backup: {backupEx.Message}");
                    LogService.Warning($"Failed to create backup of {dllPath}, proceeding without backup: {backupEx.Message}");
                    backupPath = null; // Clear backup path since backup failed
                }
            }
            
            await ExecuteWithRetryAsync(() => File.WriteAllBytesAsync(dllPath, fileBytes, cancellationToken), cancellationToken: cancellationToken);
            
            // Verify file was created and has correct size
            if (!File.Exists(dllPath))
            {
                Debug.WriteLine($"❌ DLL file was not created: {dllPath}");
                LogService.Error($"DLL file was not created at {dllPath}");
                
                // Attempt to restore backup if write failed
                if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                {
                    try
                    {
                        File.Copy(backupPath, dllPath, overwrite: true);
                        Debug.WriteLine($"🔄 Restored backup after write failure: {backupPath} -> {dllPath}");
                        LogService.Warning($"Restored backup after write failure: {backupPath} -> {dllPath}");
                    }
                    catch (Exception restoreEx)
                    {
                        LogService.Error($"Failed to restore backup after write failure: {restoreEx.Message}");
                    }
                }
                
                return Result.Fail( "DLL file was not created", PHPExtensionErrorCodes.FileNotCreated);
            }
            
            // Verify file size matches expected
            var actualSize = new FileInfo(dllPath).Length;
            if (actualSize != fileBytes.Length)
            {
                Debug.WriteLine($"⚠️ File size mismatch: expected {fileBytes.Length}, actual {actualSize}");
                LogService.Warning($"File size mismatch for {dllPath}: expected {fileBytes.Length}, actual {actualSize}");
                
                // This is a warning, not a failure, as the file might still be valid
            }
            
            // Auto-cleanup old backups in the directory
            var directory = Path.GetDirectoryName(dllPath);
            if (!string.IsNullOrEmpty(directory))
            {
                await AutoCleanupBackupsAsync(directory, cancellationToken);
            }
            
            Debug.WriteLine($"✅ Successfully wrote extension file: {dllPath}");
            return Result.Success( "Extension file written successfully");
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"❌ File write cancelled: {dllPath}");
            LogService.Warning($"File write cancelled for {dllPath}");
            
            // Restore backup if operation was cancelled
            if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
            {
                try
                {
                    File.Copy(backupPath, dllPath, overwrite: true);
                    Debug.WriteLine($"🔄 Restored backup after cancellation: {backupPath} -> {dllPath}");
                    LogService.Info($"Restored backup after cancellation: {backupPath} -> {dllPath}");
                }
                catch (Exception restoreEx)
                {
                    LogService.Error($"Failed to restore backup after cancellation: {restoreEx.Message}");
                }
            }
            
            return Result.Fail( "File write was cancelled", PHPExtensionErrorCodes.Cancelled);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Exception writing extension file: {ex.Message}");
            LogService.Error($"Failed to write extension file to {dllPath}: {ex.Message}");
            
            // Restore backup if write failed due to exception
            if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
            {
                try
                {
                    File.Copy(backupPath, dllPath, overwrite: true);
                    Debug.WriteLine($"🔄 Restored backup after exception: {backupPath} -> {dllPath}");
                    LogService.Warning($"Restored backup after exception: {backupPath} -> {dllPath}");
                }
                catch (Exception restoreEx)
                {
                    LogService.Error($"Failed to restore backup after exception: {restoreEx.Message}");
                }
            }
            
            return Result.Fail( ex.Message, PHPExtensionErrorCodes.InstallFilesFailed);
        }
    }

    private async Task<OperationResult> UpdatePHPConfiguration(ServiceDefinition service, PHPExtension extension, string phpVersion, bool enable, CancellationToken cancellationToken = default)
    {
        await _phpIniLock.WaitAsync(cancellationToken);
        try
        {
            var phpIniPath = await GetPHPIniPathAsync(service.Id, phpVersion);
            if (string.IsNullOrEmpty(phpIniPath) || !File.Exists(phpIniPath))
            {
                LogService.Error($"PHP ini file not found: {phpIniPath}");
                return Result.Fail( $"PHP ini file not found: {phpIniPath}", PHPExtensionErrorCodes.IniFileNotFound);
            }

            // Acquired lock for php.ini modification

            // Create backup before modifying
            // Create or update single backup file
            var backupPath = $"{phpIniPath}.backup";
            await CreateSingleBackupAsync(phpIniPath, backupPath, cancellationToken);
            // Ensured backup exists

            var content = await File.ReadAllTextAsync(phpIniPath, cancellationToken);
            var originalSize = content.Length;
            // Original file size

            // For extremely large files (>10MB), use atomic streaming that modifies file directly
            if (content.Length > 10_485_760)
            {
                // Using atomic streaming for extremely large INI file
                LogService.Info($"Using atomic streaming for extremely large INI file modification");
                
                var directive = extension.IsZendExtension ? "zend_extension" : "extension";
                var extensionValue = extension.DllName ?? extension.Name;
                var operation = enable ? IniOperation.AddOrUpdate : IniOperation.CommentOut;
                
                await ProcessExtremelyLargeIniFileAsync(phpIniPath, directive, extensionValue, operation, cancellationToken);
                
                // Atomic streaming completed
                LogService.Info($"Atomic streaming completed for extension {extension.Name}, Action: {(enable ? "Enable" : "Disable")}");
                return Result.Success("PHP configuration updated successfully using atomic streaming");
            }
            
            // For smaller files, use standard in-memory processing
            if (enable)
            {
                // Add extension configuration
                if (extension.IsZendExtension)
                {
                    content = AddOrUpdatePHPSetting(content, "zend_extension", extension.DllName ?? extension.Name);
                }
                else
                {
                    content = AddOrUpdatePHPSetting(content, "extension", extension.DllName ?? extension.Name);
                }

                // Skip configuration template to keep php.ini clean
                // Extensions will use default settings unless manually configured
                Debug.WriteLine($"ℹ️ Extension {extension.DisplayName} enabled with default settings");
            }
            else
            {
                // Comment out extension
                content = CommentOutPHPSetting(content, extension.IsZendExtension ? "zend_extension" : "extension", extension.DllName ?? extension.Name);
            }

            var newSize = content.Length;
            Debug.WriteLine($"📊 New file size: {newSize} characters");
            
            // Validate size increase is reasonable (prevent corruption)
            if (newSize > originalSize * 2 && originalSize > 1000) // More than double size for files > 1KB
            {
                Debug.WriteLine($"⚠️ File size increased dramatically from {originalSize} to {newSize} characters");
                var increasePercent = Math.Round((double)(newSize - originalSize) / originalSize * 100, 1);
                LogService.Warning($"PHP.ini file size increased dramatically during extension modification. Extension: {extension.DisplayName}, OriginalSize: {originalSize}, NewSize: {newSize}, Increase: {increasePercent}%. Restoring backup: {backupPath}");
                
                // Restore backup
                File.Copy(backupPath, phpIniPath, true);
                Debug.WriteLine($"🔄 Restored backup due to size increase: {backupPath} -> {phpIniPath}");
                LogService.Warning($"PHP.ini file corrupted during extension modification. Extension: {extension.DisplayName}, OriginalSize: {originalSize}, NewSize: {newSize}. Backup restored from {backupPath}");
                return Result.Fail( "PHP configuration file became corrupted, backup restored", PHPExtensionErrorCodes.ConfigCorrupted);
            }

            await ExecuteWithRetryAsync(() => File.WriteAllTextAsync(phpIniPath, content, cancellationToken), cancellationToken: cancellationToken);
            Debug.WriteLine($"✅ Updated PHP configuration for extension {extension.Name}");
            
            // TEMPORARILY DISABLED: Post-write normalization to prevent file corruption
            // TODO: Re-enable after confirming the line doubling issue is resolved  
            if (false && _options.EnablePostWriteNormalization)
            {
                var normalizationResult = await NormalizePhpIniFileAsync(phpIniPath, cancellationToken);
                if (normalizationResult.Success)
                {
                    Debug.WriteLine($"🔧 Post-write normalization completed: {normalizationResult.Message}");
                }
                else
                {
                    Debug.WriteLine($"⚠️ Post-write normalization had issues (non-critical): {normalizationResult.Message}");
                    // Don't fail the main operation for normalization issues
                }
            }
            else
            {
                Debug.WriteLine($"ℹ️ Post-write normalization disabled by configuration");
            }
            
            LogService.Info($"Updated PHP configuration for extension {extension.Name}, Action: {(enable ? "Enable" : "Disable")}");
            return Result.Success( "PHP configuration updated successfully");
        }
        catch (OperationCanceledException)
        {
            // Configuration update cancelled
            return Result.Fail( "Configuration update was cancelled", PHPExtensionErrorCodes.Cancelled);
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to update PHP configuration for extension {extension.Name}: {ex.Message}");
            return Result.Fail( ex.Message, PHPExtensionErrorCodes.ConfigUpdateFailed);
        }
        finally
        {
            _phpIniLock.Release();
            // Released lock for php.ini modification
        }
    }

    private async Task<OperationResult> UpdateExtensionInPHPConfiguration(ServiceDefinition service, PHPExtension extension, string phpVersion, bool enable, CancellationToken cancellationToken = default)
    {
        return await UpdatePHPConfigurationWithSafety(service, extension, phpVersion, enable, cancellationToken);
    }
    
    /// <summary>
    /// Updates PHP configuration with automatic service safety management
    /// </summary>
    private async Task<OperationResult> UpdatePHPConfigurationWithSafety(ServiceDefinition service, PHPExtension extension, string phpVersion, bool enable, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"🛡️ Starting safe PHP configuration update for {extension.Name} (enable: {enable})");
        LogService.Info($"Starting safe PHP configuration update for extension {extension.Name}, Enable: {enable}");
        
        // Create the configuration operation as a local function
        async Task<ServiceOperationResult> ConfigOperation(CancellationToken ct)
        {
            Debug.WriteLine($"⚙️ Executing PHP configuration update for {extension.Name}");
            var result = await UpdatePHPConfiguration(service, extension, phpVersion, enable, ct);
            return new ServiceOperationResult(result.Success, result.Message, result.ErrorCode);
        }
        
        // Execute with service safety
        var safetyResult = await _serviceSafetyManager.ExecuteWithServiceSafetyAsync(
            service.Id, 
            ConfigOperation, 
            requireUserConsent: true, // Always require user consent for extension changes
            cancellationToken);
        
        if (safetyResult.RequiredUserConsent)
        {
            // Return a special result that indicates user consent is needed
            Debug.WriteLine($"🤔 User consent required for service restart");
            return new OperationResult(false, safetyResult.Message, "USER_CONSENT_REQUIRED");
        }
        
        if (!safetyResult.Success)
        {
            Debug.WriteLine($"❌ Safe configuration update failed: {safetyResult.Message}");
            return new OperationResult(false, safetyResult.Message, safetyResult.ErrorCode);
        }
        
        Debug.WriteLine($"✅ Safe PHP configuration update completed successfully");
        return new OperationResult(true, safetyResult.Message);
    }

    private async Task<OperationResult> RemoveFromPHPConfiguration(ServiceDefinition service, PHPExtension extension, string phpVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            Debug.WriteLine($"🗑️ Safely removing extension {extension.Name} from PHP configuration");
            LogService.Info($"Safely removing extension {extension.Name} from PHP configuration");
            
            // Create the removal operation as a local function
            async Task<ServiceOperationResult> RemoveOperation(CancellationToken ct)
            {
                Debug.WriteLine($"⚙️ Executing PHP configuration removal for {extension.Name}");
                
                var phpIniPath = await GetPHPIniPathAsync(service.Id, phpVersion);
                if (string.IsNullOrEmpty(phpIniPath) || !File.Exists(phpIniPath))
                {
                    LogService.Error($"PHP ini file not found for configuration removal: {phpIniPath}");
                    return new ServiceOperationResult(false, $"PHP ini file not found: {phpIniPath}", PHPExtensionErrorCodes.IniFileNotFound);
                }

                var content = await File.ReadAllTextAsync(phpIniPath, cancellationToken);
                
                // Remove extension lines
                var directiveType = extension.IsZendExtension ? "zend_extension" : "extension";
                content = RemovePHPSetting(content, directiveType, extension.DllName ?? extension.Name);

                await File.WriteAllTextAsync(phpIniPath, content, ct);
                Debug.WriteLine($"Removed extension {extension.Name} from PHP configuration");
                
                // TEMPORARILY DISABLED: Post-write normalization to prevent file corruption
                // TODO: Re-enable after confirming the line doubling issue is resolved
                if (false && _options.EnablePostWriteNormalization)
                {
                    var normalizationResult = await NormalizePhpIniFileAsync(phpIniPath, ct);
                    if (normalizationResult.Success)
                    {
                        Debug.WriteLine($"🔧 Post-write normalization completed: {normalizationResult.Message}");
                    }
                    else
                    {
                        Debug.WriteLine($"⚠️ Post-write normalization had issues (non-critical): {normalizationResult.Message}");
                        // Don't fail the main operation for normalization issues
                    }
                }
                else
                {
                    Debug.WriteLine($"ℹ️ Post-write normalization disabled by configuration");
                }
                
                LogService.Info($"Removed extension {extension.Name} from PHP configuration");
                return new ServiceOperationResult(true, "Extension removed from PHP configuration");
            }
            
            // Execute with service safety
            var safetyResult = await _serviceSafetyManager.ExecuteWithServiceSafetyAsync(
                service.Id, 
                RemoveOperation, 
                requireUserConsent: true, // Always require user consent for extension removal
                cancellationToken);
            
            if (safetyResult.RequiredUserConsent)
            {
                // Return a special result that indicates user consent is needed
                Debug.WriteLine($"🤔 User consent required for service restart during removal");
                return new OperationResult(false, safetyResult.Message, "USER_CONSENT_REQUIRED");
            }
            
            if (!safetyResult.Success)
            {
                Debug.WriteLine($"❌ Safe configuration removal failed: {safetyResult.Message}");
                return new OperationResult(false, safetyResult.Message, safetyResult.ErrorCode);
            }
            
            Debug.WriteLine($"✅ Safe PHP configuration removal completed successfully");
            return new OperationResult(true, safetyResult.Message);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"❌ Safe configuration removal cancelled for {extension.Name}");
            return Result.Fail("Configuration removal was cancelled", PHPExtensionErrorCodes.Cancelled);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error during safe extension removal: {ex.Message}");
            LogService.Error($"Error during safe removal of extension {extension.Name}: {ex.Message}");
            return Result.Fail(ex.Message, PHPExtensionErrorCodes.ConfigRemovalFailed);
        }
    }

    private async Task<OperationResult> RemoveExtensionFiles(ServiceDefinition service, PHPExtension extension, string _, CancellationToken cancellationToken = default)
    {
        try
        {
            if (extension.IsBuiltIn) return Result.Success( "Built-in extension, no files to remove"); // Don't remove built-in extension files

            var basePath = service.BinaryPath;
            var phpExe = GetPhpExecutablePath(basePath);
            if (string.IsNullOrEmpty(phpExe))
            {
                LogService.Error($"❌ Could not find PHP executable for file removal");
                LogService.Error($"Could not find PHP executable in {basePath}");
                return Result.Fail( "Could not find PHP executable", PHPExtensionErrorCodes.InvalidPath);
            }
            
            var phpPath = Path.GetDirectoryName(phpExe);
            if (string.IsNullOrEmpty(phpPath))
            {
                LogService.Error($"❌ Could not determine PHP directory from executable: {phpExe}");
                return Result.Fail( "Could not determine PHP directory from executable", PHPExtensionErrorCodes.InvalidPath);
            }
            var extDir = Path.Combine(phpPath, "ext");
            var dllName = extension.DllName ?? $"php_{extension.Name}.dll";
            var dllPath = SecurityValidator.ValidateSafePathWithinRoot(extDir, dllName);

            if (File.Exists(dllPath))
            {
                // Create backup before deletion if enabled
                if (_options.BackupDllsBeforeRemoval)
                {
                    try
                    {
                        var backupPath = $"{dllPath}.removed_{DateTime.Now:yyyyMMdd_HHmmss}";
                        File.Copy(dllPath, backupPath, overwrite: false);
                        Debug.WriteLine($"📋 Created backup before removal: {backupPath}");
                        LogService.Info($"Created backup of DLL before removal: {dllPath} -> {backupPath}");
                    }
                    catch (Exception backupEx)
                    {
                        Debug.WriteLine($"⚠️ Failed to backup DLL before removal: {backupEx.Message}");
                        LogService.Warning($"Failed to backup DLL {dllPath} before removal, proceeding with deletion: {backupEx.Message}");
                        // Continue with deletion even if backup fails
                    }
                }
                
                File.Delete(dllPath);
                Debug.WriteLine($"Deleted extension file: {dllPath}");
                LogService.Info($"Deleted extension file {dllPath} for {extension.Name}");
                
                // Auto-cleanup old backups in the directory
                var directory = Path.GetDirectoryName(dllPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    await AutoCleanupBackupsAsync(directory, cancellationToken);
                }
                
                return Result.Success( "Extension files removed successfully");
            }
            else
            {
                LogService.Info($"Extension files not found for {extension.Name}, nothing to remove");
                return Result.Success( "Extension files not found, nothing to remove");
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to remove extension files for {extension.Name}: {ex.Message}");
            return Result.Fail( ex.Message, PHPExtensionErrorCodes.FileRemovalFailed);
        }
    }

        /// <summary>
        /// Checks if an extension is enabled in php.ini
        /// </summary>
        public async Task<bool> IsExtensionEnabledInPhpIniAsync(long serviceId, string extensionName, string _, CancellationToken cancellationToken = default)
        {
            try
            {
                var phpIniPath = await GetPHPIniPathAsync(serviceId, "");
                if (string.IsNullOrEmpty(phpIniPath) || !File.Exists(phpIniPath))
                {
                    LogService.Warning($"❌ PHP ini file not found: {phpIniPath}");
                    return false;
                }

                var content = await File.ReadAllTextAsync(phpIniPath, cancellationToken);
                LogService.Info($"🔍 Checking php.ini for extension {extensionName} at: {phpIniPath}");
                
                // Split into lines and check each line individually
                var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                LogService.Info($"🔍 php.ini has {lines.Length} lines");
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Skip empty lines and comments
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(';') || trimmedLine.StartsWith('#'))
                        continue;
                    
                    // Check for extension=gd or extension=php_gd.dll (uncommented)
                    var patterns = new[]
                    {
                        $"extension={extensionName}",
                        $"extension=php_{extensionName}.dll",
                        $"zend_extension={extensionName}",
                        $"zend_extension=php_{extensionName}.dll"
                    };

                    foreach (var pattern in patterns)
                    {
                        if (trimmedLine.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                            trimmedLine.StartsWith(pattern + " ", StringComparison.OrdinalIgnoreCase) ||
                            trimmedLine.StartsWith(pattern + "=", StringComparison.OrdinalIgnoreCase))
                        {
                            LogService.Info($"✅ Extension {extensionName} is enabled in php.ini: {trimmedLine}");
                            return true;
                        }
                    }
                }

                LogService.Info($"❌ Extension {extensionName} is NOT enabled in php.ini");
                return false;
            }
            catch (Exception ex)
            {
                LogService.Error($"❌ Error checking php.ini for extension {extensionName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the PHP executable path using the same logic as ServiceControlManager
        /// </summary>
        private static string? GetPhpExecutablePath(string basePath)
        {
            if (string.IsNullOrEmpty(basePath))
            {
                return null;
            }
            
            // For PHP, use php-cgi.exe for web services (same as ServiceControlManager)
            var phpCgiPath = Path.Combine(basePath, "php-cgi.exe");
            if (File.Exists(phpCgiPath))
            {
                return phpCgiPath;
            }

            // Fallback to php.exe
            var phpPath = Path.Combine(basePath, "php.exe");
            return File.Exists(phpPath) ? phpPath : null;
        }


    /// <summary>
    /// Normalizes an INI directive line by removing comments and normalizing whitespace
    /// Uses Span<char> for better performance with large files
    /// </summary>
    private static string NormalizeIniDirective(string line)
    {
        if (string.IsNullOrEmpty(line)) return string.Empty;
        
        var span = line.AsSpan().Trim();
        
        // Remove comment prefix
        if (span.StartsWith(";"))
        {
            span = span[1..].Trim();
        }
        
        // Normalize whitespace around = sign
        var equalIndex = span.IndexOf('=');
        if (equalIndex > 0)
        {
            var key = span[..equalIndex].Trim();
            var value = span[(equalIndex + 1)..].Trim();
            return $"{key.ToString()}={value.ToString()}";
        }
        
        return span.ToString();
    }
    
    /// <summary>
    /// Checks if two extension values match, considering .dll suffix variations
    /// </summary>
    private static bool ExtensionValuesMatch(string value1, string value2)
    {
        if (string.IsNullOrEmpty(value1) || string.IsNullOrEmpty(value2))
            return string.Equals(value1, value2, StringComparison.OrdinalIgnoreCase);
        
        // Direct comparison first
        if (string.Equals(value1, value2, StringComparison.OrdinalIgnoreCase))
            return true;
        
        // Normalize by removing .dll suffix and php_ prefix for comparison
        var normalized1 = NormalizeExtensionValue(value1);
        var normalized2 = NormalizeExtensionValue(value2);
        
        return string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Normalizes extension value by removing common prefixes, suffixes, and handling absolute paths
    /// </summary>
    private static string NormalizeExtensionValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        
        var normalized = value.Trim();
        
        // Handle quoted values (common in INI files)
        if ((normalized.StartsWith('"') && normalized.EndsWith('"')) ||
            (normalized.StartsWith('\'') && normalized.EndsWith('\'')))
        {
            normalized = normalized[1..^1].Trim();
        }
        
        // Handle both absolute and relative paths - extract just the filename
        if (Path.IsPathRooted(normalized) || normalized.Contains('/') || normalized.Contains('\\'))
        {
            try
            {
                // Handle relative paths like "ext/php_mbstring.dll" or "ext\\php_mbstring.dll"
                var fileName = Path.GetFileName(normalized);
                if (!string.IsNullOrEmpty(fileName))
                {
                    normalized = fileName;
                    Debug.WriteLine($"📁 Normalized path to filename: {value} -> {normalized}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Failed to normalize path {normalized}: {ex.Message}");
                // Continue with original value if path parsing fails
            }
        }
        
        // Remove cross-platform library suffixes
        var suffixesToRemove = new[] { ".dll", ".so", ".dylib" };
        foreach (var suffix in suffixesToRemove)
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^suffix.Length];
                break; // Only remove one suffix
            }
        }
        
        // Remove common prefixes
        var prefixesToRemove = new[] { "php_", "zend_", "lib" };
        foreach (var prefix in prefixesToRemove)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..];
                break; // Only remove one prefix
            }
        }
        
        // Always return lowercase for consistent comparisons
        return normalized.ToLowerInvariant();
    }
    
    /// <summary>
    /// Detects the line ending style used in the content
    /// </summary>
    private static string DetectLineEnding(string content)
    {
        if (string.IsNullOrEmpty(content)) return Environment.NewLine;
        
        // Check for CRLF first (most specific)
        if (content.Contains("\r\n")) return "\r\n";
        
        // Check for LF (Unix/Linux)
        if (content.Contains('\n')) return "\n";
        
        // Check for CR (old Mac)
        if (content.Contains('\r')) return "\r";
        
        // Default to system line ending
        return Environment.NewLine;
    }
    
    /// <summary>
    /// Creates or updates a single backup file, replacing any existing backup
    /// This prevents backup file accumulation while ensuring safety
    /// </summary>
    private static async Task CreateSingleBackupAsync(string originalPath, string backupPath, CancellationToken cancellationToken = default)
    {
        try
        {
            bool shouldBackup = true;

            if (File.Exists(backupPath))
            {
                var originalModified = File.GetLastWriteTime(originalPath);
                var backupModified = File.GetLastWriteTime(backupPath);

                shouldBackup = originalModified > backupModified;

                if (!shouldBackup)
                {
                    Debug.WriteLine($"📋 Backup is current, skipping: {backupPath}");
                    return;
                }
            }

            // Perform async copy with cancellation support
            await using var sourceStream = File.OpenRead(originalPath);
            await using var destStream = File.Create(backupPath);
            await sourceStream.CopyToAsync(destStream, cancellationToken);

            Debug.WriteLine($"📋 Updated single backup asynchronously: {backupPath}");
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"📋 Backup operation cancelled: {backupPath}");
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Failed to create backup (non-critical): {ex.Message}");
        }
    }
    
    /// <summary>
    /// SAFE line ending normalization that ONLY removes excessive blank lines (3+ consecutive)
    /// This method is guaranteed to never add lines, only remove excess
    /// </summary>
    private static string NormalizeLineEndings(string content, string targetLineEnding)
    {
        if (string.IsNullOrEmpty(content)) return content;
        
        Debug.WriteLine($"🔧 SAFE normalization: removing ONLY 3+ consecutive blank lines, preserving {(targetLineEnding == "\r\n" ? "CRLF" : targetLineEnding == "\n" ? "LF" : "CR")} format");
        
        var originalLength = content.Length;
        var originalLines = content.Split([targetLineEnding], StringSplitOptions.None).Length;
        
        // CRITICAL: Use the exact detected line ending, don't reconstruct it
        var result = content;
        var removedCount = 0;
        
        // Only target 3+ consecutive line endings - this is SAFE
        var tripleBreak = targetLineEnding + targetLineEnding + targetLineEnding;
        var doubleBreak = targetLineEnding + targetLineEnding;
        
        // Keep removing 3+ consecutive breaks until none remain
        while (result.Contains(tripleBreak))
        {
            result = result.Replace(tripleBreak, doubleBreak);
            removedCount++;
        }
        
        var finalLength = result.Length;
        var finalLines = result.Split([targetLineEnding], StringSplitOptions.None).Length;
        
        Debug.WriteLine($"📊 SAFE normalization stats:");
        Debug.WriteLine($"   Original: {originalLength} chars, {originalLines} lines");
        Debug.WriteLine($"   Final: {finalLength} chars, {finalLines} lines");
        Debug.WriteLine($"   Removed: {originalLength - finalLength} chars, {originalLines - finalLines} lines");
        Debug.WriteLine($"   Applied {removedCount} fixes (guaranteed safe)");
        
        // SAFETY CHECK: Ensure we never increased the file size
        if (finalLength > originalLength)
        {
            Debug.WriteLine($"⚠️ CRITICAL ERROR: Normalization INCREASED file size! Returning original content.");
            return content; // Return original if something went wrong
        }
        
        return result;
    }
    
    
    /// <summary>
    /// Performs post-write normalization on a php.ini file to ensure clean formatting
    /// </summary>
    private static async Task<OperationResult> NormalizePhpIniFileAsync(string phpIniPath, CancellationToken cancellationToken = default)
    {
        try
        {
            Debug.WriteLine($"🔧 Starting post-write normalization for {phpIniPath}");
            LogService.Info($"Starting post-write normalization for php.ini file: {phpIniPath}");
            
            if (!File.Exists(phpIniPath))
            {
                Debug.WriteLine($"❌ PHP ini file not found for normalization: {phpIniPath}");
                return Result.Fail($"PHP ini file not found: {phpIniPath}", PHPExtensionErrorCodes.IniFileNotFound);
            }
            
            // Read the current content
            var content = await File.ReadAllTextAsync(phpIniPath, cancellationToken);
            var originalSize = content.Length;
            
            // Detect the line ending format
            var lineEnding = DetectLineEnding(content);
            
            // Normalize line endings
            var normalizedContent = NormalizeLineEndings(content, lineEnding);
            var normalizedSize = normalizedContent.Length;
            
            // Only write back if there were changes
            if (normalizedContent != content)
            {
                Debug.WriteLine($"📝 Writing normalized content ({originalSize} → {normalizedSize} chars, saved {originalSize - normalizedSize} chars)");
                await File.WriteAllTextAsync(phpIniPath, normalizedContent, cancellationToken);
                
                LogService.Info($"Post-write normalization completed: {originalSize} → {normalizedSize} characters");
                return Result.Success($"PHP ini file normalized: reduced from {originalSize} to {normalizedSize} characters");
            }
            else
            {
                Debug.WriteLine($"✅ File already properly normalized, no changes needed");
                return Result.Success("PHP ini file was already properly normalized");
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"❌ PHP ini normalization cancelled");
            return Result.Fail("PHP ini normalization was cancelled", PHPExtensionErrorCodes.Cancelled);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error during PHP ini normalization: {ex.Message}");
            LogService.Error($"Error during post-write normalization of php.ini file: {phpIniPath}: {ex.Message}");
            return Result.Fail($"PHP ini normalization failed: {ex.Message}", PHPExtensionErrorCodes.ConfigUpdateFailed);
        }
    }
    
    /// <summary>
    /// Optimized INI file processing for large files using memory-efficient operations while preserving line endings
    /// </summary>
    private static string AddOrUpdatePHPSetting(string content, string directive, string value)
    {
        // Detect and preserve original line endings
        var lineEnding = DetectLineEnding(content);
        Debug.WriteLine($"📝 Detected line ending: {(lineEnding == "\r\n" ? "CRLF" : lineEnding == "\n" ? "LF" : lineEnding == "\r" ? "CR" : "System")} for directive {directive}");
        
        // For very large files (>1MB), use streaming approach to avoid memory issues
        if (content.Length > 1_048_576) // 1MB threshold
        {
            Debug.WriteLine($"🚀 Large INI file detected ({content.Length:N0} chars), using streaming approach");
            LogService.Info($"Processing large INI file ({content.Length:N0} characters) with streaming approach");
            return ProcessLargeIniFileStreaming(content, directive, value, IniOperation.AddOrUpdate);
        }
        
        // CRITICAL FIX: Split by detected line ending to prevent line doubling
        var lines = content.Split([lineEnding], StringSplitOptions.None).ToList();
        var newLine = $"{directive}={value}";
        
        Debug.WriteLine($"🔍 AddOrUpdatePHPSetting: directive={directive}, value={value}");
        
        // First check if the exact setting already exists (active or commented)
        for (int i = 0; i < lines.Count; i++)
        {
            var normalizedLine = NormalizeIniDirective(lines[i]);
            
            // Extract directive and value from normalized line
            var equalIndex = normalizedLine.IndexOf('=');
            if (equalIndex > 0)
            {
                var lineDirective = normalizedLine[..equalIndex];
                var lineValue = normalizedLine[(equalIndex + 1)..];
                
                // Check if directive matches and values match (considering .dll variations)
                if (string.Equals(lineDirective, directive, StringComparison.OrdinalIgnoreCase) &&
                    ExtensionValuesMatch(lineValue, value))
                {
                    Debug.WriteLine($"✅ Found matching directive at line {i}: {lines[i]} (normalized: {normalizedLine})");
                    if (lines[i].AsSpan().TrimStart().StartsWith(";"))
                    {
                        // Uncomment the existing line using Span for efficiency
                        var lineSpan = lines[i].AsSpan().TrimStart();
                        lines[i] = lineSpan[1..].TrimStart().ToString();
                        Debug.WriteLine($"🔄 Uncommented line: {lines[i]}");
                    }
                    return string.Join(lineEnding, lines);
                }
            }
        }
        
        // Check if there's a different value for the same directive and replace it
        bool found = false;
        for (int i = 0; i < lines.Count; i++)
        {
            var normalizedLine = NormalizeIniDirective(lines[i]);
            
            // Check if line starts with our directive followed by = (normalized)
            if (normalizedLine.StartsWith($"{directive}=", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"🔄 Replacing existing directive at line {i}: {lines[i]} -> {newLine}");
                lines[i] = newLine;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.WriteLine($"➕ Adding new line: {newLine}");
            lines.Add(newLine);
        }

        return string.Join(lineEnding, lines);
    }

    private static string CommentOutPHPSetting(string content, string directive, string value)
    {
        // Detect and preserve original line endings
        var lineEnding = DetectLineEnding(content);
        Debug.WriteLine($"📝 Detected line ending: {(lineEnding == "\r\n" ? "CRLF" : lineEnding == "\n" ? "LF" : lineEnding == "\r" ? "CR" : "System")} for commenting out {directive}");
        
        // For very large files (>1MB), use streaming approach
        if (content.Length > 1_048_576)
        {
            Debug.WriteLine($"🚀 Large INI file detected for commenting, using streaming approach");
            return ProcessLargeIniFileStreaming(content, directive, value, IniOperation.CommentOut);
        }
        
        // CRITICAL FIX: Split by detected line ending to prevent line doubling
        var lines = content.Split([lineEnding], StringSplitOptions.None).ToList();
        
        Debug.WriteLine($"🔍 CommentOutPHPSetting: directive={directive}, value={value}");
        
        bool found = false;
        for (int i = 0; i < lines.Count; i++)
        {
            var normalizedLine = NormalizeIniDirective(lines[i]);
            
            // Extract directive and value from normalized line
            var equalIndex = normalizedLine.IndexOf('=');
            if (equalIndex > 0)
            {
                var lineDirective = normalizedLine[..equalIndex];
                var lineValue = normalizedLine[(equalIndex + 1)..];
                
                // Check if directive matches and values match (considering .dll variations)
                if (string.Equals(lineDirective, directive, StringComparison.OrdinalIgnoreCase) &&
                    ExtensionValuesMatch(lineValue, value))
                {
                    Debug.WriteLine($"✅ Found matching directive at line {i}: {lines[i]} (normalized: {normalizedLine})");
                    if (!lines[i].AsSpan().TrimStart().StartsWith(";"))
                    {
                        lines[i] = ";" + lines[i];
                        Debug.WriteLine($"🔄 Commented out line: {lines[i]}");
                        found = true;
                    }
                    else
                    {
                        Debug.WriteLine($"ℹ️ Line already commented: {lines[i]}");
                        found = true;
                    }
                    break;
                }
            }
        }
        
        if (!found)
        {
            Debug.WriteLine($"⚠️ No matching line found for {directive}={value}");
        }

        return string.Join(lineEnding, lines);
    }

    private static string RemovePHPSetting(string content, string directive, string value)
    {
        // Detect and preserve original line endings
        var lineEnding = DetectLineEnding(content);
        Debug.WriteLine($"📝 Detected line ending: {(lineEnding == "\r\n" ? "CRLF" : lineEnding == "\n" ? "LF" : lineEnding == "\r" ? "CR" : "System")} for removing {directive}");
        
        // For very large files (>1MB), use streaming approach
        if (content.Length > 1_048_576)
        {
            Debug.WriteLine($"🚀 Large INI file detected for removal, using streaming approach");
            return ProcessLargeIniFileStreaming(content, directive, value, IniOperation.Remove);
        }
        
        // CRITICAL FIX: Split by detected line ending to prevent line doubling
        var lines = content.Split([lineEnding], StringSplitOptions.None).ToList();
        var normalizedTarget = $"{directive}={value}";
        
        // Remove lines that match our directive and value (normalized)
        lines.RemoveAll(line => 
        {
            var normalizedLine = NormalizeIniDirective(line);
            
            var equalIndex = normalizedLine.IndexOf('=');
            if (equalIndex > 0)
            {
                var lineDirective = normalizedLine[..equalIndex];
                var lineValue = normalizedLine[(equalIndex + 1)..];
                
                // Check if directive matches and values match (considering .dll variations)
                return string.Equals(lineDirective, directive, StringComparison.OrdinalIgnoreCase) &&
                       ExtensionValuesMatch(lineValue, value);
            }
            
            return false;
        });

        return string.Join(lineEnding, lines);
    }

    /// <summary>
    /// Scans a PHP installation to detect installed extensions
    /// </summary>
    public async Task<List<string>> ScanInstalledExtensionsAsync(long serviceId, string phpVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
            if (service == null) 
            {
                LogService.Error($"❌ Service {serviceId} not found");
                return [];
            }

            var basePath = service.BinaryPath;
            var phpExe = GetPhpExecutablePath(basePath);
            if (string.IsNullOrEmpty(phpExe))
            {
                LogService.Error($"❌ Could not find PHP executable in base path: '{basePath}'");
                return [];
            }

            LogService.Info($"🔍 Using PHP executable: {phpExe}");
            LogService.Info($"🔍 Service base path: {basePath}");
            LogService.Info($"🔍 Service binary path: {service.BinaryPath}");

            // Run php -m to get loaded modules using IProcessRunner
            LogService.Info($"🚀 Running: {phpExe} -m");
            LogService.Info($"Executing PHP command to scan extensions: {phpExe} -m");
            
            var phpPath = Path.GetDirectoryName(phpExe);
            var result = await _processRunner.ExecuteAsync(
                fileName: phpExe,
                arguments: "-m",
                workingDirectory: phpPath,
                timeoutMs: 30000,
                cancellationToken: cancellationToken);
            
            if (result.WasCancelled)
            {
                Debug.WriteLine($"❌ PHP process scan was cancelled");
                LogService.Warning($"PHP extension scan was cancelled for service {serviceId}");
                throw new OperationCanceledException();
            }
            
            if (result.TimedOut)
            {
                Debug.WriteLine($"⏱️ PHP process timed out after 30 seconds");
                LogService.Warning($"PHP extension scan timed out for service {serviceId}");
                return [];
            }

            LogService.Info($"📊 PHP -m exit code: {result.ExitCode}");
            LogService.Info($"📊 PHP -m output length: {result.Output.Length}");
            
            // Log stderr output prominently if present
            if (!string.IsNullOrEmpty(result.Error))
            {
                LogService.Warning($"⚠️ PHP -m stderr: {result.Error}");
                
                // Determine log level based on exit code
                if (result.ExitCode == 0)
                {
                    LogService.Warning($"PHP extension scan completed with warnings. Stderr: {result.Error}");
                }
                else
                {
                    LogService.Error($"PHP extension scan failed. Exit code: {result.ExitCode}, Stderr: {result.Error}");
                }
            }

            if (result.ExitCode == 0)
            {
                var extensions = result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                             .Where(line => !string.IsNullOrWhiteSpace(line) && 
                                           !line.StartsWith('[') && 
                                           !line.Contains("PHP Modules") &&
                                           !line.Contains("Zend Modules") &&
                                           !line.Contains('<') && // Filter out HTML tags
                                           !line.Contains("Warning:") &&
                                           !line.Contains("Error:") &&
                                           !line.Contains("Module compiled with") &&
                                           !line.Contains("PHP compiled with") &&
                                           !line.Contains("These options need to match") &&
                                           !line.Contains("in Unknown on line") &&
                                           !line.Contains("Unable to initialize module"))
                             .Select(line => line.Trim())
                             .ToList();
                
                LogService.Info($"📦 Found {extensions.Count} loaded extensions:");
                var stderrInfo = !string.IsNullOrEmpty(result.Error) ? $"Stderr: {result.Error}" : "No errors";
                LogService.Info($"Found {extensions.Count} loaded extensions for PHP {phpVersion}. {stderrInfo}");
                
                foreach (var ext in extensions)
                {
                    LogService.Info($"  - {ext}");
                }
                
                // Perform compatibility analysis if there were warnings
                if (!string.IsNullOrEmpty(result.Error))
                {
                    await AnalyzeExtensionCompatibilityAsync(serviceId, phpVersion, result.Error, cancellationToken);
                }
                
                return extensions;
            }
            else
            {
                LogService.Error($"❌ PHP -m failed with exit code {result.ExitCode}");
                LogService.Error($"PHP extension scan failed. Exit code: {result.ExitCode}, Stdout: {result.Output}, Stderr: {result.Error}");
            }
        }
        catch (OperationCanceledException)
        {
            LogService.Warning($"❌ Extension scanning cancelled for service {serviceId}");
            throw;
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Exception scanning extensions: {ex.Message}");
            LogService.Error($"Failed to scan installed extensions for service {serviceId}: {ex.Message}");
        }

        return [];
    }

    /// <summary>
    /// Analyzes extension compatibility issues and provides recommendations
    /// </summary>
    private async Task AnalyzeExtensionCompatibilityAsync(long serviceId, string phpVersion, string errorOutput, CancellationToken cancellationToken = default)
    {
        try
        {
            LogService.Info($"🔍 Analyzing extension compatibility issues...");
            
            var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
            if (service == null) return;

            var basePath = service.BinaryPath;
            var phpExe = GetPhpExecutablePath(basePath);
            if (string.IsNullOrEmpty(phpExe)) return;

            var phpPath = Path.GetDirectoryName(phpExe);
            var extDir = Path.Combine(phpPath, "ext");
            
            if (!Directory.Exists(extDir)) return;

            // Extract incompatible extensions from error output
            var incompatibleExtensions = new List<string>();
            var lines = errorOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                if (line.Contains("Unable to initialize module") && line.Contains("Module compiled with module API"))
                {
                    // Extract extension name from error message
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"Unable to initialize module '(\w+)'");
                    if (match.Success)
                    {
                        incompatibleExtensions.Add(match.Groups[1].Value);
                    }
                }
            }

            if (incompatibleExtensions.Count > 0)
            {
                LogService.Warning($"🔧 Found {incompatibleExtensions.Count} incompatible extensions:");
                foreach (var ext in incompatibleExtensions)
                {
                    LogService.Warning($"   - {ext}: API version mismatch");
                    
                    // Check if extension DLL exists and suggest action
                    var dllPath = Path.Combine(extDir, $"php_{ext}.dll");
                    if (File.Exists(dllPath))
                    {
                        LogService.Warning($"     DLL exists at: {dllPath}");
                        LogService.Warning($"     Recommendation: Disable this extension or update to compatible version");
                    }
                }
                
                LogService.Warning($"💡 To fix these issues:");
                LogService.Warning($"   1. Use ModStack GUI to disable incompatible extensions");
                LogService.Warning($"   2. Download compatible extension versions");
                LogService.Warning($"   3. Or reinstall PHP with matching extension versions");
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Extension compatibility analysis failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Synchronizes database with actual PHP installation
    /// </summary>
    public async Task<OperationResult> SynchronizeExtensionsAsync(long serviceId, string phpVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            var loadedExtensions = await ScanInstalledExtensionsAsync(serviceId, phpVersion, cancellationToken);
            var availableExtensions = await _extensionRepository.GetExtensionsForPHPVersionAsync(phpVersion);
            
            int syncedCount = 0;
            
            // First sync loaded extensions (from php -m)
            LogService.Info($"🔍 Syncing {loadedExtensions.Count} loaded extensions from php -m");
            foreach (var extName in loadedExtensions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var extension = availableExtensions.FirstOrDefault(e => 
                {
                    // Direct name comparison
                    if (e.Name.Equals(extName, StringComparison.OrdinalIgnoreCase))
                        return true;
                    
                    // Normalized value comparison to prevent false positives
                    if (!string.IsNullOrEmpty(e.DllName))
                    {
                        var normalizedDllName = NormalizeExtensionValue(e.DllName);
                        var normalizedExtName = NormalizeExtensionValue(extName);
                        return normalizedDllName.Equals(normalizedExtName, StringComparison.OrdinalIgnoreCase);
                    }
                    
                    return false;
                });
                
                if (extension != null)
                {
                    LogService.Info($"🔍 Found extension match for '{extName}' -> {extension.Name} (ID: {extension.Id})");
                    var isInstalled = await _extensionRepository.IsExtensionInstalledAsync(serviceId, extension.Id);
                    LogService.Info($"🔍 Extension {extension.Name} already in database: {isInstalled}");
                    
                    if (!isInstalled)
                    {
                        // Add to database as installed
                        var request = new InstallPHPExtensionRequest
                        {
                            ServiceId = serviceId,
                            ExtensionId = extension.Id,
                            PHPVersion = phpVersion,
                            EnableAfterInstall = true
                        };
                        
                        await _extensionRepository.InstallExtensionAsync(request);
                        syncedCount++;
                        LogService.Info($"✅ Synchronized loaded extension {extension.Name} for PHP {phpVersion}");
                    }
                    else
                    {
                        LogService.Info($"ℹ️ Extension {extension.Name} already synchronized");
                    }
                }
                else
                {
                    LogService.Warning($"⚠️ No extension match found for '{extName}'");
                }
            }
            
            // Then check for built-in extensions that have DLL files but aren't loaded yet
            var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
            if (service != null)
            {
                var basePath = service.BinaryPath;
                var phpExe = GetPhpExecutablePath(basePath);
                if (!string.IsNullOrEmpty(phpExe))
                {
                    var phpPath = Path.GetDirectoryName(phpExe);
                    if (!string.IsNullOrEmpty(phpPath))
                    {
                        var extDir = Path.Combine(phpPath, "ext");
                        if (Directory.Exists(extDir))
                        {
                            var builtInExtensions = availableExtensions.Where(e => e.IsBuiltIn).ToList();
                            LogService.Info($"🔍 Found {builtInExtensions.Count} built-in extensions to check");
                            
                            foreach (var extension in builtInExtensions)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                if (!string.IsNullOrEmpty(extension.DllName))
                                {
                                    var dllPath = Path.Combine(extDir, extension.DllName);
                                    var dllExists = File.Exists(dllPath);
                                    
                                    LogService.Info($"🔍 Built-in extension {extension.Name}: DLL exists at {dllPath}: {dllExists}");
                                    
                                    if (dllExists)
                                    {
                                        var isInstalled = await _extensionRepository.IsExtensionInstalledAsync(serviceId, extension.Id);
                                        LogService.Info($"🔍 Built-in extension {extension.Name} already in database: {isInstalled}");
                                        
                                        if (!isInstalled)
                                        {
                                            // Add to database as installed but not enabled
                                            var request = new InstallPHPExtensionRequest
                                            {
                                                ServiceId = serviceId,
                                                ExtensionId = extension.Id,
                                                PHPVersion = phpVersion,
                                                EnableAfterInstall = false // Don't enable automatically for built-in extensions
                                            };
                                            
                                            await _extensionRepository.InstallExtensionAsync(request);
                                            syncedCount++;
                                            LogService.Info($"✅ Synchronized built-in extension {extension.Name} for PHP {phpVersion}");
                                        }
                                        else
                                        {
                                            LogService.Info($"ℹ️ Built-in extension {extension.Name} already synchronized");
                                            
                                            // Check if it's enabled in php.ini
                                            var isEnabledInIni = await IsExtensionEnabledInPhpIniAsync(serviceId, extension.Name, phpVersion, cancellationToken);
                                            LogService.Info($"🔍 Built-in extension {extension.Name} enabled in php.ini: {isEnabledInIni}");
                                        }
                                    }
                                    else
                                    {
                                        LogService.Warning($"⚠️ Built-in extension {extension.Name} DLL not found: {dllPath}");
                                    }
                                }
                                else
                                {
                                    LogService.Warning($"⚠️ Built-in extension {extension.Name} has no DLL name");
                                }
                            }
                        }
                        else
                        {
                            LogService.Error($"❌ Extension directory does not exist: {extDir}");
                        }
                    }
                }
            }
            
            // Now check for extensions in database that are no longer loaded/installed
            var installedInDatabase = await _extensionRepository.GetInstalledExtensionsAsync(serviceId);
            int removedCount = 0;
            
            foreach (var installedExt in installedInDatabase)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var extName = installedExt.Extension?.Name ?? "";
                var isStillLoaded = loadedExtensions.Any(loaded => 
                    loaded.Equals(extName, StringComparison.OrdinalIgnoreCase));
                
                if (!isStillLoaded)
                {
                    // Check if DLL file exists
                    var serviceForCheck = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
                    if (serviceForCheck != null && installedExt.Extension != null)
                    {
                        var basePath = serviceForCheck.BinaryPath;
                        var phpExe = GetPhpExecutablePath(basePath);
                        if (string.IsNullOrEmpty(phpExe))
                        {
                            LogService.Error($"❌ Could not find PHP executable for synchronization");
                            continue;
                        }
                        var phpPath = Path.GetDirectoryName(phpExe);
                        if (string.IsNullOrEmpty(phpPath))
                        {
                            LogService.Error($"❌ Could not determine PHP directory from executable: {phpExe}");
                            continue;
                        }
                        var extPath = Path.Combine(phpPath, "ext");
                        var dllName = installedExt.Extension.DllName ?? $"{installedExt.Extension.Name}.dll";
                        var dllPath = Path.Combine(extPath, dllName);
                        
                        var dllExists = File.Exists(dllPath);
                        
                        bool shouldRemove = false;
                        
                        if (_options.StrictSynchronizationRemoval)
                        {
                            // Strict mode: Remove if not loaded, regardless of DLL existence
                            shouldRemove = true;
                        }
                        else
                        {
                            // Default mode: Only remove if both not loaded AND DLL missing
                            shouldRemove = !dllExists;
                        }
                        
                        if (shouldRemove)
                        {
                            // Remove from database
                            await _extensionRepository.UninstallExtensionAsync(serviceId, installedExt.ExtensionId);
                            removedCount++;
                        }
                    }
                }
            }
            
            LogService.Info($"Extension synchronization complete for service {serviceId}: {syncedCount} extensions added, {removedCount} extensions removed");
            return Result.Success( $"Synchronization complete: {syncedCount} extensions added, {removedCount} extensions removed");
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Synchronization cancelled for service {serviceId}");
            return Result.Fail( "Synchronization was cancelled", PHPExtensionErrorCodes.Cancelled);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Synchronization failed: {ex.Message}");
            LogService.Error($"Failed to synchronize extensions for service {serviceId}: {ex.Message}");
            return Result.Fail( ex.Message, PHPExtensionErrorCodes.SyncFailed);
        }
    }

    #endregion
    
    #region Large File Streaming
    
    /// <summary>
    /// Operations that can be performed on INI files
    /// </summary>
    private enum IniOperation
    {
        AddOrUpdate,
        CommentOut,
        Remove
    }
    
    /// <summary>
    /// Processes large INI files using streaming to avoid memory issues
    /// For extremely large files (>10MB), uses direct file streaming
    /// </summary>
    private static string ProcessLargeIniFileStreaming(string content, string directive, string value, IniOperation operation)
    {
        // Detect and preserve original line endings
        var lineEnding = DetectLineEnding(content);
        Debug.WriteLine($"📝 Detected line ending: {(lineEnding == "\r\n" ? "CRLF" : lineEnding == "\n" ? "LF" : lineEnding == "\r" ? "CR" : "System")} for streaming {operation} operation");
        
        // For extremely large files (>10MB), use direct file streaming to minimize memory usage
        if (content.Length > 10_485_760) // 10MB threshold
        {
            Debug.WriteLine($"🌊 Extremely large INI file detected ({content.Length:N0} chars), using direct file streaming");
            LogService.Info($"Processing extremely large INI file ({content.Length:N0} characters) with direct file streaming");
            // For large files that aren't being written to disk, we still need the old approach
            // This is for in-memory content processing, not direct file modification
            return ProcessLargeIniFileStreaming(content, directive, value, operation);
        }
        
        var newLine = $"{directive}={value}";
        var result = new List<string>();
        var found = false;
        var lineNumber = 0;
        
        Debug.WriteLine($"🌊 In-memory streaming processing for {operation} operation: {directive}={value}");
        
        using var reader = new StringReader(content);
        string? line;
        
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            var normalizedLine = NormalizeIniDirective(line);
            var equalIndex = normalizedLine.IndexOf('=');
            
            if (equalIndex > 0)
            {
                var lineDirective = normalizedLine[..equalIndex];
                var lineValue = normalizedLine[(equalIndex + 1)..];
                
                // Check if this line matches our target
                var isMatch = string.Equals(lineDirective, directive, StringComparison.OrdinalIgnoreCase) &&
                             ExtensionValuesMatch(lineValue, value);
                
                if (isMatch && !found)
                {
                    Debug.WriteLine($"✅ Streaming: Found match at line {lineNumber}: {line}");
                    
                    switch (operation)
                    {
                        case IniOperation.AddOrUpdate:
                            if (line.AsSpan().TrimStart().StartsWith(";"))
                            {
                                // Uncomment existing line
                                var lineSpan = line.AsSpan().TrimStart();
                                result.Add(lineSpan[1..].TrimStart().ToString());
                                Debug.WriteLine($"🔄 Streaming: Uncommented line");
                            }
                            else
                            {
                                result.Add(line); // Keep as-is
                            }
                            break;
                        case IniOperation.CommentOut:
                            if (!line.AsSpan().TrimStart().StartsWith(";"))
                            {
                                result.Add(";" + line);
                                Debug.WriteLine($"🔄 Streaming: Commented out line");
                            }
                            else
                            {
                                result.Add(line); // Already commented
                            }
                            break;
                        case IniOperation.Remove:
                            // Skip this line (don't add to result)
                            Debug.WriteLine($"🗑️ Streaming: Removed line");
                            continue;
                    }
                    found = true;
                }
                else if (operation == IniOperation.AddOrUpdate && 
                        string.Equals(lineDirective, directive, StringComparison.OrdinalIgnoreCase) && 
                        !found)
                {
                    // Replace with new value
                    Debug.WriteLine($"🔄 Streaming: Replacing directive at line {lineNumber}");
                    result.Add(newLine);
                    found = true;
                }
                else
                {
                    result.Add(line); // Keep unchanged
                }
            }
            else
            {
                result.Add(line); // Keep non-directive lines
            }
        }
        
        // Add new line if not found (only for AddOrUpdate)
        if (!found && operation == IniOperation.AddOrUpdate)
        {
            Debug.WriteLine($"➕ Streaming: Adding new line: {newLine}");
            result.Add(newLine);
        }
        
        Debug.WriteLine($"✅ Streaming processing complete: processed {lineNumber} lines");
        return string.Join(lineEnding, result);
    }
    
    /// <summary>
    /// Processes extremely large INI files (>10MB) using fully atomic streaming to minimize memory usage
    /// Writes directly to target file with backup and atomic replacement
    /// </summary>
    private static async Task ProcessExtremelyLargeIniFileAsync(string originalFilePath, string directive, string value, IniOperation operation, CancellationToken cancellationToken = default)
    {
        var backupPath = $"{originalFilePath}.atomic_backup_{DateTime.Now:yyyyMMdd_HHmmss}";
        var tempPath = $"{originalFilePath}.atomic_temp_{DateTime.Now:yyyyMMdd_HHmmss}";
        
        try
        {
            // Create backup of original file
            File.Copy(originalFilePath, backupPath, overwrite: false);
            Debug.WriteLine($"📋 Created atomic backup: {backupPath}");
            LogService.Info($"Created atomic backup for large INI file processing: {backupPath}");
            
            var newLine = $"{directive}={value}";
            var found = false;
            var lineNumber = 0;
            
            Debug.WriteLine($"🌊 Atomic file streaming for {operation}: {directive}={value}");
            LogService.Info($"Using atomic file streaming for extremely large INI processing: {operation}");
            
            // Process file line by line with atomic write
            using (var reader = new StreamReader(originalFilePath))
            using (var writer = new StreamWriter(tempPath))
            {
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lineNumber++;
                    
                    // Report progress for very large files
                    if (lineNumber % 10000 == 0)
                    {
                        Debug.WriteLine($"📊 Processed {lineNumber:N0} lines...");
                    }
                    
                    var normalizedLine = NormalizeIniDirective(line);
                    var equalIndex = normalizedLine.IndexOf('=');
                    
                    if (equalIndex > 0)
                    {
                        var lineDirective = normalizedLine[..equalIndex];
                        var lineValue = normalizedLine[(equalIndex + 1)..];
                        
                        var isMatch = string.Equals(lineDirective, directive, StringComparison.OrdinalIgnoreCase) &&
                                     ExtensionValuesMatch(lineValue, value);
                        
                        if (isMatch && !found)
                        {
                            Debug.WriteLine($"✅ Atomic streaming: Found match at line {lineNumber}");
                            
                            switch (operation)
                            {
                                case IniOperation.AddOrUpdate:
                                    if (line.AsSpan().TrimStart().StartsWith(";"))
                                    {
                                        var lineSpan = line.AsSpan().TrimStart();
                                        await writer.WriteLineAsync(lineSpan[1..].TrimStart().ToString());
                                    }
                                    else
                                    {
                                        await writer.WriteLineAsync(line);
                                    }
                                    break;
                                case IniOperation.CommentOut:
                                    if (!line.AsSpan().TrimStart().StartsWith(";"))
                                    {
                                        await writer.WriteLineAsync(";" + line);
                                    }
                                    else
                                    {
                                        await writer.WriteLineAsync(line);
                                    }
                                    break;
                                case IniOperation.Remove:
                                    // Skip this line
                                    continue;
                            }
                            found = true;
                        }
                        else if (operation == IniOperation.AddOrUpdate && 
                                string.Equals(lineDirective, directive, StringComparison.OrdinalIgnoreCase) && 
                                !found)
                        {
                            await writer.WriteLineAsync(newLine);
                            found = true;
                        }
                        else
                        {
                            await writer.WriteLineAsync(line);
                        }
                    }
                    else
                    {
                        await writer.WriteLineAsync(line);
                    }
                }
                
                // Add new line if not found (only for AddOrUpdate)
                if (!found && operation == IniOperation.AddOrUpdate)
                {
                    await writer.WriteLineAsync(newLine);
                }
            }
            
            // Atomically replace original file with processed version
            File.Move(tempPath, originalFilePath, overwrite: true);
            Debug.WriteLine($"✅ Atomic file streaming complete: processed {lineNumber:N0} lines, file replaced atomically");
            LogService.Info($"Atomic file streaming completed: processed {lineNumber:N0} lines");
            
            // Clean up backup after successful operation
            try
            {
                File.Delete(backupPath);
                Debug.WriteLine($"🗑️ Cleaned up atomic backup after successful operation");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Failed to clean up atomic backup: {ex.Message}");
                LogService.Warning($"Failed to clean up atomic backup {backupPath}: {ex.Message}");
            }
            
            // Direct file modification completed, no return value needed
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"❌ Atomic streaming cancelled, restoring backup");
            
            // Restore backup on cancellation
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Move(backupPath, originalFilePath, overwrite: true);
                    Debug.WriteLine($"🔄 Restored backup after cancellation");
                }
            }
            catch (Exception restoreEx)
            {
                LogService.Error($"Failed to restore backup after cancellation: {backupPath}: {restoreEx.Message}");
            }
            
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Atomic streaming failed, restoring backup: {ex.Message}");
            
            // Restore backup on failure
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Move(backupPath, originalFilePath, overwrite: true);
                    Debug.WriteLine($"🔄 Restored backup after failure");
                    LogService.Info($"Restored backup after atomic streaming failure");
                }
            }
            catch (Exception restoreEx)
            {
                LogService.Error($"Failed to restore backup after failure: {backupPath}: {restoreEx.Message}");
            }
            
            throw;
        }
        finally
        {
            // Clean up any remaining temp files
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                // Note: backup is cleaned up in success path or kept for manual recovery in error paths
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Failed to clean up temp file: {ex.Message}");
            }
        }
    }
    
    #endregion

    #region Backup Management
    
    /// <summary>
    /// Cleans up old backup files in the specified directory with count limits
    /// </summary>
    public async Task<CleanupResult> CleanupOldBackupsAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return new CleanupResult(true, "Directory does not exist, no backups to clean", null, 0, 0);
            }

            var backupPatterns = new[] { "*.backup_*", "*.removed_*" };
            var allBackupFiles = backupPatterns
                .SelectMany(pattern => Directory.EnumerateFiles(directoryPath, pattern, SearchOption.TopDirectoryOnly))
                .Select(path => new FileInfo(path))
                .ToList();

            var cutoffTime = DateTime.Now - _options.BackupRetentionPeriod;
            var scannedCount = allBackupFiles.Count;
            var deletedCount = 0;

            LogService.Info($"Starting backup cleanup in {directoryPath}. Found {scannedCount} files. Retention: {_options.BackupRetentionPeriod}, Max per directory: {_options.MaxBackupsPerDirectory}");

            Debug.WriteLine($"🧹 Found {scannedCount} files in {directoryPath}");
            Debug.WriteLine($"🧹 Cutoff time: {cutoffTime:yyyy-MM-dd HH:mm:ss}");

            // Sort once by creation time
            var sortedBackups = allBackupFiles.OrderBy(f => f.CreationTimeUtc).ToList();

            // Files older than retention period
            var filesToDelete = sortedBackups
                .Where(f => f.CreationTimeUtc < cutoffTime)
                .ToList();

            // Enforce max per directory
            var remainingCount = sortedBackups.Count - filesToDelete.Count;
            if (remainingCount > _options.MaxBackupsPerDirectory)
            {
                var excess = remainingCount - _options.MaxBackupsPerDirectory;
                filesToDelete.AddRange(sortedBackups
                    .Except(filesToDelete)
                    .Take(excess));
                
                LogService.Info(
                    $"Directory {directoryPath} over backup limit ({remainingCount} > {_options.MaxBackupsPerDirectory}), removing {excess} oldest files");
            }

            // Delete files (run asynchronously on background thread)
            await Task.Run(() =>
            {
                foreach (var file in filesToDelete)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        file.Delete();
                        deletedCount++;
                        Debug.WriteLine($"🗑️ Deleted: {file.Name} ({file.CreationTime:yyyy-MM-dd HH:mm:ss})");
                    }
                    catch (Exception ex)
                    {
                        LogService.Warning($"Failed to delete backup file {file.FullName}: {ex.Message}");
                        Debug.WriteLine($"⚠️ Failed to delete {file.Name}: {ex.Message}");
                    }
                }
            }, cancellationToken);

            LogService.Info(
                $"✅ Cleanup complete in {directoryPath}: {deletedCount} files deleted of {scannedCount}");

            return new CleanupResult(true, $"Cleaned up {deletedCount} old backup files", null, deletedCount, scannedCount);
        }
        catch (OperationCanceledException)
        {
            LogService.Warning($"❌ Backup cleanup was cancelled");
            return new CleanupResult(false, "Backup cleanup was cancelled", PHPExtensionErrorCodes.Cancelled);
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Failed to cleanup backups in {directoryPath}: {ex.Message}");
            return new CleanupResult(false, ex.Message, PHPExtensionErrorCodes.BackupCleanupFailed);
        }
    }

    
    /// <summary>
    /// Cleans up all backup files across all PHP installations with parallel processing
    /// </summary>
    public async Task<CleanupResult> CleanupAllBackupsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var services = await _serviceRepository.GetAllAsync(cancellationToken);
            var phpServices = services.Where(s => s.ServiceType == ModStack.Common.ServiceType.PHP).ToList();
            
            Debug.WriteLine($"🧹 Starting global backup cleanup for {phpServices.Count} PHP services");
            LogService.Info($"Starting global backup cleanup for {phpServices.Count} PHP services");
            
            var totalDeleted = 0;
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.BackupCleanupMaxParallelism,
                CancellationToken = cancellationToken
            };
            
            Debug.WriteLine($"🚀 Using parallel processing with max {_options.BackupCleanupMaxParallelism} concurrent operations");
            LogService.Info($"Using parallel backup cleanup with MaxDegreeOfParallelism: {_options.BackupCleanupMaxParallelism}");
            
            var totalScanned = 0;
            var lockObject = new object();
            
            await Parallel.ForEachAsync(phpServices, parallelOptions, async (service, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                
                if (string.IsNullOrEmpty(service.BinaryPath)) return;
                
                var serviceDeleted = 0;
                var serviceScanned = 0;
                
                // Clean up backups in the service's ext directory
                var extPath = Path.Combine(service.BinaryPath, "ext");
                if (Directory.Exists(extPath))
                {
                    var result = await CleanupOldBackupsAsync(extPath, ct);
                    serviceDeleted += result.DeletedCount;
                    serviceScanned += result.ScannedCount;
                }
                
                // Clean up PHP ini backups
                var phpIniPath = Path.Combine(service.BinaryPath, "php.ini");
                if (File.Exists(phpIniPath))
                {
                    var iniDir = Path.GetDirectoryName(phpIniPath);
                    if (!string.IsNullOrEmpty(iniDir))
                    {
                        var result = await CleanupOldBackupsAsync(iniDir, ct);
                        serviceDeleted += result.DeletedCount;
                        serviceScanned += result.ScannedCount;
                    }
                }
                
                // Thread-safe addition to totals
                lock (lockObject)
                {
                    totalDeleted += serviceDeleted;
                    totalScanned += serviceScanned;
                }
                
                if (serviceDeleted > 0 || serviceScanned > 0)
                {
                    Debug.WriteLine($"🧹 Service {service.Name}: deleted {serviceDeleted}/{serviceScanned} backup files");
                }
            });
            
            Debug.WriteLine($"✅ Global backup cleanup complete: {totalDeleted} files deleted out of {totalScanned} scanned");
            LogService.Info($"Global backup cleanup completed: {totalDeleted} files deleted out of {totalScanned} scanned across all services");
            
            return new CleanupResult(true, $"Global cleanup completed: {totalDeleted} backup files deleted", null, totalDeleted, totalScanned);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"❌ Global backup cleanup cancelled");
            return new CleanupResult(false, "Global backup cleanup was cancelled", PHPExtensionErrorCodes.Cancelled);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Exception during global backup cleanup: {ex.Message}");
            LogService.Error($"Failed to perform global backup cleanup: {ex.Message}");
            return new CleanupResult(false, ex.Message, PHPExtensionErrorCodes.GlobalBackupCleanupFailed);
        }
    }
    
    /// <summary>
    /// Automatically cleans up old backups in a directory if auto-cleanup is enabled
    /// </summary>
    private async Task AutoCleanupBackupsAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (!_options.AutoCleanupBackups) return;
        
        try
        {
            await CleanupOldBackupsAsync(directoryPath, cancellationToken);
        }
        catch (Exception ex)
        {
            // Auto-cleanup failures should not break the main operation
            Debug.WriteLine($"⚠️ Auto-cleanup failed: {ex.Message}");
            LogService.Warning($"Auto-cleanup of backups failed in {directoryPath}: {ex.Message}");
        }
    }
    
    #endregion

    #region IDisposable

    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            // Perform safety cleanup if enabled (fire-and-forget to avoid blocking finalization)
            if (_options.CleanupOnDispose)
            {
                Debug.WriteLine($"🧹 Starting fire-and-forget backup cleanup on dispose");
                LogService.Info($"Starting fire-and-forget backup cleanup during dispose");
                
                // Use Task.Run for fire-and-forget cleanup that won't block dispose
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)); // Generous timeout for background cleanup
                        var result = await CleanupAllBackupsAsync(cts.Token);
                        
                        Debug.WriteLine($"✅ Background dispose cleanup completed: {result.DeletedCount} files deleted");
                        LogService.Info($"Background dispose cleanup completed: {result.DeletedCount} backup files deleted");
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine($"⚠️ Background dispose cleanup was cancelled");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"⚠️ Background dispose cleanup failed: {ex.Message}");
                        LogService.Warning($"Background dispose cleanup failed: {ex.Message}");
                    }
                });
                
                Debug.WriteLine($"✅ Fire-and-forget cleanup initiated");
            }
            
            _phpIniLock?.Dispose();
            Debug.WriteLine($"🗑️ PHPExtensionManager disposed (cleanup running in background)");
        }
        _disposed = true;
    }

    #endregion
}

