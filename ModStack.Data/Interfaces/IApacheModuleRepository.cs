using ModStack.Common.Models;

namespace ModStack.Data.Interfaces
{
    /// <summary>
    /// Repository interface for Apache module data operations
    /// </summary>
    public interface IApacheModuleRepository
    {
        /// <summary>
        /// Gets all Apache modules
        /// </summary>
        Task<IEnumerable<ApacheModule>> GetAllAsync();
        
        /// <summary>
        /// Gets Apache modules by category
        /// </summary>
        Task<IEnumerable<ApacheModule>> GetByCategoryAsync(string category);
        
        /// <summary>
        /// Gets enabled Apache modules
        /// </summary>
        Task<IEnumerable<ApacheModule>> GetEnabledAsync();
        
        /// <summary>
        /// Gets available Apache modules (file exists)
        /// </summary>
        Task<IEnumerable<ApacheModule>> GetAvailableAsync();
        
        /// <summary>
        /// Gets Apache module by module name
        /// </summary>
        Task<ApacheModule?> GetByModuleNameAsync(string moduleName);
        
        /// <summary>
        /// Gets Apache module by ID
        /// </summary>
        Task<ApacheModule?> GetByIdAsync(long id);
        
        /// <summary>
        /// Creates a new Apache module
        /// </summary>
        Task<ApacheModule> CreateAsync(ApacheModule module);
        
        /// <summary>
        /// Updates an existing Apache module
        /// </summary>
        Task<ApacheModule> UpdateAsync(ApacheModule module);
        
        /// <summary>
        /// Deletes an Apache module
        /// </summary>
        Task<bool> DeleteAsync(long id);
        
        /// <summary>
        /// Updates the enabled status of a module
        /// </summary>
        Task<bool> UpdateEnabledStatusAsync(long id, bool isEnabled);
        
        /// <summary>
        /// Updates the availability status of a module
        /// </summary>
        Task<bool> UpdateAvailabilityStatusAsync(long id, bool isAvailable);
        
        /// <summary>
        /// Gets distinct categories
        /// </summary>
        Task<IEnumerable<string>> GetCategoriesAsync();
        
        /// <summary>
        /// Searches modules by name or description
        /// </summary>
        Task<IEnumerable<ApacheModule>> SearchAsync(string searchTerm);
    }
}
