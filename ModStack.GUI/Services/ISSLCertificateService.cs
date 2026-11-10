namespace ModStack.GUI.Services
{
    /// <summary>
    /// Interface for SSL certificate generation and management
    /// </summary>
    public interface ISSLCertificateService
    {
        /// <summary>
        /// Checks if OpenSSL is available on the system
        /// </summary>
        /// <returns>True if OpenSSL is available, false otherwise</returns>
        Task<bool> IsOpenSSLAvailableAsync();

        /// <summary>
        /// Gets information about available SSL certificate generation methods
        /// </summary>
        /// <returns>Dictionary of available methods and their status</returns>
        Task<Dictionary<string, bool>> GetAvailableGenerationMethodsAsync();

        /// <summary>
        /// Generates a self-signed SSL certificate for the given domain
        /// </summary>
        /// <param name="domain">The domain name for the certificate</param>
        /// <param name="certPath">Path where the certificate file should be saved</param>
        /// <param name="keyPath">Path where the private key file should be saved</param>
        /// <param name="validityDays">Number of days the certificate should be valid (default: 365)</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> GenerateSelfSignedCertificateAsync(string domain, string certPath, string keyPath, int validityDays = 365);

        /// <summary>
        /// Validates if an SSL certificate is valid and not expired
        /// </summary>
        /// <param name="certPath">Path to the certificate file</param>
        /// <returns>True if valid, false otherwise</returns>
        Task<bool> ValidateCertificateAsync(string certPath);

        /// <summary>
        /// Gets certificate information
        /// </summary>
        /// <param name="certPath">Path to the certificate file</param>
        /// <returns>Certificate information or null if invalid</returns>
        Task<CertificateInfo?> GetCertificateInfoAsync(string certPath);
    }
}
