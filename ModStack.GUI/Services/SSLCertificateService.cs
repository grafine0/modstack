using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ModStack.Common.Security;

namespace ModStack.GUI.Services
{
    /// <summary>
    /// Service for generating SSL certificates for local development
    /// </summary>
    public class SSLCertificateService : ISSLCertificateService
    {
        private readonly ILogger<SSLCertificateService>? _logger;

        public SSLCertificateService(ILogger<SSLCertificateService>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Checks if OpenSSL is available on the system
        /// </summary>
        /// <returns>True if OpenSSL is available, false otherwise</returns>
        public async Task<bool> IsOpenSSLAvailableAsync()
        {
            try
            {
                var openSslPaths = new[]
                {
                    @"C:\OpenSSL-Win64\bin\openssl.exe",
                    @"C:\OpenSSL-Win32\bin\openssl.exe",
                    @"C:\Program Files\OpenSSL-Win64\bin\openssl.exe",
                    @"C:\Program Files\OpenSSL-Win32\bin\openssl.exe",
                    @"C:\Program Files (x86)\OpenSSL-Win64\bin\openssl.exe",
                    @"C:\Program Files (x86)\OpenSSL-Win32\bin\openssl.exe",
                    @"C:\xampp\apache\bin\openssl.exe",
                    @"C:\wamp64\bin\openssl\openssl.exe",
                    @"C:\wamp\bin\openssl\openssl.exe",
                    @"C:\laragon\bin\openssl\openssl.exe",
                    @"C:\Bitnami\apache2\bin\openssl.exe",
                    @"openssl.exe"
                };

                foreach (var path in openSslPaths)
                {
                    if (path == "openssl.exe")
                    {
                        var pathTest = await RunProcessAsync("where", "openssl.exe");
                        if (pathTest.Success && !string.IsNullOrEmpty(pathTest.Output))
                        {
                            return true;
                        }
                    }
                    else if (File.Exists(path))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets information about available SSL certificate generation methods
        /// </summary>
        /// <returns>Dictionary of available methods and their status</returns>
        public async Task<Dictionary<string, bool>> GetAvailableGenerationMethodsAsync()
        {
            var methods = new Dictionary<string, bool>();

            // Check OpenSSL
            methods["OpenSSL"] = await IsOpenSSLAvailableAsync();

            // Check PowerShell (always available on Windows 10+)
            try
            {
                var psTest = await RunProcessAsync("powershell.exe", "-Command \"Get-Host\"");
                methods["PowerShell"] = psTest.Success;
            }
            catch
            {
                methods["PowerShell"] = false;
            }

            // .NET Cryptography is always available
            methods[".NET Cryptography"] = true;

            return methods;
        }

        /// <summary>
        /// Generates a self-signed SSL certificate for the given domain
        /// </summary>
        /// <param name="domain">The domain name for the certificate</param>
        /// <param name="certPath">Path where the certificate file should be saved</param>
        /// <param name="keyPath">Path where the private key file should be saved</param>
        /// <param name="validityDays">Number of days the certificate should be valid (default: 365)</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> GenerateSelfSignedCertificateAsync(string domain, string certPath, string keyPath, int validityDays = 365)
        {
            return await GenerateSelfSignedCertificateAsync(domain, certPath, keyPath, validityDays, null);
        }

        /// <summary>
        /// Generates a self-signed SSL certificate for the given domain with optional custom password
        /// </summary>
        /// <param name="domain">The domain name for the certificate</param>
        /// <param name="certPath">Path where the certificate file should be saved</param>
        /// <param name="keyPath">Path where the private key file should be saved</param>
        /// <param name="validityDays">Number of days the certificate should be valid (default: 365)</param>
        /// <param name="customPassword">Custom password for PFX files (if null, generates secure random password)</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> GenerateSelfSignedCertificateAsync(string domain, string certPath, string keyPath, int validityDays, string? customPassword)
        {
            try
            {
                SecurityValidator.ValidateHostnameOrThrow(domain, nameof(domain), allowSingleLabelLocalhost: true);
                if (validityDays <= 0 || validityDays > 36500)
                {
                    throw new ArgumentOutOfRangeException(nameof(validityDays), "Certificate validity must be between 1 and 36500 days.");
                }

                Debug.WriteLine($"🔐 Generating SSL certificate for domain: {domain}");
                _logger?.LogInformation("Generating self-signed SSL certificate for domain {Domain}", domain);

                // Generate a secure password if not provided
                var password = customPassword ?? GenerateSecurePassword();

                // Preferred method: Native .NET Cryptography (in-process, no external CLI or shell arguments)
                if (await TryGenerateWithDotNetAsync(domain, certPath, keyPath, validityDays, password))
                {
                    Debug.WriteLine($"✅ SSL certificate generated successfully using .NET cryptography for {domain}");
                    _logger?.LogInformation("SSL certificate generated successfully using .NET cryptography for domain {Domain}", domain);
                    return true;
                }

                // Fallback 1: OpenSSL
                if (await TryGenerateWithOpenSSLAsync(domain, certPath, keyPath, validityDays))
                {
                    Debug.WriteLine($"✅ SSL certificate generated successfully using OpenSSL for {domain}");
                    _logger?.LogInformation("SSL certificate generated successfully using OpenSSL for domain {Domain}", domain);
                    return true;
                }

                // Fallback 2: PowerShell method (Windows only, parameterized via environment & stdin)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && await TryGenerateWithPowerShellAsync(domain, certPath, keyPath, validityDays, password))
                {
                    Debug.WriteLine($"✅ SSL certificate generated successfully using PowerShell for {domain}");
                    _logger?.LogInformation("SSL certificate generated successfully using PowerShell for domain {Domain}", domain);
                    return true;
                }

                Debug.WriteLine($"❌ Failed to generate SSL certificate for {domain} using any method");
                _logger?.LogError("Failed to generate SSL certificate for domain {Domain} using any available method", domain);
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error generating SSL certificate for {domain}: {ex.Message}");
                _logger?.LogError(ex, "Error generating SSL certificate for domain {Domain}", domain);
                return false;
            }
        }

        /// <summary>
        /// Attempts to generate SSL certificate using OpenSSL
        /// </summary>
        private async Task<bool> TryGenerateWithOpenSSLAsync(string domain, string certPath, string keyPath, int validityDays)
        {
            try
            {
                Debug.WriteLine("🔍 Searching for OpenSSL installation...");
                
                // Look for OpenSSL in common locations
                var openSslPaths = new[]
                {
                    @"C:\OpenSSL-Win64\bin\openssl.exe",
                    @"C:\OpenSSL-Win32\bin\openssl.exe",
                    @"C:\Program Files\OpenSSL-Win64\bin\openssl.exe",
                    @"C:\Program Files\OpenSSL-Win32\bin\openssl.exe",
                    @"C:\Program Files (x86)\OpenSSL-Win64\bin\openssl.exe",
                    @"C:\Program Files (x86)\OpenSSL-Win32\bin\openssl.exe",
                    @"C:\xampp\apache\bin\openssl.exe",
                    @"C:\wamp64\bin\openssl\openssl.exe",
                    @"C:\wamp\bin\openssl\openssl.exe",
                    @"C:\laragon\bin\openssl\openssl.exe",
                    @"C:\Bitnami\apache2\bin\openssl.exe",
                    @"openssl.exe" // Try PATH
                };

                string? openSslPath = null;
                foreach (var path in openSslPaths)
                {
                    if (path == "openssl.exe")
                    {
                        // Test if openssl is in PATH
                        var pathTest = await RunProcessAsync("where", "openssl.exe");
                        if (pathTest.Success && !string.IsNullOrEmpty(pathTest.Output))
                        {
                            openSslPath = "openssl.exe";
                            Debug.WriteLine($"✅ Found OpenSSL in PATH: {pathTest.Output.Trim()}");
                            break;
                        }
                    }
                    else if (File.Exists(path))
                    {
                        openSslPath = path;
                        Debug.WriteLine($"✅ Found OpenSSL at: {path}");
                        break;
                    }
                }

                if (string.IsNullOrEmpty(openSslPath))
                {
                    Debug.WriteLine("⚠️ OpenSSL not found in common locations or PATH");
                    Debug.WriteLine("💡 OpenSSL installation suggestions:");
                    Debug.WriteLine("   1. Download from: https://slproweb.com/products/Win32OpenSSL.html");
                    Debug.WriteLine("   2. Install XAMPP/WAMP which includes OpenSSL");
                    Debug.WriteLine("   3. Add OpenSSL to your system PATH");
                    Debug.WriteLine("🔄 Falling back to PowerShell method...");
                    return false;
                }

                Debug.WriteLine($"🔍 Using OpenSSL at: {openSslPath}");

                // Create certificate directory if it doesn't exist
                var certDir = Path.GetDirectoryName(certPath);
                if (!string.IsNullOrEmpty(certDir) && !Directory.Exists(certDir))
                {
                    Directory.CreateDirectory(certDir);
                }

                // Generate private key
                var keyGenArgs = $"genrsa -out \"{keyPath}\" 2048";
                var keyGenResult = await RunProcessAsync(openSslPath, keyGenArgs);
                if (!keyGenResult.Success)
                {
                    Debug.WriteLine($"❌ Failed to generate private key: {keyGenResult.Error}");
                    return false;
                }

                // Create certificate signing request (CSR) config
                var csrConfigPath = Path.Combine(Path.GetTempPath(), $"{domain}_csr.conf");
                var csrConfig = $@"[req]
                    default_bits = 2048
                    prompt = no
                    default_md = sha256
                    distinguished_name = dn
                    req_extensions = v3_req

                    [dn]
                    C=US
                    ST=Development
                    L=Development
                    O=ModStack Development
                    OU=IT Department
                    CN={domain}

                    [v3_req]
                    basicConstraints = CA:FALSE
                    keyUsage = nonRepudiation, digitalSignature, keyEncipherment
                    subjectAltName = @alt_names

                    [alt_names]
                    DNS.1 = {domain}
                    DNS.2 = www.{domain}
                    DNS.3 = localhost
                    IP.1 = 127.0.0.1";

                await File.WriteAllTextAsync(csrConfigPath, csrConfig);

                // Generate certificate signing request
                var csrPath = Path.Combine(Path.GetTempPath(), $"{domain}.csr");
                var csrArgs = $"req -new -key \"{keyPath}\" -out \"{csrPath}\" -config \"{csrConfigPath}\"";
                var csrResult = await RunProcessAsync(openSslPath, csrArgs);
                if (!csrResult.Success)
                {
                    Debug.WriteLine($"❌ Failed to generate CSR: {csrResult.Error}");
                    return false;
                }

                // Generate self-signed certificate
                var certArgs = $"x509 -req -in \"{csrPath}\" -signkey \"{keyPath}\" -out \"{certPath}\" -days {validityDays} -extensions v3_req -extfile \"{csrConfigPath}\"";
                var certResult = await RunProcessAsync(openSslPath, certArgs);
                if (!certResult.Success)
                {
                    Debug.WriteLine($"❌ Failed to generate certificate: {certResult.Error}");
                    return false;
                }

                // Clean up temporary files with detailed error handling
                CleanupTemporaryFiles(new[] { csrConfigPath, csrPath });

                return File.Exists(certPath) && File.Exists(keyPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ OpenSSL method failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Attempts to generate SSL certificate using PowerShell without string interpolation
        /// </summary>
        private async Task<bool> TryGenerateWithPowerShellAsync(string domain, string certPath, string keyPath, int validityDays, string password)
        {
            try
            {
                SecurityValidator.ValidateHostnameOrThrow(domain, nameof(domain), allowSingleLabelLocalhost: true);
                Debug.WriteLine("🔍 Attempting to generate SSL certificate using PowerShell");

                // Create certificate directory if it doesn't exist
                var certDir = Path.GetDirectoryName(certPath);
                if (!string.IsNullOrEmpty(certDir) && !Directory.Exists(certDir))
                {
                    Directory.CreateDirectory(certDir);
                }

                // Create temporary PFX file path
                var tempPfxPath = Path.Combine(Path.GetTempPath(), $"cert_{Guid.NewGuid():N}.pfx");

                // Static PowerShell script: reads parameters from environment variables and password from StandardInput
                const string psScript = @"
                try {
                    $domain = $env:MODSTACK_CERT_DOMAIN
                    $days = [int]$env:MODSTACK_CERT_DAYS
                    $pfxPath = $env:MODSTACK_PFX_PATH
                    $rawPwd = [Console]::In.ReadLine()
                    if ([string]::IsNullOrEmpty($domain) -or [string]::IsNullOrEmpty($pfxPath) -or [string]::IsNullOrEmpty($rawPwd)) {
                        throw 'Missing required certificate parameters.'
                    }
                    $cert = New-SelfSignedCertificate -DnsName $domain, ""www.$domain"", 'localhost', '127.0.0.1' -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddDays($days) -KeyAlgorithm RSA -KeyLength 2048 -KeyUsage DigitalSignature, KeyEncipherment -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.1,1.3.6.1.5.5.7.3.2')
                    $pwd = ConvertTo-SecureString -String $rawPwd -Force -AsPlainText
                    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pwd -Force | Out-Null
                    $certStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('My', 'CurrentUser')
                    $certStore.Open('ReadWrite')
                    $certStore.Remove($cert)
                    $certStore.Close()
                    Write-Output 'SUCCESS'
                } catch {
                    Write-Error $_.Exception.Message
                    exit 1
                }";

                var envVars = new Dictionary<string, string>
                {
                    ["MODSTACK_CERT_DOMAIN"] = domain,
                    ["MODSTACK_CERT_DAYS"] = validityDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["MODSTACK_PFX_PATH"] = tempPfxPath
                };

                var result = await RunProcessWithStandardInputAsync(
                    "powershell.exe",
                    new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", psScript },
                    password,
                    envVars);
                
                if (!result.Success || !File.Exists(tempPfxPath))
                {
                    Debug.WriteLine($"❌ PowerShell failed to generate PFX: {result.Error}");
                    return false;
                }

                // Extract certificate and private key from PFX
                var success = await ExtractPfxToPemAsync(tempPfxPath, certPath, keyPath, password);
                
                // Clean up temporary PFX file
                CleanupTemporaryFiles(new[] { tempPfxPath });
                
                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ PowerShell method failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extracts certificate and private key from PFX file using .NET cryptography first, falling back to OpenSSL via stdin
        /// </summary>
        private async Task<bool> ExtractPfxToPemAsync(string pfxPath, string certPath, string keyPath, string password)
        {
            try
            {
                // Primary method: extract in-process using .NET cryptography so password never crosses process boundaries
                if (await ExtractPfxToPemWithDotNetAsync(pfxPath, certPath, keyPath, password))
                {
                    return true;
                }

                Debug.WriteLine("🔍 Falling back to OpenSSL with stdin password pipe for PFX extraction");

                // Try to find OpenSSL
                var openSslPaths = new[]
                {
                    @"C:\OpenSSL-Win64\bin\openssl.exe",
                    @"C:\OpenSSL-Win32\bin\openssl.exe",
                    @"C:\Program Files\OpenSSL-Win64\bin\openssl.exe",
                    @"C:\Program Files (x86)\OpenSSL-Win32\bin\openssl.exe",
                    @"C:\xampp\apache\bin\openssl.exe",
                    @"C:\wamp64\bin\apache\apache2.4.65\bin\openssl.exe",
                    @"C:\wamp\bin\apache\apache2.4.65\bin\openssl.exe",
                    @"openssl.exe" // Try PATH
                };

                string? openSslPath = null;
                foreach (var path in openSslPaths)
                {
                    if (path == "openssl.exe")
                    {
                        var pathTest = await RunProcessAsync("where", "openssl.exe");
                        if (pathTest.Success && !string.IsNullOrEmpty(pathTest.Output))
                        {
                            openSslPath = "openssl.exe";
                            break;
                        }
                    }
                    else if (File.Exists(path))
                    {
                        openSslPath = path;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(openSslPath))
                {
                    return false;
                }

                // Extract certificate from PFX using -passin stdin (never expose password in command-line arguments)
                var tempCertPath = certPath + ".tmp";
                var certResult = await RunProcessWithStandardInputAsync(
                    openSslPath,
                    new[] { "pkcs12", "-in", pfxPath, "-clcerts", "-nokeys", "-out", tempCertPath, "-passin", "stdin" },
                    password);
                
                if (!certResult.Success)
                {
                    Debug.WriteLine($"❌ Failed to extract certificate from PFX: {certResult.Error}");
                    return false;
                }

                // Extract private key from PFX using -passin stdin
                var tempKeyPath = keyPath + ".tmp";
                var keyResult = await RunProcessWithStandardInputAsync(
                    openSslPath,
                    new[] { "pkcs12", "-in", pfxPath, "-nocerts", "-nodes", "-out", tempKeyPath, "-passin", "stdin" },
                    password);
                
                if (!keyResult.Success)
                {
                    Debug.WriteLine($"❌ Failed to extract private key from PFX: {keyResult.Error}");
                    return false;
                }

                // Atomically move temporary files to final locations
                File.Move(tempCertPath, certPath, overwrite: true);
                File.Move(tempKeyPath, keyPath, overwrite: true);

                Debug.WriteLine($"✅ Successfully extracted certificate and private key from PFX");
                return File.Exists(certPath) && File.Exists(keyPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error extracting PFX: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extracts certificate and private key from PFX using .NET cryptography
        /// </summary>
        private async Task<bool> ExtractPfxToPemWithDotNetAsync(string pfxPath, string certPath, string keyPath, string password)
        {
            try
            {
                Debug.WriteLine("🔍 Extracting certificate and private key from PFX using .NET cryptography");

                // Load PFX file with Exportable flag so private key can be exported to PEM
                using var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                    pfxPath,
                    password,
                    X509KeyStorageFlags.Exportable);
                
                // Export certificate as PEM (atomic write)
                var certPem = $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(certificate.RawData, Base64FormattingOptions.InsertLineBreaks)}\n-----END CERTIFICATE-----";
                var tempCertPath = certPath + ".tmp";
                await File.WriteAllTextAsync(tempCertPath, certPem);
                File.Move(tempCertPath, certPath, overwrite: true);

                // Export private key as PEM using .NET cryptography
                if (certificate.HasPrivateKey)
                {
                    using var privateKey = certificate.GetRSAPrivateKey();
                    if (privateKey != null)
                    {
                        // Windows CNG keys imported from PFX with Exportable allow encrypted PKCS#8 export,
                        // which we re-import into an ephemeral RSA instance to export PKCS#1 PEM safely.
                        var pbeParams = new PbeParameters(
                            PbeEncryptionAlgorithm.Aes256Cbc,
                            HashAlgorithmName.SHA256,
                            10000);
                        var encryptedPkcs8 = privateKey.ExportEncryptedPkcs8PrivateKey(password, pbeParams);
                        using var exportableRsa = RSA.Create();
                        exportableRsa.ImportEncryptedPkcs8PrivateKey(password, encryptedPkcs8, out _);

                        var keyBytes = exportableRsa.ExportRSAPrivateKey();
                        var keyPem = $"-----BEGIN RSA PRIVATE KEY-----\n{Convert.ToBase64String(keyBytes, Base64FormattingOptions.InsertLineBreaks)}\n-----END RSA PRIVATE KEY-----";
                        var tempKeyPath = keyPath + ".tmp";
                        await File.WriteAllTextAsync(tempKeyPath, keyPem);
                        File.Move(tempKeyPath, keyPath, overwrite: true);
                        
                        Debug.WriteLine("✅ Successfully extracted private key using .NET cryptography");
                    }
                    else
                    {
                        Debug.WriteLine("⚠️ Certificate has private key but it's not accessible");
                        return false;
                    }
                }
                else
                {
                    Debug.WriteLine("⚠️ Certificate does not have a private key");
                    return false;
                }

                Debug.WriteLine("✅ Successfully extracted certificate and private key using .NET cryptography");
                return true;
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                Debug.WriteLine($"❌ Cryptographic error extracting PFX with .NET: {ex.Message}");
                _logger?.LogError(ex, "Cryptographic error extracting PFX with .NET");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"❌ Access denied extracting PFX with .NET: {ex.Message}");
                _logger?.LogError(ex, "Access denied extracting PFX with .NET");
                return false;
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"❌ I/O error extracting PFX with .NET: {ex.Message}");
                _logger?.LogError(ex, "I/O error extracting PFX with .NET");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Unexpected error extracting PFX with .NET: {ex.Message}");
                _logger?.LogError(ex, "Unexpected error extracting PFX with .NET");
                return false;
            }
        }

        /// <summary>
        /// Attempts to generate SSL certificate using .NET cryptography
        /// </summary>
        private async Task<bool> TryGenerateWithDotNetAsync(string domain, string certPath, string keyPath, int validityDays, string password)
        {
            try
            {
                Debug.WriteLine("🔍 Attempting to generate SSL certificate using .NET cryptography");

                // Create certificate directory if it doesn't exist
                var certDir = Path.GetDirectoryName(certPath);
                if (!string.IsNullOrEmpty(certDir) && !Directory.Exists(certDir))
                {
                    Directory.CreateDirectory(certDir);
                }

                // Generate RSA key pair
                using var rsa = RSA.Create(2048);
                var keyRequest = new CertificateRequest($"CN={domain}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                // Add subject alternative names
                var sanBuilder = new SubjectAlternativeNameBuilder();
                sanBuilder.AddDnsName(domain);
                sanBuilder.AddDnsName($"www.{domain}");
                sanBuilder.AddDnsName("localhost");
                sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
                keyRequest.CertificateExtensions.Add(sanBuilder.Build());

                // Add key usage extensions
                keyRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
                keyRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

                // Create self-signed certificate
                var notBefore = DateTimeOffset.UtcNow;
                var notAfter = notBefore.AddDays(validityDays);
                using var certificate = keyRequest.CreateSelfSigned(notBefore, notAfter);

                // Export certificate to PEM format
                var certBytes = certificate.Export(X509ContentType.Cert);
                var certPem = "-----BEGIN CERTIFICATE-----\n" +
                             Convert.ToBase64String(certBytes, Base64FormattingOptions.InsertLineBreaks) +
                             "\n-----END CERTIFICATE-----";
                await File.WriteAllTextAsync(certPath, certPem);

                // Export private key to PEM format
                var keyBytes = rsa.ExportRSAPrivateKey();
                var keyPem = "-----BEGIN RSA PRIVATE KEY-----\n" +
                            Convert.ToBase64String(keyBytes, Base64FormattingOptions.InsertLineBreaks) +
                            "\n-----END RSA PRIVATE KEY-----";
                await File.WriteAllTextAsync(keyPath, keyPem);

                return File.Exists(certPath) && File.Exists(keyPath);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                Debug.WriteLine($"❌ Cryptographic error in .NET certificate generation: {ex.Message}");
                _logger?.LogError(ex, "Cryptographic error in .NET certificate generation");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"❌ Access denied in .NET certificate generation: {ex.Message}");
                _logger?.LogError(ex, "Access denied in .NET certificate generation");
                return false;
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"❌ I/O error in .NET certificate generation: {ex.Message}");
                _logger?.LogError(ex, "I/O error in .NET certificate generation");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Unexpected error in .NET certificate generation: {ex.Message}");
                _logger?.LogError(ex, "Unexpected error in .NET certificate generation");
                return false;
            }
        }

        /// <summary>
        /// Runs a process and returns the result
        /// </summary>
        private async Task<(bool Success, string Output, string Error)> RunProcessAsync(string fileName, string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                return (process.ExitCode == 0, output, error);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, ex.Message);
            }
        }

        /// <summary>
        /// Runs a process with structured ArgumentList, optional environment variables, and sensitive data piped via StandardInput
        /// </summary>
        private async Task<(bool Success, string Output, string Error)> RunProcessWithStandardInputAsync(
            string fileName,
            IEnumerable<string> arguments,
            string standardInputLine,
            IDictionary<string, string>? environmentVariables = null)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                foreach (var arg in arguments)
                {
                    startInfo.ArgumentList.Add(arg);
                }

                if (environmentVariables != null)
                {
                    foreach (var kvp in environmentVariables)
                    {
                        startInfo.Environment[kvp.Key] = kvp.Value;
                    }
                }

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                await process.StandardInput.WriteLineAsync(standardInputLine);
                await process.StandardInput.FlushAsync();
                process.StandardInput.Close();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                return (process.ExitCode == 0, output, error);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, ex.Message);
            }
        }

        /// <summary>
        /// Validates if an SSL certificate is valid and not expired
        /// </summary>
        /// <param name="certPath">Path to the certificate file</param>
        /// <returns>True if valid, false otherwise</returns>
        public async Task<bool> ValidateCertificateAsync(string certPath)
        {
            try
            {
                if (!File.Exists(certPath))
                {
                    return false;
                }

                var certContent = await File.ReadAllTextAsync(certPath);
                if (!certContent.Contains("-----BEGIN CERTIFICATE-----"))
                {
                    return false;
                }

                // Parse PEM content to handle multiple certificates
                var certBytes = ParsePemCertificate(certContent);
                if (certBytes == null)
                {
                    return false;
                }

                using var certificate = new X509Certificate2(certBytes);
                return certificate.NotAfter > DateTime.Now;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error validating certificate: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates certificate with X509Chain for trust verification
        /// </summary>
        public async Task<bool> ValidateCertificateWithChainAsync(string certPath)
        {
            try
            {
                if (!File.Exists(certPath))
                {
                    return false;
                }

                var certContent = await File.ReadAllTextAsync(certPath);
                if (!certContent.Contains("-----BEGIN CERTIFICATE-----"))
                {
                    return false;
                }

                // Parse PEM content to handle multiple certificates
                var certBytes = ParsePemCertificate(certContent);
                if (certBytes == null)
                {
                    return false;
                }

                using var certificate = new X509Certificate2(certBytes);
                
                // Check if certificate is expired
                if (certificate.NotAfter <= DateTime.Now)
                {
                    Debug.WriteLine($"❌ Certificate expired on {certificate.NotAfter}");
                    return false;
                }

                // Validate certificate chain
                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // Skip revocation check for dev certs
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                
                var isValid = chain.Build(certificate);
                
                if (!isValid)
                {
                    Debug.WriteLine($"❌ Certificate chain validation failed:");
                    foreach (X509ChainStatus status in chain.ChainStatus)
                    {
                        Debug.WriteLine($"   - {status.Status}: {status.StatusInformation}");
                    }
                }
                else
                {
                    Debug.WriteLine($"✅ Certificate chain validation successful");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error validating certificate chain: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets certificate information
        /// </summary>
        /// <param name="certPath">Path to the certificate file</param>
        /// <returns>Certificate information or null if invalid</returns>
        public async Task<CertificateInfo?> GetCertificateInfoAsync(string certPath)
        {
            try
            {
                if (!File.Exists(certPath))
                {
                    return null;
                }

                var certContent = await File.ReadAllTextAsync(certPath);
                if (!certContent.Contains("-----BEGIN CERTIFICATE-----"))
                {
                    return null;
                }

                // Parse PEM content to handle multiple certificates
                var certBytes = ParsePemCertificate(certContent);
                if (certBytes == null)
                {
                    return null;
                }

                using var certificate = new X509Certificate2(certBytes);
                
                // Extract Subject Alternative Names using reliable parsing
                var sanList = ParseSubjectAlternativeNames(certificate);

                // Get key information
                var keySize = 0;
                var keyAlgorithm = "Unknown";
                try
                {
                    if (certificate.PublicKey.GetRSAPublicKey() is System.Security.Cryptography.RSA rsa)
                    {
                        keySize = rsa.KeySize;
                        keyAlgorithm = "RSA";
                    }
                    else if (certificate.PublicKey.GetECDsaPublicKey() is System.Security.Cryptography.ECDsa ecdsa)
                    {
                        keySize = ecdsa.KeySize;
                        keyAlgorithm = "ECDSA";
                    }
                }
                catch
                {
                    // Ignore key size extraction errors
                }

                return new CertificateInfo
                {
                    Subject = certificate.Subject,
                    Issuer = certificate.Issuer,
                    ValidFrom = certificate.NotBefore,
                    ValidTo = certificate.NotAfter,
                    Thumbprint = certificate.Thumbprint,
                    IsValid = certificate.NotAfter > DateTime.Now,
                    SubjectAlternativeNames = sanList,
                    SerialNumber = certificate.SerialNumber,
                    SignatureAlgorithm = certificate.SignatureAlgorithm.FriendlyName ?? certificate.SignatureAlgorithm.Value,
                    KeySize = keySize,
                    KeyAlgorithm = keyAlgorithm
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error getting certificate info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generates a secure random password for PFX files
        /// </summary>
        private string GenerateSecurePassword()
        {
            // Generate a secure random password using GUID
            var password = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..8];
            Debug.WriteLine("🔐 Generated secure password for PFX file (value hidden for security)");
            return password;
        }

        /// <summary>
        /// Safely cleans up temporary files with secure deletion and detailed error handling
        /// </summary>
        private void CleanupTemporaryFiles(string[] filePaths)
        {
            foreach (var filePath in filePaths)
            {
                try
                {
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        SecureDeleteFile(filePath);
                        Debug.WriteLine($"🔒 Securely cleaned up temporary file: {Path.GetFileName(filePath)}");
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    Debug.WriteLine($"⚠️ Could not delete temporary file {filePath}: Access denied. {ex.Message}");
                }
                catch (IOException ex)
                {
                    Debug.WriteLine($"⚠️ Could not delete temporary file {filePath}: File in use. {ex.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️ Could not delete temporary file {filePath}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Securely deletes a file by overwriting it with random data before deletion
        /// </summary>
        private void SecureDeleteFile(string filePath)
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
                    File.WriteAllBytes(filePath, buffer);
                }

                // Finally delete the file
                File.Delete(filePath);
                Debug.WriteLine($"🔒 Securely deleted sensitive file: {Path.GetFileName(filePath)}");
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

        /// <summary>
        /// Parses PEM content to extract the first certificate (handles certificate chains)
        /// </summary>
        private byte[]? ParsePemCertificate(string pemContent)
        {
            try
            {
                // Use regex to find all certificate blocks
                var certPattern = @"-----BEGIN CERTIFICATE-----\s*(.*?)\s*-----END CERTIFICATE-----";
                var matches = System.Text.RegularExpressions.Regex.Matches(pemContent, certPattern, 
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                if (matches.Count == 0)
                {
                    return null;
                }

                // Take the first certificate (leaf certificate)
                var firstMatch = matches[0];
                var base64Content = firstMatch.Groups[1].Value
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Replace(" ", "");

                return Convert.FromBase64String(base64Content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error parsing PEM certificate: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses PEM content to extract all certificates in the chain
        /// </summary>
        private List<byte[]> ParsePemCertificateChain(string pemContent)
        {
            var certificates = new List<byte[]>();
            
            try
            {
                var certPattern = @"-----BEGIN CERTIFICATE-----\s*(.*?)\s*-----END CERTIFICATE-----";
                var matches = System.Text.RegularExpressions.Regex.Matches(pemContent, certPattern, 
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var base64Content = match.Groups[1].Value
                        .Replace("\n", "")
                        .Replace("\r", "")
                        .Replace(" ", "");

                    certificates.Add(Convert.FromBase64String(base64Content));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error parsing PEM certificate chain: {ex.Message}");
            }

            return certificates;
        }

        /// <summary>
        /// Adds a certificate to the trusted root store (WARNING: Development only!)
        /// </summary>
        /// <param name="certPath">Path to the certificate file</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> AddToTrustedRootAsync(string certPath)
        {
            try
            {
                Debug.WriteLine("⚠️ Adding certificate to trusted root store - DEVELOPMENT ONLY!");
                _logger?.LogWarning("Adding certificate to trusted root store - DEVELOPMENT ONLY! Domain: {CertPath}", certPath);

                if (!File.Exists(certPath))
                {
                    Debug.WriteLine($"❌ Certificate file not found: {certPath}");
                    return false;
                }

                // Parse PEM content to handle multiple certificates
                var certContent = await File.ReadAllTextAsync(certPath);
                if (!certContent.Contains("-----BEGIN CERTIFICATE-----"))
                {
                    Debug.WriteLine($"❌ Invalid certificate format: {certPath}");
                    return false;
                }

                var certBytes = ParsePemCertificate(certContent);
                if (certBytes == null)
                {
                    Debug.WriteLine($"❌ Failed to parse certificate: {certPath}");
                    return false;
                }

                using var certificate = new X509Certificate2(certBytes);
                
                // Add to trusted root store
                using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                
                // Check if certificate already exists
                var existingCerts = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);
                if (existingCerts.Count > 0)
                {
                    Debug.WriteLine($"✅ Certificate already exists in trusted root store: {certificate.Thumbprint}");
                    return true;
                }
                
                store.Add(certificate);
                Debug.WriteLine($"✅ Successfully added certificate to trusted root store: {certificate.Thumbprint}");
                _logger?.LogInformation("Certificate added to trusted root store: {Thumbprint}", certificate.Thumbprint);
                
                return true;
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                Debug.WriteLine($"❌ Cryptographic error adding certificate to trusted root store: {ex.Message}");
                _logger?.LogError(ex, "Cryptographic error adding certificate to trusted root store");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"❌ Access denied adding certificate to trusted root store: {ex.Message}");
                _logger?.LogError(ex, "Access denied adding certificate to trusted root store");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Unexpected error adding certificate to trusted root store: {ex.Message}");
                _logger?.LogError(ex, "Unexpected error adding certificate to trusted root store");
                return false;
            }
        }

        /// <summary>
        /// Removes a certificate from the trusted root store
        /// </summary>
        /// <param name="certPath">Path to the certificate file</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> RemoveFromTrustedRootAsync(string certPath)
        {
            try
            {
                Debug.WriteLine("🗑️ Removing certificate from trusted root store");
                _logger?.LogInformation("Removing certificate from trusted root store: {CertPath}", certPath);

                if (!File.Exists(certPath))
                {
                    Debug.WriteLine($"❌ Certificate file not found: {certPath}");
                    return false;
                }

                // Parse PEM content to handle multiple certificates
                var certContent = await File.ReadAllTextAsync(certPath);
                if (!certContent.Contains("-----BEGIN CERTIFICATE-----"))
                {
                    Debug.WriteLine($"❌ Invalid certificate format: {certPath}");
                    return false;
                }

                var certBytes = ParsePemCertificate(certContent);
                if (certBytes == null)
                {
                    Debug.WriteLine($"❌ Failed to parse certificate: {certPath}");
                    return false;
                }

                using var certificate = new X509Certificate2(certBytes);
                
                // Remove from trusted root store
                using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                
                var existingCerts = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);
                if (existingCerts.Count == 0)
                {
                    Debug.WriteLine($"✅ Certificate not found in trusted root store: {certificate.Thumbprint}");
                    return true;
                }
                
                foreach (var cert in existingCerts)
                {
                    store.Remove(cert);
                }
                
                Debug.WriteLine($"✅ Successfully removed certificate from trusted root store: {certificate.Thumbprint}");
                _logger?.LogInformation("Certificate removed from trusted root store: {Thumbprint}", certificate.Thumbprint);
                
                return true;
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                Debug.WriteLine($"❌ Cryptographic error removing certificate from trusted root store: {ex.Message}");
                _logger?.LogError(ex, "Cryptographic error removing certificate from trusted root store");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"❌ Access denied removing certificate from trusted root store: {ex.Message}");
                _logger?.LogError(ex, "Access denied removing certificate from trusted root store");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Unexpected error removing certificate from trusted root store: {ex.Message}");
                _logger?.LogError(ex, "Unexpected error removing certificate from trusted root store");
                return false;
            }
        }

        /// <summary>
        /// Parses Subject Alternative Names from a certificate using reliable ASN.1 parsing
        /// </summary>
        private List<string> ParseSubjectAlternativeNames(X509Certificate2 certificate)
        {
            var sanList = new List<string>();
            
            try
            {
                foreach (var extension in certificate.Extensions)
                {
                    if (extension.Oid?.Value == "2.5.29.17") // SAN extension OID
                    {
                        try
                        {
                            // Try to parse using AsnEncodedData for better reliability
                            var sanExtension = new AsnEncodedData(extension.Oid, extension.RawData);
                            var sanText = sanExtension.Format(false);
                            
                            // Parse DNS names, IP addresses, and other SAN entries
                            var lines = sanText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                var trimmedLine = line.Trim();
                                
                                // Parse DNS names
                                if (trimmedLine.StartsWith("DNS Name=", StringComparison.OrdinalIgnoreCase))
                                {
                                    var dnsName = trimmedLine.Substring(9).Trim();
                                    if (!string.IsNullOrEmpty(dnsName) && !sanList.Contains(dnsName))
                                    {
                                        sanList.Add(dnsName);
                                    }
                                }
                                // Parse IP addresses
                                else if (trimmedLine.StartsWith("IP Address=", StringComparison.OrdinalIgnoreCase))
                                {
                                    var ipAddress = trimmedLine.Substring(11).Trim();
                                    if (!string.IsNullOrEmpty(ipAddress) && !sanList.Contains(ipAddress))
                                    {
                                        sanList.Add(ipAddress);
                                    }
                                }
                                // Parse other SAN types (RFC822, URI, etc.)
                                else if (trimmedLine.StartsWith("RFC822 Name=", StringComparison.OrdinalIgnoreCase))
                                {
                                    var rfc822Name = trimmedLine.Substring(12).Trim();
                                    if (!string.IsNullOrEmpty(rfc822Name) && !sanList.Contains(rfc822Name))
                                    {
                                        sanList.Add($"email:{rfc822Name}");
                                    }
                                }
                                else if (trimmedLine.StartsWith("Uniform Resource Identifier=", StringComparison.OrdinalIgnoreCase))
                                {
                                    var uri = trimmedLine.Substring(29).Trim();
                                    if (!string.IsNullOrEmpty(uri) && !sanList.Contains(uri))
                                    {
                                        sanList.Add($"uri:{uri}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"⚠️ Warning: Could not parse SAN extension using AsnEncodedData: {ex.Message}");
                            
                            // Fallback to Format() method if AsnEncodedData fails
                            try
                            {
                                var fallbackText = extension.Format(false);
                                var lines = fallbackText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                                foreach (var line in lines)
                                {
                                    if (line.Trim().StartsWith("DNS Name=", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var dnsName = line.Trim().Substring(9).Trim();
                                        if (!string.IsNullOrEmpty(dnsName) && !sanList.Contains(dnsName))
                                        {
                                            sanList.Add(dnsName);
                                        }
                                    }
                                }
                            }
                            catch (Exception fallbackEx)
                            {
                                Debug.WriteLine($"⚠️ Warning: SAN parsing fallback also failed: {fallbackEx.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Warning: Error parsing Subject Alternative Names: {ex.Message}");
            }
            
            return sanList;
        }
    }
    /// <summary>
    /// Information about an SSL certificate
    /// </summary>
    public class CertificateInfo
    {
        public string Subject { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public string Thumbprint { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public List<string> SubjectAlternativeNames { get; set; } = [];
        public string SerialNumber { get; set; } = string.Empty;
        public string SignatureAlgorithm { get; set; } = string.Empty;
        public int KeySize { get; set; }
        public string KeyAlgorithm { get; set; } = string.Empty;
    }
}
