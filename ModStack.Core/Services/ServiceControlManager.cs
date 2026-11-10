using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModStack.Core.Common;
using ModStack.Core.Extensions;
using ModStack.Core.Interfaces;
using ModStack.Common.Models;

namespace ModStack.Core.Services;

/// <summary>
/// Manages service control operations (start, stop, status checking) separate from UI concerns
/// </summary>
public class ServiceControlManager(
    IServiceInstallationChecker installationChecker,
    ILogger<ServiceControlManager> logger)
{
    private readonly IServiceInstallationChecker _installationChecker = installationChecker ?? throw new ArgumentNullException(nameof(installationChecker));
    private readonly ILogger<ServiceControlManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    
    private static readonly char[] SpaceSeparator = [' '];
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private static string? TryGetProcessExecutablePath(Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }
        }
        catch
        {
            // Fallback to QueryFullProcessImageName below
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var buffer = new StringBuilder(1024);
            int size = buffer.Capacity;
            if (QueryFullProcessImageName(handle, 0, buffer, ref size))
            {
                return buffer.ToString(0, size);
            }
        }
        catch
        {
            // Ignore and return null
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                CloseHandle(handle);
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a service has a port conflict with other running services or external processes
    /// </summary>
    public static async Task<ServiceDisplayItem?> CheckPortConflictAsync(ServiceDisplayItem serviceToStart, IEnumerable<ServiceDisplayItem> allServices)
    {
        try
        {
            ModStack.Common.Services.LogService.Info($"Checking port conflict for {serviceToStart.Name} on port {serviceToStart.Port}");

            // Check for conflicts with other ModStack services
            var conflictingService = allServices
                .FirstOrDefault(service =>
                    service.Id != serviceToStart.Id &&
                    service.Port == serviceToStart.Port &&
                    string.Equals(service.Status, "Running", StringComparison.OrdinalIgnoreCase));

            if (conflictingService != null)
            {
                ModStack.Common.Services.LogService.Warning($"Port conflict detected: {conflictingService.Name} is already running on port {conflictingService.Port}");
                return conflictingService;
            }

            // Check for external processes using the port
            ModStack.Common.Services.LogService.Info($"Checking for external processes using port {serviceToStart.Port}");
            var externalProcessInfo = await CheckExternalPortUsageAsync(serviceToStart.Port);
            if (externalProcessInfo != null)
            {
                ModStack.Common.Services.LogService.Warning($"External process conflict detected: {externalProcessInfo.ProcessName} (PID: {externalProcessInfo.ProcessId}) is using port {serviceToStart.Port}");
                
                // Create a temporary service item to represent the external process
                var externalService = new ServiceDisplayItem
                {
                    Name = externalProcessInfo.ProcessName,
                    Version = externalProcessInfo.ProcessVersion ?? "Unknown",
                    Port = serviceToStart.Port,
                    Status = "Running (External)",
                    ProcessIds = externalProcessInfo.ProcessId.ToString(),
                    Type = ModStack.Common.ServiceType.Other
                };
                
                return externalService;
            }
            else
            {
                ModStack.Common.Services.LogService.Info($"No external processes found using port {serviceToStart.Port}");
            }

            ModStack.Common.Services.LogService.Info($"No port conflict found for port {serviceToStart.Port}");
            return null;
        }
        catch (Exception ex)
        {
            ModStack.Common.Services.LogService.Error($"Error checking port conflict for {serviceToStart.Name}: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Checks if an external process is using the specified port
    /// </summary>
    private static async Task<ExternalProcessInfo?> CheckExternalPortUsageAsync(int port)
    {
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Use netstat -ano directly (without shell pipe) to check port usage
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "netstat",
                        Arguments = "-ano",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    if (process == null) 
                    {
                        ModStack.Common.Services.LogService.Info($"Failed to start netstat process for port {port}");
                        return null;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                    {
                        ModStack.Common.Services.LogService.Info($"No netstat results for port {port} (ExitCode: {process.ExitCode})");
                        return null;
                    }

                    // Parse netstat output to find the process ID listening on exact port
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var portSuffix = $":{port}";
                    foreach (var line in lines)
                    {
                        if (line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 5 &&
                                parts[1].EndsWith(portSuffix, StringComparison.Ordinal) &&
                                int.TryParse(parts[^1], out int pid))
                            {
                                try
                                {
                                    var processInfo = Process.GetProcessById(pid);
                                    return new ExternalProcessInfo
                                    {
                                        ProcessId = pid,
                                        ProcessName = processInfo.ProcessName,
                                        ProcessVersion = GetProcessVersion(processInfo)
                                    };
                                }
                                catch (ArgumentException)
                                {
                                    // Process doesn't exist anymore
                                    continue;
                                }
                            }
                        }
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    ModStack.Common.Services.LogService.Error($"Error checking external port usage for port {port}: {ex.Message}", ex);
                    return null;
                }
            });
        }
        catch (Exception ex)
        {
            ModStack.Common.Services.LogService.Error($"Error in CheckExternalPortUsageAsync for port {port}: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Gets the version of a process
    /// </summary>
    private static string? GetProcessVersion(Process process)
    {
        try
        {
            var exePath = TryGetProcessExecutablePath(process);
            if (string.IsNullOrEmpty(exePath))
            {
                return null;
            }
            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
            return !string.IsNullOrEmpty(versionInfo.FileVersion) ? versionInfo.FileVersion : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets detailed error information for service startup failures
    /// </summary>
    private static string GetDetailedServiceError(ServiceDisplayItem service, string standardError)
    {
        var errorDetails = new List<string>();
        
        if (!string.IsNullOrEmpty(standardError))
        {
            errorDetails.Add($"Standard Error: {standardError.Trim()}");
        }
        
        // For MySQL, check the error log file
        if (service.Type == ModStack.Common.ServiceType.MySQL)
        {
            var mysqlErrorLog = GetMySQLErrorLogPath(service);
            if (!string.IsNullOrEmpty(mysqlErrorLog) && File.Exists(mysqlErrorLog))
            {
                try
                {
                    var logLines = File.ReadAllLines(mysqlErrorLog).TakeLast(10); // Get last 10 lines
                    var recentErrors = logLines.Where(line => 
                        line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Fatal", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Failed", StringComparison.OrdinalIgnoreCase)).ToList();
                    
                    if (recentErrors.Count > 0)
                    {
                        errorDetails.Add("MySQL Error Log:");
                        errorDetails.AddRange(recentErrors);
                    }
                }
                catch (Exception ex)
                {
                    errorDetails.Add($"Could not read MySQL error log: {ex.Message}");
                }
            }
        }
        
        return errorDetails.Count > 0 ? string.Join(" | ", errorDetails) : "No detailed error information available";
    }
    
    /// <summary>
    /// Gets the MySQL error log path from the service configuration
    /// </summary>
    private static string? GetMySQLErrorLogPath(ServiceDisplayItem service)
    {
        // Try to extract log path from service path or use default location
        var baseDir = service.Path;
        if (string.IsNullOrEmpty(baseDir)) return null;
        
        // Common MySQL error log locations
        var possiblePaths = new[]
        {
            Path.Combine(baseDir, "logs", "error.log"),
            Path.Combine(baseDir, "data", "error.log"),
            Path.Combine(baseDir, "error.log"),
            Path.Combine(baseDir, "data", $"{Environment.MachineName.ToLower()}.err")
        };
        
        return possiblePaths.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Information about an external process using a port
    /// </summary>
    private class ExternalProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string? ProcessVersion { get; set; }
    }

    /// <summary>
    /// Detects the current status of a service
    /// </summary>
    public async Task<string> DetectServiceStatusAsync(ServiceDisplayItem service)
    {
        try
        {
            // Check if service is actually installed first
            bool isInstalled = IsServiceInstalled(service);
            if (!isInstalled)
            {
                return "Not Installed";
            }

            // Check service status based on type
            return await CheckServiceStatusAsync(service);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting status for service {ServiceName}", service.Name);
            return "Error";
        }
    }

    /// <summary>
    /// Starts a service
    /// </summary>
    public async Task StartServiceAsync(ServiceDisplayItem service, CancellationToken cancellationToken = default)
    {
        // For PostgreSQL, ensure database is initialized before starting
        if (service.Type == ModStack.Common.ServiceType.PostgreSQL)
        {
            _logger.LogInformation("Checking PostgreSQL initialization for {ServiceName}", service.Name);
            var initialized = await ModStack.Core.Extensions.ServiceTypeExtensions.EnsurePostgreSqlInitializedAsync(service.Path);
            if (!initialized)
            {
                throw new InvalidOperationException($"Failed to initialize PostgreSQL database for {service.Name}. Please check that the PostgreSQL binaries are correctly installed.");
            }
            _logger.LogInformation("PostgreSQL database initialized successfully for {ServiceName}", service.Name);
        }

        var executablePath = service.Type.GetExecutablePath(service.Path);
        if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
            throw new FileNotFoundException($"Service executable not found: {executablePath ?? service.Path}");

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath),
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Add service-specific arguments
        var arguments = service.Type.GetStartArguments(service);
        if (!string.IsNullOrEmpty(arguments))
        {
            startInfo.Arguments = arguments;
        }

        _logger.LogInformation("Starting {ServiceName}: {ExecutablePath} {Arguments}", 
            service.Name, executablePath, arguments);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start process for {service.Name}");

        try
        {
            // For Apache, we need special handling
            if (service.Type == ModStack.Common.ServiceType.Apache)
            {
                await StartApacheServiceAsync(process, service, cancellationToken);
            }
            else if (service.Type == ModStack.Common.ServiceType.PostgreSQL)
            {
                // PostgreSQL with pg_ctl returns immediately after starting
                await WaitForPostgreSqlStartupAsync(process, service, cancellationToken);
            }
            else
            {
                // For other services, poll for successful startup
                await WaitForServiceStartupAsync(process, service, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start {ServiceName}", service.Name);
            throw;
        }
    }

    /// <summary>
    /// Stops a service by terminating its processes
    /// </summary>
    public async Task StopServiceAsync(ServiceDisplayItem service, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping service: {ServiceName} with PIDs: {ProcessIds}", service.Name, service.ProcessIds);
        
        if (string.IsNullOrEmpty(service.ProcessIds))
        {
            throw new InvalidOperationException($"No process IDs found for service {service.Name}. The service may not be running.");
        }

        // Parse the comma-separated PIDs
        var pidStrings = service.ProcessIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var pidsToKill = new List<int>();
        
        foreach (var pidString in pidStrings)
        {
            if (int.TryParse(pidString.Trim(), out int pid))
            {
                pidsToKill.Add(pid);
            }
        }

        if (pidsToKill.Count == 0)
        {
            throw new InvalidOperationException($"No valid process IDs found for service {service.Name}.");
        }

        _logger.LogDebug("Targeting specific PIDs for {ServiceName}: {ProcessIds}", service.Name, string.Join(", ", pidsToKill));

        // Kill only the specific processes for this service
        var killedPids = new List<int>();
        var failedPids = new List<int>();

        foreach (var pid in pidsToKill)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                
                // Double-check this process belongs to our service by verifying the path
                var processPath = TryGetProcessExecutablePath(process);
                if (!string.IsNullOrEmpty(processPath) && 
                    processPath.StartsWith(service.Path, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Killing process {ProcessId}: {ProcessPath}", pid, processPath);
                    process.Kill();
                    await process.WaitForExitAsync(cancellationToken);
                    killedPids.Add(pid);
                    _logger.LogDebug("Successfully killed process {ProcessId}", pid);
                }
                else
                {
                    _logger.LogWarning("Process {ProcessId} path mismatch: {ProcessPath} vs {ServicePath}", 
                        pid, processPath, service.Path);
                    failedPids.Add(pid);
                }
            }
            catch (ArgumentException)
            {
                // Process no longer exists
                _logger.LogDebug("Process {ProcessId} no longer exists (already stopped)", pid);
                killedPids.Add(pid); // Count as success since it's stopped
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to kill process {ProcessId}", pid);
                failedPids.Add(pid);
            }
        }

        _logger.LogInformation("Stop operation completed for {ServiceName}. Killed PIDs: {KilledPids}, Failed PIDs: {FailedPids}",
            service.Name, string.Join(", ", killedPids), string.Join(", ", failedPids));

        if (killedPids.Count == 0 && failedPids.Count > 0)
        {
            throw new InvalidOperationException($"Failed to stop any processes for service {service.Name}. PIDs that failed: {string.Join(", ", failedPids)}");
        }

        // Clear the ProcessIds since we stopped the service
        service.ProcessIds = string.Empty;
    }

    private bool IsServiceInstalled(ServiceDisplayItem service)
    {
        if (_installationChecker == null)
        {
            _logger.LogWarning("ServiceInstallationChecker not available, falling back to basic check");
            return !string.IsNullOrEmpty(service.Path) && Directory.Exists(service.Path);
        }
        
        // Add null checks to prevent NullReferenceException
        if (service == null)
        {
            _logger.LogWarning("ServiceDisplayItem is null");
            return false;
        }
        
        if (string.IsNullOrEmpty(service.Path))
        {
            _logger.LogWarning("Service path is null or empty for service: {ServiceName}", service.Name);
            return false;
        }
        
        try
        {
            return _installationChecker.IsServiceInstalled(service.Path, service.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking service installation for {ServiceName} at {ServicePath}", service.Name, service.Path);
            return false;
        }
    }

    private async Task<string> CheckServiceStatusAsync(ServiceDisplayItem service)
    {
        try
        {
            return await Task.Run(() =>
            {
                // Check if process is running from this service's specific directory
                var processName = service.Type.GetProcessName(service.Name);
                _logger.LogDebug("Checking status for {ServiceName} - looking for process: {ProcessName} in path: {ServicePath}", 
                    service.Name, processName, service.Path);
                
                if (!string.IsNullOrEmpty(processName))
                {
                    var processes = Process.GetProcessesByName(processName);
                    _logger.LogDebug("Found {ProcessCount} total processes named '{ProcessName}'", processes.Length, processName);
                    
                    var matchingProcessIds = new List<int>();
                    
                    foreach (var process in processes)
                    {
                        try
                        {
                            // Check if this process is running from our service's directory
                            var processPath = TryGetProcessExecutablePath(process);
                            _logger.LogTrace("Process {ProcessId}: {ProcessPath}", process.Id, processPath);
                            
                            if (!string.IsNullOrEmpty(processPath) && 
                                processPath.StartsWith(service.Path, StringComparison.OrdinalIgnoreCase))
                            {
                                matchingProcessIds.Add(process.Id);
                                _logger.LogTrace("Process {ProcessId} matches service path: {ServicePath}", process.Id, service.Path);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Process might have exited or access denied
                            _logger.LogTrace(ex, "Cannot access process {ProcessId}", process.Id);
                        }
                    }
                    
                    if (matchingProcessIds.Count > 0)
                    {
                        // Update ProcessIds property with comma-separated list
                        service.ProcessIds = string.Join(", ", matchingProcessIds);
                        _logger.LogDebug("Service {ServiceName} is Running (PIDs: {ProcessIds} from {ServicePath})", 
                            service.Name, service.ProcessIds, service.Path);
                        return "Running";
                    }
                }

                // Clear ProcessIds when service is stopped
                service.ProcessIds = string.Empty;
                _logger.LogDebug("Service {ServiceName} is Stopped (no processes from {ServicePath})", service.Name, service.Path);
                return "Stopped";
            });
        }
        catch (Exception ex)
        {
            service.ProcessIds = string.Empty;
            _logger.LogError(ex, "Error checking status for {ServiceName}", service.Name);
            return "Unknown";
        }
    }

    private async Task WaitForServiceStartupAsync(Process process, ServiceDisplayItem service, CancellationToken cancellationToken)
    {
        // Poll for process startup with timeout
        const int timeoutMs = 10000; // 10 seconds
        const int pollIntervalMs = 500; // 0.5 seconds
        
        var startTime = DateTime.UtcNow;
        
        while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Check if process is still running
            if (process.HasExited)
            {
                if (process.ExitCode != 0)
                {
                    var errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken);
                    var detailedError = GetDetailedServiceError(service, errorOutput);
                    throw new InvalidOperationException($"Service {service.Name} failed to start. Exit code: {process.ExitCode}. Error: {detailedError}");
                }
                // Process exited cleanly, which might be normal for some services
                return;
            }
            
            await Task.Delay(pollIntervalMs, cancellationToken);
        }
        
        // If we get here, the process is still running after timeout - that's usually good
        _logger.LogDebug("Service {ServiceName} started successfully (still running after {TimeoutMs}ms)", 
            service.Name, timeoutMs);
    }

    private async Task WaitForPostgreSqlStartupAsync(Process process, ServiceDisplayItem service, CancellationToken cancellationToken)
    {
        try
        {
            // pg_ctl start returns quickly after spawning the postgres process
            // We need to wait for it to complete and check if it started successfully
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            // Wait for pg_ctl to finish (it should return quickly)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(combinedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                _logger.LogWarning("PostgreSQL {ServiceName} startup timed out", service.Name);
                throw new InvalidOperationException($"PostgreSQL startup timed out for {service.Name}");
            }

            var output = await outputTask;
            var error = await errorTask;

            _logger.LogDebug("PostgreSQL pg_ctl output: {Output}", output);
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogDebug("PostgreSQL pg_ctl error: {Error}", error);
            }

            // pg_ctl returns 0 on success
            if (process.ExitCode == 0)
            {
                _logger.LogInformation("PostgreSQL {ServiceName} started successfully", service.Name);
                
                // Give PostgreSQL a moment to fully initialize and start accepting connections
                await Task.Delay(1000, cancellationToken);
                
                // Verify PostgreSQL is actually listening on the port
                await VerifyPostgreSqlListeningAsync(service, cancellationToken);
                return;
            }

            // Build detailed error message
            var errorMessage = $"PostgreSQL failed to start.\n\nExit code: {process.ExitCode}";
            
            if (!string.IsNullOrEmpty(output))
            {
                errorMessage += $"\n\nOutput:\n{output}";
            }
            
            if (!string.IsNullOrEmpty(error))
            {
                errorMessage += $"\n\nError details:\n{error}";
            }

            // Check for common PostgreSQL issues
            errorMessage += "\n\nCommon solutions:\n" +
                           "• Check if port " + service.Port + " is already in use\n" +
                           "• Verify the data directory exists and is initialized\n" +
                           "• Check PostgreSQL log files in the logs directory\n" +
                           "• Ensure no other PostgreSQL instance is running";

            throw new InvalidOperationException(errorMessage);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to start PostgreSQL service: {ex.Message}", ex);
        }
    }

    private async Task VerifyPostgreSqlListeningAsync(ServiceDisplayItem service, CancellationToken cancellationToken)
    {
        // Try to verify PostgreSQL is accepting connections using pg_isready
        var basePath = service.Path;
        var pgIsReadyPath = Path.Combine(basePath, "bin", "pg_isready.exe");
        
        if (!File.Exists(pgIsReadyPath))
        {
            pgIsReadyPath = Path.Combine(basePath, "pgsql", "bin", "pg_isready.exe");
        }

        if (File.Exists(pgIsReadyPath))
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pgIsReadyPath,
                Arguments = $"-h localhost -p {service.Port}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            const int maxAttempts = 10;
            for (int i = 0; i < maxAttempts; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                using var checkProcess = Process.Start(startInfo);
                if (checkProcess != null)
                {
                    await checkProcess.WaitForExitAsync(cancellationToken);
                    if (checkProcess.ExitCode == 0)
                    {
                        _logger.LogDebug("PostgreSQL {ServiceName} is accepting connections on port {Port}", 
                            service.Name, service.Port);
                        return;
                    }
                }
                
                await Task.Delay(500, cancellationToken);
            }
            
            _logger.LogWarning("PostgreSQL {ServiceName} started but may not be accepting connections yet", service.Name);
        }
    }

    private async Task StartApacheServiceAsync(Process process, ServiceDisplayItem service, CancellationToken cancellationToken)
    {
        try
        {
            // Wait for Apache to start and capture output
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            // Give Apache time to start (up to 10 seconds)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(combinedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                // Apache is still running, which is good
                _logger.LogDebug("Apache {ServiceName} started successfully (still running after timeout)", service.Name);
                return;
            }

            // Process exited, check exit code
            if (process.ExitCode == 0)
            {
                _logger.LogDebug("Apache {ServiceName} started successfully", service.Name);
                return;
            }

            // Apache failed to start, capture error details
            var output = await outputTask;
            var error = await errorTask;

            _logger.LogError("Apache {ServiceName} failed to start. Exit code: {ExitCode}, Output: {Output}, Error: {Error}", 
                service.Name, process.ExitCode, output, error);

            // Check if this is just a warning (AH00558) or a real error
            var isWarningOnly = !string.IsNullOrEmpty(error) && 
                               error.Contains("AH00558") && 
                               !error.Contains("AH00526") && // Syntax errors are real errors
                               !error.Contains("AH00548") && // Permission errors are real errors
                               !error.Contains("AH00574");   // Port binding errors are real errors

            if (isWarningOnly)
            {
                // This might just be a warning, Apache could still be running
                _logger.LogWarning("Apache startup warning detected, but service might be running");
                
                // Wait a bit more and check if Apache is actually running
                await Task.Delay(2000, cancellationToken);
                
                // Check if Apache process is running
                var apacheProcesses = Process.GetProcessesByName("httpd");
                if (apacheProcesses.Length > 0)
                {
                    _logger.LogDebug("Apache is actually running despite warning");
                    return; // Success - Apache is running
                }
            }

            // Create a detailed error message
            var errorMessage = $"Apache failed to start.\n\nExit code: {process.ExitCode}";
            
            if (!string.IsNullOrEmpty(error))
            {
                errorMessage += $"\n\nError details:\n{error}";
            }

            if (!string.IsNullOrEmpty(output))
            {
                errorMessage += $"\n\nOutput:\n{output}";
            }

            // Add common troubleshooting tips for Apache
            errorMessage += "\n\nCommon solutions:\n" +
                           "• Check if port 80/443 is already in use\n" +
                           "• Verify Apache configuration with 'httpd -t'\n" +
                           "• Check if required modules are loaded\n" +
                           "• Run as administrator if needed\n" +
                           "• Check Windows Firewall settings\n" +
                           "• Ensure no other web server is running on the same ports";

            throw new InvalidOperationException(errorMessage);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to start Apache service: {ex.Message}", ex);
        }
    }
}
