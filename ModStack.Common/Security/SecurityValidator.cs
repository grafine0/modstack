using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ModStack.Common.Security
{
    /// <summary>
    /// Provides centralized security validation for hostnames, file paths, download URLs, and cryptographic digests.
    /// </summary>
    public static class SecurityValidator
    {
        private const int MaxHostnameLength = 253;
        private const int MaxLabelLength = 63;

        /// <summary>
        /// Validates whether a string is a safe, RFC 1123 compliant hostname suitable for virtual host and certificate configuration.
        /// </summary>
        public static bool IsValidHostname(string? hostname, bool allowSingleLabelLocalhost = false)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                return false;

            if (hostname.Length > MaxHostnameLength)
                return false;

            // Reject any whitespace, control characters, path separators, quotes, or shell metacharacters
            foreach (char c in hostname)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c) ||
                    c is '/' or '\\' or ':' or '"' or '\'' or '`' or ';' or '$' or '&' or '|' or '<' or '>' or '\0')
                {
                    return false;
                }
            }

            if (allowSingleLabelLocalhost && string.Equals(hostname, "localhost", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!hostname.Contains('.'))
                return false;

            var labels = hostname.Split('.');
            if (labels.Length < 2)
                return false;

            foreach (var label in labels)
            {
                if (string.IsNullOrEmpty(label) || label.Length > MaxLabelLength)
                    return false;

                // Labels must start and end with an ASCII letter or digit
                if (!char.IsAsciiLetterOrDigit(label[0]) || !char.IsAsciiLetterOrDigit(label[^1]))
                    return false;

                // Interior characters may only be ASCII letters, digits, or hyphens
                foreach (char c in label)
                {
                    if (!char.IsAsciiLetterOrDigit(c) && c != '-')
                        return false;
                }
            }

            // Final TLD label must not be all-numeric
            var tld = labels[^1];
            if (tld.All(char.IsAsciiDigit))
                return false;

            return true;
        }

        /// <summary>
        /// Validates a hostname or throws an <see cref="ArgumentException"/> if invalid.
        /// </summary>
        public static void ValidateHostnameOrThrow(string? hostname, string paramName = "domain", bool allowSingleLabelLocalhost = false)
        {
            if (!IsValidHostname(hostname, allowSingleLabelLocalhost))
            {
                throw new ArgumentException($"Invalid or unsafe hostname '{hostname}'.", paramName);
            }
        }

        /// <summary>
        /// Checks whether a relative archive or configuration path contains rooted or parent-directory traversal segments.
        /// </summary>
        public static bool ContainsPathTraversalOrRootedSegments(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return true;

            if (path.Contains('\0') || path.Contains(':'))
                return true;

            if (path.StartsWith('/') || path.StartsWith('\\') || Path.IsPathRooted(path))
                return true;

            var segments = path.Split('/', '\\');
            foreach (var segment in segments)
            {
                if (segment == "..")
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves a candidate relative path against a root directory and ensures the canonical destination resides strictly within the root.
        /// </summary>
        public static string ValidateSafePathWithinRoot(string rootDirectory, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("Root directory cannot be empty.", nameof(rootDirectory));

            if (ContainsPathTraversalOrRootedSegments(relativePath))
                throw new InvalidOperationException($"Unsafe relative path rejected: '{relativePath}'.");

            var canonicalRoot = Path.GetFullPath(rootDirectory);
            if (!canonicalRoot.EndsWith(Path.DirectorySeparatorChar))
            {
                canonicalRoot += Path.DirectorySeparatorChar;
            }

            var combinedPath = Path.Combine(canonicalRoot, relativePath);
            var canonicalDestination = Path.GetFullPath(combinedPath);

            if (!canonicalDestination.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase) ||
                canonicalDestination.Length <= canonicalRoot.Length)
            {
                throw new InvalidOperationException(
                    $"Path traversal detected: '{relativePath}' resolves outside the target directory '{canonicalRoot}'.");
            }

            return canonicalDestination;
        }

        /// <summary>
        /// Validates that a download URL is a well-formed absolute HTTPS URI.
        /// </summary>
        public static Uri ValidateHttpsUrlOrThrow(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException($"Invalid download URL format: '{url}'.");
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Insecure download transport '{uri.Scheme}' rejected for URL '{url}'. HTTPS is required.");
            }

            return uri;
        }

        /// <summary>
        /// Computes the SHA-256 hex digest of a file and verifies it against an expected digest.
        /// </summary>
        public static async Task<string> VerifyFileSha256Async(
            string filePath,
            string? expectedChecksum,
            bool requireChecksum = false,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found for integrity verification.", filePath);

            var normalizedExpected = NormalizeChecksum(expectedChecksum);
            if (string.IsNullOrEmpty(normalizedExpected))
            {
                if (requireChecksum)
                {
                    throw new InvalidOperationException(
                        $"Integrity verification failed for '{Path.GetFileName(filePath)}': a trusted SHA-256 checksum is required but was not provided.");
                }
            }

            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
            var actualHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

            if (!string.IsNullOrEmpty(normalizedExpected) &&
                !string.Equals(actualHex, normalizedExpected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"SHA-256 integrity mismatch for '{Path.GetFileName(filePath)}'. Expected: {normalizedExpected}, Actual: {actualHex}.");
            }

            return actualHex;
        }

        private static string NormalizeChecksum(string? checksum)
        {
            if (string.IsNullOrWhiteSpace(checksum))
                return string.Empty;

            var trimmed = checksum.Trim();
            if (trimmed.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["sha256:".Length..].Trim();
            }

            return trimmed.ToLowerInvariant();
        }
    }
}

