using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using ModStack.Common.Security;
using ModStack.Data.Migrations;
using Xunit;

namespace ModStack.Data.Tests.Security;

public class SecurityHardeningTests : IDisposable
{
    private readonly string _tempRoot;

    public SecurityHardeningTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"modstack_sec_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("..\\outside.txt")]
    [InlineData("subdir/../../outside.txt")]
    [InlineData("/absolute/path.txt")]
    [InlineData("\\Windows\\System32\\evil.dll")]
    [InlineData("C:\\evil.exe")]
    public async Task ExtractZipSafelyAsync_WithTraversalOrRootedEntries_ShouldRejectAndLeaveTargetClean(string maliciousEntryName)
    {
        var zipPath = Path.Combine(_tempRoot, "malicious.zip");
        var extractDir = Path.Combine(_tempRoot, "extracted");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry(maliciousEntryName);
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("malicious payload");
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SafeArchiveExtractor.ExtractZipSafelyAsync(zipPath, extractDir));

        Assert.False(Directory.Exists(extractDir) && Directory.EnumerateFileSystemEntries(extractDir).Any());
    }

    [Fact]
    public async Task ExtractZipSafelyAsync_WithDuplicateNormalizedDestinations_ShouldThrow()
    {
        var zipPath = Path.Combine(_tempRoot, "duplicate.zip");
        var extractDir = Path.Combine(_tempRoot, "extracted_dup");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry1 = archive.CreateEntry("pkg-1.0/bin/app.exe");
            await using (var writer = new StreamWriter(entry1.Open()))
            {
                await writer.WriteAsync("first");
            }

            var entry2 = archive.CreateEntry("pkg-2.0/bin/app.exe");
            await using (var writer = new StreamWriter(entry2.Open()))
            {
                await writer.WriteAsync("second");
            }
        }

        var options = new ArchiveExtractionOptions
        {
            // Transformer flattens both entries to "bin/app.exe"
            EntryPathTransformer = path => string.Join(Path.DirectorySeparatorChar, path.Split('/').Skip(1))
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SafeArchiveExtractor.ExtractZipSafelyAsync(zipPath, extractDir, options));
    }

    [Fact]
    public async Task ExtractZipSafelyAsync_ExceedingMaxEntriesQuota_ShouldThrow()
    {
        var zipPath = Path.Combine(_tempRoot, "too_many_entries.zip");
        var extractDir = Path.Combine(_tempRoot, "extracted_quota");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            for (int i = 0; i < 5; i++)
            {
                var entry = archive.CreateEntry($"file_{i}.txt");
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync("data");
            }
        }

        var options = new ArchiveExtractionOptions { MaxEntries = 3 };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SafeArchiveExtractor.ExtractZipSafelyAsync(zipPath, extractDir, options));
    }

    [Fact]
    public async Task ExtractZipSafelyAsync_ExceedingUncompressedByteQuota_ShouldThrow()
    {
        var zipPath = Path.Combine(_tempRoot, "oversized.zip");
        var extractDir = Path.Combine(_tempRoot, "extracted_size");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("large.bin");
            await using var stream = entry.Open();
            var bytes = new byte[4096];
            await stream.WriteAsync(bytes);
        }

        var options = new ArchiveExtractionOptions { MaxTotalUncompressedBytes = 1024 };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SafeArchiveExtractor.ExtractZipSafelyAsync(zipPath, extractDir, options));
    }

    [Fact]
    public async Task ExtractZipSafelyAsync_WithValidArchive_ShouldExtractAndPromoteFiles()
    {
        var zipPath = Path.Combine(_tempRoot, "valid.zip");
        var extractDir = Path.Combine(_tempRoot, "extracted_valid");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("conf/httpd.conf");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("ServerRoot \"/apache\"");
        }

        await SafeArchiveExtractor.ExtractZipSafelyAsync(zipPath, extractDir);

        var extractedFile = Path.Combine(extractDir, "conf", "httpd.conf");
        Assert.True(File.Exists(extractedFile));
        Assert.Equal("ServerRoot \"/apache\"", await File.ReadAllTextAsync(extractedFile));
    }

    [Fact]
    public async Task VerifyFileSha256Async_WithMatchingAndMismatchingHashes_ShouldBehaveCorrectly()
    {
        var filePath = Path.Combine(_tempRoot, "artifact.bin");
        var content = Encoding.UTF8.GetBytes("verified-package-payload");
        await File.WriteAllBytesAsync(filePath, content);

        var expectedHex = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        var actualHex = await SecurityValidator.VerifyFileSha256Async(filePath, expectedHex, requireChecksum: true);
        Assert.Equal(expectedHex, actualHex);

        var prefixedHex = await SecurityValidator.VerifyFileSha256Async(filePath, $"sha256:{expectedHex.ToUpperInvariant()}", requireChecksum: true);
        Assert.Equal(expectedHex, prefixedHex);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SecurityValidator.VerifyFileSha256Async(filePath, new string('0', 64), requireChecksum: true));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SecurityValidator.VerifyFileSha256Async(filePath, null, requireChecksum: true));
    }

    [Theory]
    [InlineData("http://nginx.org/download/nginx-1.24.0.zip")]
    [InlineData("ftp://files.example.com/archive.zip")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("not-a-valid-url")]
    [InlineData("")]
    public void ValidateHttpsUrlOrThrow_WithInsecureOrInvalidUrls_ShouldThrow(string invalidUrl)
    {
        Assert.Throws<InvalidOperationException>(() => SecurityValidator.ValidateHttpsUrlOrThrow(invalidUrl));
    }

    [Fact]
    public void ValidateHttpsUrlOrThrow_WithValidHttpsUrl_ShouldReturnUri()
    {
        var uri = SecurityValidator.ValidateHttpsUrlOrThrow("https://nginx.org/download/nginx-1.24.0.zip");
        Assert.Equal("https", uri.Scheme);
        Assert.Equal("nginx.org", uri.Host);
    }

    [Theory]
    [InlineData("myapp.local", true)]
    [InlineData("api.sub-domain.test", true)]
    [InlineData("project123.dev", true)]
    [InlineData("localhost", false)]
    [InlineData("../evil.local", false)]
    [InlineData("app.local\nServerAlias evil.local", false)]
    [InlineData("app.local'; Start-Process calc; '", false)]
    [InlineData("app.local/../../conf", false)]
    [InlineData("-leadinghyphen.local", false)]
    [InlineData("trailinghyphen-.local", false)]
    [InlineData("double..dot.local", false)]
    [InlineData("numeric.123", false)]
    [InlineData("has space.local", false)]
    public void IsValidHostname_ShouldEnforceStrictRfcValidation(string candidate, bool expectedValid)
    {
        Assert.Equal(expectedValid, SecurityValidator.IsValidHostname(candidate));
    }

    [Theory]
    [InlineData("../php_evil.dll")]
    [InlineData("..\\php_evil.dll")]
    [InlineData("C:\\Windows\\System32\\evil.dll")]
    [InlineData("/etc/evil.so")]
    public void ValidateSafePathWithinRoot_WithUnsafeExtensionDllName_ShouldThrow(string unsafeDllName)
    {
        var extDir = Path.Combine(_tempRoot, "php", "ext");
        Directory.CreateDirectory(extDir);

        Assert.Throws<InvalidOperationException>(
            () => SecurityValidator.ValidateSafePathWithinRoot(extDir, unsafeDllName));
    }

    [Fact]
    public void DotNetCertificateAndPfxExtraction_ShouldExportPrivateKeyAndCertificateToPem()
    {
        var domain = "secure-test.local";
        SecurityValidator.ValidateHostnameOrThrow(domain);

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={domain}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(domain);
        req.CertificateExtensions.Add(sanBuilder.Build());

        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(365));
        var password = Guid.NewGuid().ToString("N");
        var pfxPath = Path.Combine(_tempRoot, "test.pfx");
        File.WriteAllBytes(pfxPath, cert.Export(X509ContentType.Pfx, password));

        using var loaded = new X509Certificate2(
            pfxPath,
            password,
            X509KeyStorageFlags.Exportable);

        Assert.True(loaded.HasPrivateKey);
        using var privateKey = loaded.GetRSAPrivateKey();
        Assert.NotNull(privateKey);

        var pbeParams = new PbeParameters(
            PbeEncryptionAlgorithm.Aes256Cbc,
            HashAlgorithmName.SHA256,
            10000);
        var encryptedPkcs8 = privateKey!.ExportEncryptedPkcs8PrivateKey(password, pbeParams);
        using var exportableRsa = RSA.Create();
        exportableRsa.ImportEncryptedPkcs8PrivateKey(password, encryptedPkcs8, out _);

        var keyBytes = exportableRsa.ExportRSAPrivateKey();
        Assert.NotEmpty(keyBytes);
    }

    [Fact]
    public async Task Migration015_AddPhp85Version_ShouldInsertPhp85AndUpdateExtensionsIdempotently()
    {
        var dbPath = Path.Combine(_tempRoot, "migration015.db");
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        await connection.ExecuteAsync(@"
            CREATE TABLE Packages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                Category TEXT NOT NULL
            );
            CREATE TABLE PackageVersions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PackageId INTEGER NOT NULL,
                Version TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                ServiceName TEXT,
                ReleaseDate DATETIME,
                IsStable BOOLEAN DEFAULT TRUE,
                IsRecommended BOOLEAN DEFAULT FALSE,
                IsLTS BOOLEAN DEFAULT FALSE,
                Size INTEGER DEFAULT 0,
                DownloadUrls TEXT,
                UNIQUE(PackageId, Version)
            );
            CREATE TABLE PHPExtensions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                Category TEXT NOT NULL,
                SupportedPHPVersions TEXT NOT NULL,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            INSERT INTO Packages (Name, DisplayName, Category) VALUES ('php', 'PHP', 'Programming Language');
            INSERT INTO PHPExtensions (Name, DisplayName, Category, SupportedPHPVersions)
            VALUES ('curl', 'cURL', 'WebServices', '[""7.4"",""8.0"",""8.1"",""8.2"",""8.3"",""8.4""]');
        ");

        var migration = new Migration015_AddPhp85Version();
        await migration.ApplyAsync(connection);
        // Apply a second time to verify idempotency
        await migration.ApplyAsync(connection);

        var versionCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM PackageVersions WHERE Version = '8.5.0' AND ServiceName = 'php-8.5.0'");
        Assert.Equal(1, versionCount);

        var supportedJson = await connection.ExecuteScalarAsync<string>(
            "SELECT SupportedPHPVersions FROM PHPExtensions WHERE Name = 'curl'");
        Assert.Contains("\"8.5\"", supportedJson);
    }
}

