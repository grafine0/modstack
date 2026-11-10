using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Common;

namespace ModStack.Services
{
    /// <summary>
    /// Handles package uninstallation and service cleanup
    /// </summary>
    public class PackageUninstaller(
        WindowsServiceManager windowsServiceManager,
        IServiceRepository serviceRepository,
        IPortAllocationRepository? portRepository = null)
    {
        private readonly WindowsServiceManager _windowsServiceManager = windowsServiceManager ?? 
            throw new ArgumentNullException(nameof(windowsServiceManager));
        private readonly IServiceRepository _serviceRepository = serviceRepository ?? 
            throw new ArgumentNullException(nameof(serviceRepository));
        private readonly IPortAllocationRepository? _portRepository = portRepository;

        /// <summary>
        /// Validates if a service can be safely uninstalled
        /// </summary>
        public async Task<ServiceUninstallValidation> ValidateUninstallAsync(string packageName, string? version = null)
        {
            var validation = new ServiceUninstallValidation
            {
                PackageName = packageName,
                Version = version,
                CanUninstall = true,
                Warnings = [],
                RequiredActions = []
            };

            try
            {
                // Find all matching services
                var services = await _serviceRepository.GetAllAsync();
                var matchingServices = FindMatchingServices(services, packageName, version).ToList();

                validation.AffectedServices = matchingServices;

                if (matchingServices.Count == 0)
                {
                    validation.Warnings.Add($"No installed services found for {packageName}" + 
                        (version != null ? $" version {version}" : ""));
                    return validation;
                }

                // Check each service
                foreach (var service in matchingServices)
                {
                    // Check if service is running
                    if (service.Status == (int)ServiceStatus.Running)
                    {
                        validation.RunningServices.Add(service);
                        validation.RequiredActions.Add($"Stop '{service.DisplayName}' service");
                    }

                    // Check if service is registered as Windows Service
                    if (!string.IsNullOrEmpty(service.WindowsServiceName))
                    {
                        validation.RequiredActions.Add($"Unregister Windows service '{service.WindowsServiceName}'");
                    }

                    // Check if service has allocated ports
                    if (_portRepository != null)
                    {
                        var ports = await _portRepository.GetByServiceIdAsync(service.Id);
                        if (ports.Count > 0)
                        {
                            validation.RequiredActions.Add($"Release {ports.Count} allocated port(s)");
                        }
                    }

                    // Check for dependencies (other services that depend on this one)
                    var dependentServices = FindDependentServices(service, services);
                    if (dependentServices.Count > 0)
                    {
                        validation.Warnings.Add($"'{service.DisplayName}' is used by: {string.Join(", ", dependentServices.Select(s => s.DisplayName))}");
                    }

                    // Check if installation directory exists and has files
                    if (!string.IsNullOrEmpty(service.BinaryPath) && Directory.Exists(service.BinaryPath))
                    {
                        var fileCount = Directory.GetFiles(service.BinaryPath, "*", SearchOption.AllDirectories).Length;
                        if (fileCount > 0)
                        {
                            validation.RequiredActions.Add($"Delete {fileCount} file(s) from installation directory");
                        }
                    }
                }

                return validation;
            }
            catch (Exception ex)
            {
                validation.CanUninstall = false;
                validation.ErrorMessage = $"Validation failed: {ex.Message}";
                return validation;
            }
        }

        /// <summary>
        /// Uninstalls a package and cleans up all associated services
        /// </summary>
        public async Task<bool> UninstallPackageAsync(string packageName, string? version = null, IProgressReporter? progressReporter = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Starting uninstallation for {packageName} {version ?? "all versions"} ===");
                progressReporter?.UpdateProgress("Validating uninstallation...", 0);

                // Validate uninstallation
                var validation = await ValidateUninstallAsync(packageName, version);
                if (!validation.CanUninstall)
                {
                    throw new Exception(validation.ErrorMessage ?? "Uninstallation validation failed");
                }

                if (validation.AffectedServices.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No services found to uninstall");
                    return true; // Nothing to uninstall
                }

                var totalServices = validation.AffectedServices.Count;
                var currentService = 0;

                foreach (var service in validation.AffectedServices)
                {
                    currentService++;
                    var progressPercent = (currentService * 80) / totalServices; // 80% for service cleanup
                    
                    System.Diagnostics.Debug.WriteLine($"🗑️ Uninstalling service: {service.DisplayName} (ID: {service.Id})");
                    progressReporter?.UpdateProgress($"Uninstalling {service.DisplayName}...", progressPercent);

                    // Stop service if running
                    if (service.Status == (int)ServiceStatus.Running)
                    {
                        System.Diagnostics.Debug.WriteLine($"🛑 Stopping running service: {service.DisplayName}");
                        await _windowsServiceManager.StopServiceAsync(service.Id);
                        
                        // Wait for service to stop
                        await Task.Delay(2000);
                    }

                    // Uninstall Windows service
                    if (!string.IsNullOrEmpty(service.WindowsServiceName))
                    {
                        System.Diagnostics.Debug.WriteLine($"🗑️ Unregistering Windows service: {service.WindowsServiceName}");
                        await _windowsServiceManager.UninstallServiceAsync(service.Id);
                    }

                    // Delete installation files
                    if (!string.IsNullOrEmpty(service.BinaryPath) && Directory.Exists(service.BinaryPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"📁 Deleting installation directory: {service.BinaryPath}");
                        await DeleteInstallationDirectoryAsync(service.BinaryPath);
                    }

                    // Update service status in database
                    await _serviceRepository.UpdateInstallationStatusAsync(
                        service.Id, 
                        isInstalled: false, 
                        binaryPath: null, 
                        version: null
                    );

                    System.Diagnostics.Debug.WriteLine($"✅ Service {service.DisplayName} uninstalled successfully");
                }

                progressReporter?.UpdateProgress("Uninstallation complete!", 100);
                System.Diagnostics.Debug.WriteLine($"=== Uninstallation completed successfully for {packageName} ===");
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== Uninstallation FAILED for {packageName} ===");
                System.Diagnostics.Debug.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                progressReporter?.UpdateProgress($"Uninstallation failed: {ex.Message}", -1);
                return false;
            }
        }

