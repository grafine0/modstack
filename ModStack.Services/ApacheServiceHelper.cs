using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Common;
using System.Diagnostics;

namespace ModStack.Services;

/// <summary>
/// Shared helper for Apache service operations
/// </summary>
public static class ApacheServiceHelper
{
    /// <summary>
    /// Finds the first available Apache service
    /// </summary>
    public static async Task<ServiceDefinition?> FindApacheServiceAsync(IServiceRepository serviceRepository, ILogger logger)
    {
        try
        {
            var services = await serviceRepository.GetAllAsync();
            return services.FirstOrDefault(s => s.ServiceType == ServiceType.Apache);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to find Apache service");
            return null;
        }
    }

    /// <summary>
    /// Finds Apache installation path
    /// </summary>
    public static string? FindApacheInstallationPath(ILogger logger)
    {
        var possiblePaths = new[]
        {
            @"C:\ModStack\Services\Apache"
        };

        foreach (var basePath in possiblePaths)
        {
            if (Directory.Exists(basePath))
            {
                // Look for version-specific directories
                var versionDirs = Directory.GetDirectories(basePath, "apache-*")
                    .OrderByDescending(d => d)
                    .ToList();

                foreach (var versionDir in versionDirs)
                {
                    var httpdPath = Path.Combine(versionDir, "bin", "httpd.exe");
                    if (File.Exists(httpdPath))
                    {
                        logger.LogDebug("Found Apache at: {ApachePath}", versionDir);
                        return versionDir;
                    }
                }

                // Check the base path directly
                var directHttpdPath = Path.Combine(basePath, "bin", "httpd.exe");
                if (File.Exists(directHttpdPath))
                {
                    logger.LogDebug("Found Apache at: {ApachePath}", basePath);
                    return basePath;
                }
            }
        }

        logger.LogWarning("Apache installation not found in expected paths");
        return null;
    }

    /// <summary>
    /// Ensures Apache configuration has ServerName directive to prevent domain name warnings
    /// </summary>
    public static async Task<bool> EnsureServerNameDirectiveAsync(string apachePath, ILogger logger)
    {
        try
        {
            var confPath = Path.Combine(apachePath, "conf", "httpd.conf");
            if (!File.Exists(confPath))
            {
                logger.LogWarning("Apache httpd.conf not found at {ConfPath}", confPath);
                return false;
            }

            var content = await File.ReadAllTextAsync(confPath);
            
            // Check if ServerName directive already exists
            if (content.Contains("ServerName localhost"))
            {
                logger.LogDebug("Apache already has ServerName directive");
                return true;
            }

            // Add ServerName directive after the Listen directives
            var serverNameDirective = "\n# ModStack ServerName directive to prevent domain name warning\nServerName localhost\n";
            
            // Try to insert after Listen directives, otherwise append at the end
            var listenIndex = content.LastIndexOf("Listen");
            if (listenIndex != -1)
            {
                var lineEndIndex = content.IndexOf('\n', listenIndex);
                if (lineEndIndex != -1)
                {
                    content = content.Insert(lineEndIndex + 1, serverNameDirective);
                }
                else
                {
                    content += serverNameDirective;
                }
            }
            else
            {
                content += serverNameDirective;
            }
            
            await File.WriteAllTextAsync(confPath, content);
            logger.LogInformation("Added ServerName directive to Apache configuration");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding ServerName directive to Apache configuration");
            return false;
        }
    }

