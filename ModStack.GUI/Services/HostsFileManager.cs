using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using ModStack.GUI.Services;

namespace ModStack.GUI.Services
{
    /// <summary>
    /// Manages Windows hosts file entries for local development domains
    /// </summary>
    public class HostsFileManager
    {
        private readonly ILogger<HostsFileManager> _logger;

        public HostsFileManager(ILogger<HostsFileManager> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Adds a domain to the hosts file pointing to 127.0.0.1
        /// </summary>
        public async Task<bool> AddDomainAsync(string domain)
        {
            try
            {
                var hostsPath = GetHostsFilePath();
                var hostEntry = $"127.0.0.1\t{domain}";
                
                LogService.Info($"🌐 Adding domain '{domain}' to hosts file");
                LogService.Info($"📁 Hosts file path: {hostsPath}");
                LogService.Info($"📝 Entry to add: {hostEntry}");
                
                // Check if entry already exists
                if (File.Exists(hostsPath))
                {
                    var existingContent = await File.ReadAllTextAsync(hostsPath);
                    LogService.Info($"📄 Current hosts file content length: {existingContent.Length} characters");
                    
                    // Use precise domain checking instead of Contains
                    var domainExists = await DomainExistsAsync(domain);
                    if (domainExists)
                    {
                        LogService.Info($"ℹ️ Domain '{domain}' already exists in hosts file - no action needed");
                        return true; // Domain already exists, consider this success
                    }
                    else
                    {
                        LogService.Info($"ℹ️ Domain '{domain}' not found in existing hosts file, will add it");
                    }
                }
                else
                {
                    LogService.Warning($"⚠️ Hosts file does not exist at: {hostsPath}");
                }
                
                // Try to add the entry
                try
                {
                    await File.AppendAllTextAsync(hostsPath, $"\n{hostEntry}\n");
                    
                    // Verify the entry was actually added
                    var verifyContent = await File.ReadAllTextAsync(hostsPath);
                    LogService.Info($"📄 Hosts file content after write length: {verifyContent.Length} characters");
                    
                    if (verifyContent.Contains(domain))
                    {
                        LogService.Info($"✅ Successfully added '{domain}' to hosts file and verified");
                        return true;
                    }
                    else
                    {
                        LogService.Error($"❌ Failed to add '{domain}' to hosts file - entry not found after write");
                        LogService.Error($"📄 Current hosts file content: {verifyContent}");
                        return false;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // If we don't have permission, try to run with elevated privileges
                    LogService.Warning($"⚠️ Access denied, trying elevated privileges for '{domain}'");
                    return await AddDomainElevatedAsync(domain);
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"❌ Error adding '{domain}' to hosts file: {ex.Message}");
                _logger?.LogError(ex, "Failed to add domain {Domain} to hosts file", domain);
                
                // Show user-friendly error dialog
                ModernDialogService.Show(
                    message: $"Could not add '{domain}' to hosts file automatically.",
                    title: "Hosts File Update Required",
                    buttons: System.Windows.MessageBoxButton.OK,
                    icon: System.Windows.MessageBoxImage.Information,
                    details: GetManualInstructionsText(domain, ex.Message)
                );
                
                return false;
            }
        }

        /// <summary>
        /// Removes a domain from the hosts file
        /// </summary>
        public async Task<bool> RemoveDomainAsync(string domain)
        {
            try
            {
                Debug.WriteLine($"🗑️ Removing domain '{domain}' from hosts file");
                
                var hostsPath = GetHostsFilePath();
                
                if (!File.Exists(hostsPath))
                {
                    Debug.WriteLine($"⚠️ Hosts file not found, nothing to remove for '{domain}'");
                    return true;
                }
                
                var content = await File.ReadAllTextAsync(hostsPath);
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var updatedLines = new List<string>();
                
                bool domainRemoved = false;
                
                foreach (var line in lines)
                {
                    // Skip lines that contain the domain we want to remove
                    if (line.Contains(domain) && (line.StartsWith("127.0.0.1") || line.StartsWith("::1")))
                    {
                        Debug.WriteLine($"🗑️ Removing line: {line}");
                        domainRemoved = true;
                        continue;
                    }
                    
                    updatedLines.Add(line);
                }
                
                if (domainRemoved)
                {
                    var updatedContent = string.Join(Environment.NewLine, updatedLines);
                    await File.WriteAllTextAsync(hostsPath, updatedContent);
                    Debug.WriteLine($"✅ Successfully removed '{domain}' from hosts file");
                }
                else
                {
                    Debug.WriteLine($"ℹ️ Domain '{domain}' was not found in hosts file");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error removing '{domain}' from hosts file: {ex.Message}");
                _logger?.LogError(ex, "Failed to remove domain {Domain} from hosts file", domain);
                return false;
            }
        }

        /// <summary>
        /// Checks if a domain exists in the hosts file
        /// </summary>
        public async Task<bool> DomainExistsAsync(string domain)
        {
            try
            {
                var hostsPath = GetHostsFilePath();
                
                if (!File.Exists(hostsPath))
                {
                    return false;
                }
                
                var content = await File.ReadAllTextAsync(hostsPath);
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Skip comments and empty lines
                    if (trimmedLine.StartsWith("#") || string.IsNullOrWhiteSpace(trimmedLine))
                        continue;
                    
                    // Look for lines that start with 127.0.0.1 or ::1
                    if (trimmedLine.StartsWith("127.0.0.1") || trimmedLine.StartsWith("::1"))
                    {
                        var parts = trimmedLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            var hostname = parts[1];
                            if (hostname.Equals(domain, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                LogService.Error($"❌ Error checking domain '{domain}' in hosts file: {ex.Message}");
                return false;
            }
        }

        private string GetHostsFilePath()
        {
            return Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");
        }

        private async Task<bool> AddDomainElevatedAsync(string domain)
        {
            string? tempBatFile = null;
            try
            {
                LogService.Info($"🔐 Attempting elevated hosts file update for '{domain}'");
                
                // Create a temporary batch file to add the hosts entry
                tempBatFile = Path.GetTempFileName() + ".bat";
                var hostEntry = $"127.0.0.1\t{domain}";
                var batchContent = $@"@echo off
echo {hostEntry} >> ""%SystemRoot%\System32\drivers\etc\hosts""
";

                await File.WriteAllTextAsync(tempBatFile, batchContent);

                // Run the batch file with elevated privileges
                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{tempBatFile}\"",
                    UseShellExecute = true,
                    Verb = "runas", // This requests elevation
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        // Verify the entry was actually added
                        var hostsPath = GetHostsFilePath();
                        var verifyContent = await File.ReadAllTextAsync(hostsPath);
                        if (verifyContent.Contains(domain))
                        {
                            LogService.Info($"✅ Successfully added '{domain}' to hosts file with elevation and verified");
                            return true;
                        }
                        else
                        {
                            LogService.Error($"❌ Elevated hosts file update failed - entry not found after write");
                            LogService.Error($"📄 Hosts file content after elevated write: {verifyContent}");
                            return false;
                        }
                    }
                    else
                    {
                        LogService.Error($"❌ Elevated hosts file update failed with exit code: {process.ExitCode}");
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LogService.Error($"❌ Error in elevated hosts file update: {ex.Message}");
                return false;
            }
            finally
            {
                // Clean up temporary batch file securely
                if (!string.IsNullOrEmpty(tempBatFile))
                {
                    await SecureDeleteFileAsync(tempBatFile);
                }
            }
        }

        /// <summary>
        /// Securely deletes a file by overwriting it with random data before deletion
        /// </summary>
        private async Task SecureDeleteFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                // Overwrite file with random data multiple times for security
                var fileInfo = new FileInfo(filePath);
                var buffer = new byte[fileInfo.Length];
                
                // Overwrite 3 times with random data
                for (int i = 0; i < 3; i++)
                {
                    System.Security.Cryptography.RandomNumberGenerator.Fill(buffer);
                    await File.WriteAllBytesAsync(filePath, buffer);
                }

                // Finally delete the file
                File.Delete(filePath);
                Debug.WriteLine($"🔒 Securely deleted temporary file: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Could not securely delete file {filePath}: {ex.Message}");
                // Fallback to regular deletion
                try
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
                catch
                {
                    Debug.WriteLine($"❌ Failed to delete file {filePath} even with fallback method");
                }
            }
        }

        private string GetManualInstructionsText(string domain, string errorMessage)
        {
            return $@"Add this line to your hosts file:
127.0.0.1	{domain}

Location: C:\Windows\System32\drivers\etc\hosts

📝 Instructions:
1. Open Notepad as administrator
2. Open the hosts file from the location above
3. Add the line shown above
4. Save the file

Error: {errorMessage}";
        }

        /// <summary>
        /// Validates that a domain name is suitable for hosts file usage
        /// </summary>
        public bool IsValidDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return false;

            // Basic domain validation
            if (domain.Length > 253)
                return false;

            // Check for invalid characters
            var invalidChars = new[] { ' ', '\t', '\r', '\n', '#' };
            if (domain.IndexOfAny(invalidChars) >= 0)
                return false;

            // Must contain at least one dot for local development domains
            if (!domain.Contains('.'))
                return false;

            return true;
        }

        /// <summary>
        /// Gets a list of all custom domains in the hosts file (excluding system entries)
        /// </summary>
        public async Task<List<string>> GetCustomDomainsAsync()
        {
            var customDomains = new List<string>();
            
            try
            {
                var hostsPath = GetHostsFilePath();
                
                if (!File.Exists(hostsPath))
                {
                    return customDomains;
                }
                
                var content = await File.ReadAllTextAsync(hostsPath);
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Skip comments and empty lines
                    if (trimmedLine.StartsWith("#") || string.IsNullOrWhiteSpace(trimmedLine))
                        continue;
                    
                    // Look for lines that start with 127.0.0.1 (local development domains)
                    if (trimmedLine.StartsWith("127.0.0.1"))
                    {
                        var parts = trimmedLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            var domain = parts[1];
                            
                            // Skip localhost and other system domains
                            if (domain != "localhost" && !domain.EndsWith(".local") && domain.Contains('.'))
                            {
                                customDomains.Add(domain);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error reading custom domains from hosts file: {ex.Message}");
            }
            
            return customDomains.Distinct().ToList();
        }
    }
}