        /// <summary>
        /// Finds services matching the package name and version
        /// </summary>
        private static IEnumerable<ServiceDefinition> FindMatchingServices(
            IEnumerable<ServiceDefinition> allServices, 
            string packageName, 
            string? version = null)
        {
            var packageNameLower = packageName.ToLower();
            
            return allServices.Where(service =>
            {
                var serviceNameLower = service.Name.ToLower();
                
                if (version == null)
                {
                    // Match any version of the package
                    return serviceNameLower == packageNameLower || 
                           serviceNameLower.StartsWith($"{packageNameLower}-");
                }
                else
                {
                    // Match specific version
                    return serviceNameLower == packageNameLower || 
                           serviceNameLower == $"{packageNameLower}-{version}";
                }
            }).Where(s => s.IsInstalled);
        }

        /// <summary>
        /// Finds services that depend on the given service
        /// </summary>
        private static List<ServiceDefinition> FindDependentServices(
            ServiceDefinition service, 
            IEnumerable<ServiceDefinition> allServices)
        {
            // This is a simplified dependency check
            // In a more complex system, you'd check actual service dependencies
            var dependents = new List<ServiceDefinition>();
            
            // Example: PHP services depend on web servers
            if (service.ServiceType == ServiceType.Apache || service.ServiceType == ServiceType.Nginx)
            {
                var phpServices = allServices.Where(s => 
                    s.ServiceType == ServiceType.PHP && 
                    s.IsInstalled && 
                    s.Status == (int)ServiceStatus.Running);
                dependents.AddRange(phpServices);
            }

            return dependents;
        }

        /// <summary>
        /// Safely deletes installation directory with retry logic
        /// </summary>
        private static async Task DeleteInstallationDirectoryAsync(string path)
        {
            const int maxRetries = 3;
            const int delayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        // First, try to kill any processes that might be using files in this directory
                        await KillProcessesUsingDirectoryAsync(path);
                        
                        // Wait a bit for processes to fully terminate
                        await Task.Delay(delayMs);
                        
                        // Delete the directory
                        Directory.Delete(path, recursive: true);
                        System.Diagnostics.Debug.WriteLine($"✅ Deleted installation directory: {path}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Attempt {attempt} to delete {path} failed: {ex.Message}");
                    
                    if (attempt == maxRetries)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Failed to delete {path} after {maxRetries} attempts");
                        throw new Exception($"Could not delete installation directory {path}: {ex.Message}");
                    }
                    
                    await Task.Delay(delayMs * attempt); // Exponential backoff
                }
            }
        }

        /// <summary>
        /// Kills processes that might be using files in the specified directory
        /// </summary>
        private static async Task KillProcessesUsingDirectoryAsync(string directoryPath)
        {
            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => !p.HasExited && IsProcessUsingDirectory(p, directoryPath))
                    .ToArray();

                if (processes.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"🛑 Found {processes.Length} processes using directory {directoryPath}");
                    
                    foreach (var process in processes)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"🔪 Killing process: {process.ProcessName} (PID: {process.Id})");
                            process.Kill();
                            await process.WaitForExitAsync();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ Could not kill process {process.ProcessName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Error killing processes: {ex.Message}");
                // Don't throw - this is best effort
            }
        }

        /// <summary>
        /// Checks if a process is using files in the specified directory
        /// </summary>
        private static bool IsProcessUsingDirectory(Process process, string directoryPath)
        {
            try
            {
                if (process.HasExited) return false;

                var processPath = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(processPath)) return false;

                return processPath.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false; // If we can't check, assume it's not using the directory
            }
        }
    }

}
