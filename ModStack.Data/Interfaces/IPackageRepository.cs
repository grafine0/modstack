using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModStack.Common.Models;
using ModStack.Common.Interfaces;

namespace ModStack.Data.Interfaces
{
    /// <summary>
    /// Repository interface for package management operations
    /// </summary>
    public interface IPackageRepository
    {
        /// <summary>
        /// Get all available packages
        /// </summary>
        Task<List<AvailablePackage>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get package by ID
        /// </summary>
        Task<AvailablePackage?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get package by name
        /// </summary>
        Task<AvailablePackage?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get packages by category
        /// </summary>
        Task<List<AvailablePackage>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);

        /// <summary>
        /// Search packages by name, description, or tags
        /// </summary>
        Task<List<AvailablePackage>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get popular packages
        /// </summary>
        Task<List<AvailablePackage>> GetPopularAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get user-added packages
        /// </summary>
        Task<List<AvailablePackage>> GetUserAddedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Add a new package
        /// </summary>
        Task<AvailablePackage> AddAsync(AvailablePackage package, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update an existing package
        /// </summary>
        Task<AvailablePackage> UpdateAsync(AvailablePackage package, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a package
        /// </summary>
        Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if package exists by name
        /// </summary>
        Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get package versions
        /// </summary>
        Task<List<PackageVersion>> GetVersionsAsync(long packageId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Add package version
        /// </summary>
        Task<PackageVersion> AddVersionAsync(long packageId, PackageVersion version, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update package version
        /// </summary>
        Task<PackageVersion> UpdateVersionAsync(PackageVersion version, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete package version
        /// </summary>
        Task<bool> DeleteVersionAsync(long versionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get package configuration
        /// </summary>
        Task<PackageConfiguration?> GetConfigurationAsync(long packageId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Set package configuration
        /// </summary>
        Task<PackageConfiguration> SetConfigurationAsync(long packageId, PackageConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Initialize default packages if database is empty
        /// </summary>
        Task InitializeDefaultPackagesAsync(IPathConfigurationService pathConfigurationService, CancellationToken cancellationToken = default);
    }
}
