using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModStack.Common;
using ModStack.Common.Models;
using ModStack.Common.Security;
using ModStack.Core.Interfaces;

namespace ModStack.Core.Services;

public class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService> _logger;

    public UpdateService(HttpClient httpClient, ILogger<UpdateService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<UpdateInfo> CheckForUpdatesAsync()
    {
        try
        {
            _logger.LogInformation("Checking for application updates...");

            // Return current version as up-to-date until a signed production update feed is configured
            var info = new UpdateInfo
            {
                IsUpdateAvailable = false,
                LatestVersion = Constants.ApplicationVersion,
                DownloadUrl = string.Empty,
                Checksum = string.Empty,
                ReleaseNotes = "You are running the latest version of ModStack."
            };

            return Task.FromResult(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            return Task.FromResult(new UpdateInfo { IsUpdateAvailable = false });
        }
    }

    public async Task DownloadUpdateAsync(string downloadUrl, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var uri = SecurityValidator.ValidateHttpsUrlOrThrow(downloadUrl);
        _logger.LogInformation("Starting verified update download from {DownloadUrl}", uri);

        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName) || SecurityValidator.ContainsPathTraversalOrRootedSegments(fileName))
        {
            fileName = $"ModStack-Update-{Guid.NewGuid():N}.exe";
        }

        var downloadPath = SecurityValidator.ValidateSafePathWithinRoot(Path.GetTempPath(), fileName);

        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.RequestMessage?.RequestUri is { } finalUri &&
            !string.Equals(finalUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Insecure redirect from HTTPS to '{finalUri.Scheme}' blocked.");
        }

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            long downloadedBytes = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    progress?.Report((double)downloadedBytes / totalBytes * 100.0);
                }
            }

            await fileStream.FlushAsync(cancellationToken);
        }

        var actualLength = new FileInfo(downloadPath).Length;
        if (totalBytes > 0 && actualLength != totalBytes)
        {
            File.Delete(downloadPath);
            throw new InvalidOperationException($"Update download size mismatch: expected {totalBytes} bytes, got {actualLength} bytes.");
        }

        _logger.LogInformation("Update downloaded to {DownloadPath}", downloadPath);
    }

    public void InstallUpdate(string installerPath)
    {
        if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
        {
            throw new FileNotFoundException("Update installer file was not found.", installerPath);
        }

        // Verify the installer has an Authenticode signature before executing
        try
        {
            using var signerCert = X509Certificate.CreateFromSignedFile(installerPath);
            using var cert2 = new X509Certificate2(signerCert);
            if (cert2.NotAfter <= DateTime.Now)
            {
                throw new InvalidOperationException("Update installer signature certificate has expired.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refusing to execute unsigned or invalidly signed update installer: {InstallerPath}", installerPath);
            throw new InvalidOperationException("Update installer failed Authenticode signature verification.", ex);
        }

        _logger.LogInformation("Starting verified update installer from {InstallerPath}", installerPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        });
    }
}