    /// <summary>
    /// Tests Apache configuration validity
    /// </summary>
    public static async Task<bool> TestApacheConfigurationAsync(string apachePath, ILogger logger)
    {
        try
        {
            var httpdPath = Path.Combine(apachePath, "bin", "httpd.exe");
            if (!File.Exists(httpdPath))
            {
                logger.LogWarning("Apache httpd.exe not found at {HttpdPath}", httpdPath);
                return false;
            }

            // Test configuration
            var testProcess = new ProcessStartInfo
            {
                FileName = httpdPath,
                Arguments = "-t",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var testProc = Process.Start(testProcess);
            if (testProc != null)
            {
                await testProc.WaitForExitAsync();
                var isValid = testProc.ExitCode == 0;
                
                if (isValid)
                {
                    logger.LogInformation("Apache configuration test passed");
                }
                else
                {
                    var error = await testProc.StandardError.ReadToEndAsync();
                    
                    // Check if it's just a ServerName warning (non-fatal)
                    if (error.Contains("AH00558") && error.Contains("ServerName"))
                    {
                        logger.LogWarning("Apache configuration test failed due to ServerName warning: {Error}", error);
                        // Try to fix the ServerName issue
                        var wasFixed = await EnsureServerNameDirectiveAsync(apachePath, logger);
                        if (wasFixed)
                        {
                            logger.LogInformation("Attempting to retest Apache configuration after ServerName fix...");
                            // Retest after fix
                            return await TestApacheConfigurationAsync(apachePath, logger);
                        }
                    }
                    else
                    {
                        logger.LogError("Apache configuration test failed: {Error}", error);
                    }
                }
                
                return isValid;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing Apache configuration");
            return false;
        }
    }

    /// <summary>
    /// Restarts Apache service
    /// </summary>
    public static async Task RestartApacheServiceAsync(ILogger logger)
    {
        try
        {
            logger.LogInformation("Restarting Apache service...");
            
            // Stop Apache service
            var stopProcess = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = "stop Apache2.4",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var stopProc = Process.Start(stopProcess);
            if (stopProc != null)
            {
                await stopProc.WaitForExitAsync();
                var stopOutput = await stopProc.StandardOutput.ReadToEndAsync();
                var stopError = await stopProc.StandardError.ReadToEndAsync();
                
                logger.LogDebug("Apache stop output: {Output}, Error: {Error}", stopOutput, stopError);
            }

            // Wait a moment for the service to fully stop
            await Task.Delay(2000);

            // Start Apache service
            var startProcess = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = "start Apache2.4",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var startProc = Process.Start(startProcess);
            if (startProc != null)
            {
                await startProc.WaitForExitAsync();
                var startOutput = await startProc.StandardOutput.ReadToEndAsync();
                var startError = await startProc.StandardError.ReadToEndAsync();
                
                if (startProc.ExitCode == 0)
                {
                    logger.LogInformation("Apache service restarted successfully");
                }
                else
                {
                    logger.LogWarning("Apache service restart completed with warnings. Output: {Output}, Error: {Error}", startOutput, startError);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error restarting Apache service");
            // Don't throw - this is not critical for configuration fix
        }
    }

    /// <summary>
    /// Fixes FCGID directives in virtual host content by wrapping them in IfModule blocks
    /// </summary>
    public static string FixFCGIDDirectivesInVirtualHost(string content)
    {
        var lines = content.Split('\n').ToList();
        var newLines = new List<string>();
        bool inFcgiBlock = false;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("Fcgid", StringComparison.OrdinalIgnoreCase) 
                && !line.Contains("<IfModule"))
            {
                if (!inFcgiBlock)
                {
                    newLines.Add("    # FCGID Configuration (only if mod_fcgid is loaded)");
                    newLines.Add("    <IfModule mod_fcgid.c>");
                    inFcgiBlock = true;
                }
                newLines.Add("        " + line.Trim());
            }
            else
            {
                if (inFcgiBlock && !line.TrimStart().StartsWith("Fcgid"))
                {
                    newLines.Add("    </IfModule>");
                    inFcgiBlock = false;
                }
                newLines.Add(line);
            }
        }

        if (inFcgiBlock)
            newLines.Add("    </IfModule>");

        // Wrap AddHandler as well
        var result = string.Join("\n", newLines);
        return result.Replace("        AddHandler fcgid-script .php",
                             "        <IfModule mod_fcgid.c>\n            AddHandler fcgid-script .php\n        </IfModule>");
    }
}
