using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace ModStack.Common.Security
{
    /// <summary>
    /// Options for controlling archive extraction safety limits.
    /// </summary>
    public sealed class ArchiveExtractionOptions
    {
        public int MaxEntries { get; set; } = 50_000;
        public long MaxTotalUncompressedBytes { get; set; } = 2L * 1024 * 1024 * 1024; // 2 GB
        public double MaxCompressionRatio { get; set; } = 100.0;
        public long CompressionRatioCheckThresholdBytes { get; set; } = 10L * 1024 * 1024; // 10 MB
        public Func<string, string>? EntryPathTransformer { get; set; }
        public Action<int, int>? ProgressCallback { get; set; }
    }

    /// <summary>
    /// Extracts ZIP archives safely using an isolated staging directory, canonical path containment checks,
    /// duplicate destination detection, and resource quotas to prevent Zip Slip and Zip Bomb attacks.
    /// </summary>
    public static class SafeArchiveExtractor
    {
        public static async Task ExtractZipSafelyAsync(
            string zipPath,
            string destinationDirectory,
            ArchiveExtractionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                throw new FileNotFoundException("Archive file not found.", zipPath);

            if (string.IsNullOrWhiteSpace(destinationDirectory))
                throw new ArgumentException("Destination directory cannot be empty.", nameof(destinationDirectory));

            options ??= new ArchiveExtractionOptions();

            var canonicalDestinationRoot = Path.GetFullPath(destinationDirectory);
            var parentDir = Path.GetDirectoryName(canonicalDestinationRoot) ?? Path.GetTempPath();
            Directory.CreateDirectory(parentDir);

            var stagingDirectory = Path.Combine(parentDir, $".modstack_stage_{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDirectory);

            try
            {
                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(zipPath);
                    var totalEntries = archive.Entries.Count;

                    if (totalEntries > options.MaxEntries)
                    {
                        throw new InvalidOperationException(
                            $"Archive contains {totalEntries} entries, exceeding the maximum allowed limit of {options.MaxEntries}.");
                    }

                    var seenDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    long totalBytesExtracted = 0;
                    int processedEntries = 0;
                    var buffer = new byte[81920];

                    foreach (var entry in archive.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var rawEntryPath = entry.FullName;

                        // Always validate raw archive path before any transformation
                        if (SecurityValidator.ContainsPathTraversalOrRootedSegments(rawEntryPath))
                        {
                            throw new InvalidOperationException(
                                $"Malicious or unsafe archive entry path rejected: '{rawEntryPath}'.");
                        }

                        // Skip directory entries after validating their path
                        if (string.IsNullOrEmpty(entry.Name) || rawEntryPath.EndsWith('/') || rawEntryPath.EndsWith('\\'))
                        {
                            processedEntries++;
                            options.ProgressCallback?.Invoke(processedEntries, totalEntries);
                            continue;
                        }

                        var effectiveRelativePath = options.EntryPathTransformer != null
                            ? options.EntryPathTransformer(rawEntryPath)
                            : rawEntryPath;

                        if (string.IsNullOrWhiteSpace(effectiveRelativePath))
                        {
                            processedEntries++;
                            options.ProgressCallback?.Invoke(processedEntries, totalEntries);
                            continue;
                        }

                        // Validate canonical path within staging root
                        var stagedFilePath = SecurityValidator.ValidateSafePathWithinRoot(stagingDirectory, effectiveRelativePath);

                        // Detect duplicate normalized destinations
                        if (!seenDestinations.Add(stagedFilePath))
                        {
                            throw new InvalidOperationException(
                                $"Duplicate normalized archive destination path detected: '{effectiveRelativePath}'.");
                        }

                        // Check declared size and compression ratio
                        if (entry.Length < 0)
                        {
                            throw new InvalidOperationException($"Archive entry '{rawEntryPath}' has an invalid negative length.");
                        }

                        if (totalBytesExtracted + entry.Length > options.MaxTotalUncompressedBytes)
                        {
                            throw new InvalidOperationException(
                                $"Archive extraction aborted: total uncompressed size exceeds the {options.MaxTotalUncompressedBytes / (1024 * 1024)} MB quota.");
                        }

                        if (entry.Length >= options.CompressionRatioCheckThresholdBytes && entry.CompressedLength > 0)
                        {
                            var ratio = (double)entry.Length / entry.CompressedLength;
                            if (ratio > options.MaxCompressionRatio)
                            {
                                throw new InvalidOperationException(
                                    $"Archive entry '{rawEntryPath}' exceeds maximum compression ratio ({ratio:F1}x > {options.MaxCompressionRatio:F1}x).");
                            }
                        }

                        var stagedFileDir = Path.GetDirectoryName(stagedFilePath);
                        if (!string.IsNullOrEmpty(stagedFileDir))
                        {
                            Directory.CreateDirectory(stagedFileDir);
                        }

                        // Stream extraction with runtime byte enforcement to guard against spoofed ZIP headers
                        using (var entryStream = entry.Open())
                        using (var outputStream = new FileStream(stagedFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            long entryBytesWritten = 0;
                            int bytesRead;
                            while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                entryBytesWritten += bytesRead;
                                totalBytesExtracted += bytesRead;

                                if (totalBytesExtracted > options.MaxTotalUncompressedBytes)
                                {
                                    throw new InvalidOperationException(
                                        $"Archive extraction aborted: stream exceeded maximum uncompressed size quota ({options.MaxTotalUncompressedBytes} bytes).");
                                }

                                if (entryBytesWritten >= options.CompressionRatioCheckThresholdBytes && entry.CompressedLength > 0)
                                {
                                    var actualRatio = (double)entryBytesWritten / entry.CompressedLength;
                                    if (actualRatio > options.MaxCompressionRatio)
                                    {
                                        throw new InvalidOperationException(
                                            $"Archive entry '{rawEntryPath}' stream exceeded maximum compression ratio ({actualRatio:F1}x > {options.MaxCompressionRatio:F1}x).");
                                    }
                                }

                                outputStream.Write(buffer, 0, bytesRead);
                            }
                        }

                        processedEntries++;
                        options.ProgressCallback?.Invoke(processedEntries, totalEntries);
                    }
                }, cancellationToken);

                // Promote staged files to canonicalDestinationRoot
                PromoteStagingDirectory(stagingDirectory, canonicalDestinationRoot);
            }
            finally
            {
                if (Directory.Exists(stagingDirectory))
                {
                    try
                    {
                        Directory.Delete(stagingDirectory, recursive: true);
                    }
                    catch
                    {
                        // Best-effort cleanup of temporary staging directory
                    }
                }
            }
        }

        private static void PromoteStagingDirectory(string stagingDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (var dirPath in Directory.GetDirectories(stagingDirectory, "*", SearchOption.AllDirectories))
            {
                var relativeDir = Path.GetRelativePath(stagingDirectory, dirPath);
                var targetDir = SecurityValidator.ValidateSafePathWithinRoot(destinationDirectory, relativeDir);
                Directory.CreateDirectory(targetDir);
            }

            foreach (var filePath in Directory.GetFiles(stagingDirectory, "*", SearchOption.AllDirectories))
            {
                var relativeFile = Path.GetRelativePath(stagingDirectory, filePath);
                var targetFile = SecurityValidator.ValidateSafePathWithinRoot(destinationDirectory, relativeFile);
                var targetFileDir = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetFileDir))
                {
                    Directory.CreateDirectory(targetFileDir);
                }

                File.Move(filePath, targetFile, overwrite: true);
            }
        }
    }
}

