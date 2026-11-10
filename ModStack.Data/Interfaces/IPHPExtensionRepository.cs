using ModStack.Common.Models;

namespace ModStack.Data.Interfaces;

/// <summary>
/// Repository interface for managing PHP extensions
/// </summary>
public interface IPHPExtensionRepository
{
    // Available Extensions
    Task<List<PHPExtension>> GetAvailableExtensionsAsync();
    Task<List<PHPExtension>> GetExtensionsByCategoryAsync(string category);
    Task<List<PHPExtension>> GetPopularExtensionsAsync();
    Task<List<PHPExtension>> GetExtensionsForPHPVersionAsync(string phpVersion);
    Task<PHPExtension?> GetExtensionByNameAsync(string name);
    Task<PHPExtension?> GetExtensionByIdAsync(long id);
    
    // Installed Extensions
    Task<List<InstalledPHPExtension>> GetInstalledExtensionsAsync(long serviceId);
    Task<List<InstalledPHPExtension>> GetInstalledExtensionsForVersionAsync(long serviceId, string phpVersion);
    Task<InstalledPHPExtension?> GetInstalledExtensionAsync(long serviceId, long extensionId);
    Task<bool> IsExtensionInstalledAsync(long serviceId, long extensionId);
    
    // Extension Management
    Task<InstalledPHPExtension> InstallExtensionAsync(InstallPHPExtensionRequest request);
    Task<bool> UninstallExtensionAsync(long serviceId, long extensionId);
    Task<bool> EnableExtensionAsync(long serviceId, long extensionId);
    Task<bool> DisableExtensionAsync(long serviceId, long extensionId);
    Task<bool> UpdateExtensionConfigurationAsync(long serviceId, long extensionId, string configuration);
    
    // Extension Data Management
    Task<PHPExtension> CreateExtensionAsync(PHPExtension extension);
    Task<bool> UpdateExtensionAsync(PHPExtension extension);
    Task<bool> DeleteExtensionAsync(long id);
    
    // Initialization
    Task InitializeDefaultExtensionsAsync();
}
