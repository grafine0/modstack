using ModStack.Common.Models;

namespace ModStack.Services.Interfaces
{
    /// <summary>
    /// Interface for managing virtual hosts for Apache and Nginx services
    /// </summary>
    public interface IVirtualHostManager
    {
        /// <summary>
        /// Creates a virtual host for a project
        /// </summary>
        Task<VirtualHost> CreateVirtualHostAsync(CreateVirtualHostRequest request);

        /// <summary>
        /// Updates an existing virtual host
        /// </summary>
        Task<bool> UpdateVirtualHostAsync(VirtualHost virtualHost);

        /// <summary>
        /// Deletes a virtual host
        /// </summary>
        Task DeleteVirtualHostAsync(long virtualHostId);

        /// <summary>
        /// Gets all virtual hosts
        /// </summary>
        Task<List<VirtualHost>> GetAllVirtualHostsAsync();


        /// <summary>
        /// Gets all virtual hosts for a service
        /// </summary>
        Task<List<VirtualHost>> GetVirtualHostsForServiceAsync(long serviceId);

        /// <summary>
        /// Lists all virtual host domain names (for compatibility with GUI service)
        /// </summary>
        Task<List<string>> GetAllVirtualHostDomainsAsync();
    }
}
