using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Common.Interfaces;

namespace ModStack.Services
{
    /// <summary>
    /// Service for managing available packages and their installation
    /// </summary>
    public class PackageManagerService
    {
        private readonly IPackageRepository _packageRepository;
        private readonly IPathConfigurationService _pathConfigurationService;

        public PackageManagerService(IPackageRepository packageRepository, IPathConfigurationService pathConfigurationService)
        {
            _packageRepository = packageRepository;
            _pathConfigurationService = pathConfigurationService;
        }

        /// <summary>
        /// Get all available packages that can be downloaded
        /// </summary>
        public async Task<List<AvailablePackage>> GetAvailablePackagesAsync()
        {
            // Initialize default packages if database is empty
            await _packageRepository.InitializeDefaultPackagesAsync(_pathConfigurationService);
            
            return await _packageRepository.GetAllAsync();
        }

        /// <summary>
        /// Get packages filtered by category
        /// </summary>
        public async Task<List<AvailablePackage>> GetPackagesByCategoryAsync(string category)
        {
            return await _packageRepository.GetByCategoryAsync(category);
        }

        /// <summary>
        /// Search packages by name or description
        /// </summary>
        public async Task<List<AvailablePackage>> SearchPackagesAsync(string searchTerm)
        {
            return await _packageRepository.SearchAsync(searchTerm);
        }

        /// <summary>
        /// Get popular/recommended packages
        /// </summary>
        public async Task<List<AvailablePackage>> GetPopularPackagesAsync()
        {
            return await _packageRepository.GetPopularAsync();
        }

        /// <summary>
        /// Get a specific package by ID with latest data from database
        /// </summary>
        public async Task<AvailablePackage?> GetPackageByIdAsync(long id)
        {
            return await _packageRepository.GetByIdAsync(id);
        }

        /// <summary>
        /// Get a specific package by name with latest data from database
        /// </summary>
        public async Task<AvailablePackage?> GetPackageByNameAsync(string name)
        {
            return await _packageRepository.GetByNameAsync(name);
        }

        /// <summary>
        /// Add a new package manually
        /// </summary>
        public async Task<AvailablePackage> AddPackageAsync(AvailablePackage package)
        {
            return await _packageRepository.AddAsync(package);
        }

        /// <summary>
        /// Update an existing package
        /// </summary>
        public async Task<AvailablePackage> UpdatePackageAsync(AvailablePackage package)
        {
            return await _packageRepository.UpdateAsync(package);
        }

        /// <summary>
        /// Delete a package
        /// </summary>
        public async Task<bool> DeletePackageAsync(long packageId)
        {
            return await _packageRepository.DeleteAsync(packageId);
        }

        /// <summary>
        /// Check if package exists
        /// </summary>
        public async Task<bool> PackageExistsAsync(string name)
        {
            return await _packageRepository.ExistsAsync(name);
        }

        /// <summary>
        /// Format file size for display
        /// </summary>
        public string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
