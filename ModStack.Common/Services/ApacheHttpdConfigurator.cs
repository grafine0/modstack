using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModStack.Common.Services;

/// <summary>
/// Shared helper for normalizing Apache httpd.conf configuration.
/// Ensures required modules are loaded and SSL cache directives are configured.
/// </summary>
public partial class ApacheHttpdConfigurator
{
    private static readonly SemaphoreSlim ConfigLock = new(1, 1);
    private readonly ILogger<ApacheHttpdConfigurator>? _logger;

    [GeneratedRegex(@"(LoadModule\s+\w+_module[^\n]+\n)")]
    private static partial Regex LoadModulePattern();

    [GeneratedRegex(@"^\s*SSLSessionCache.*$", RegexOptions.Multiline)]
    private static partial Regex SslSessionCacheRegex();

    [GeneratedRegex(@"^\s*SSLSessionCacheTimeout.*$", RegexOptions.Multiline)]
    private static partial Regex SslSessionCacheTimeoutRegex();

    public ApacheHttpdConfigurator(ILogger<ApacheHttpdConfigurator>? logger = null)
    {
        _logger = logger;
    }

    public async Task EnsureModulesAsync(string apachePath, bool enableSsl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apachePath))
        {
            return;
        }

        var httpdConfPath = Path.Combine(apachePath, "conf", "httpd.conf");
        if (!File.Exists(httpdConfPath))
        {
            _logger?.LogWarning("Apache configuration not found at {Path}", httpdConfPath);
            return;
        }

        await ConfigLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var content = await File.ReadAllTextAsync(httpdConfPath, cancellationToken).ConfigureAwait(false);

            // Ensure ServerName is set to prevent Apache from guessing and to support
            // proper name-based virtual hosting (avoids cross-project serving)
            content = EnsureServerNameDirective(content);

            content = EnsureLoadModuleDirective(content, "proxy_module", "modules/mod_proxy.so");
            content = EnsureLoadModuleDirective(content, "proxy_fcgi_module", "modules/mod_proxy_fcgi.so");
            content = EnsureLoadModuleDirective(content, "fcgid_module", "modules/mod_fcgid.so");
            content = EnsureLoadModuleDirective(content, "rewrite_module", "modules/mod_rewrite.so");

            if (enableSsl)
            {
                content = EnsureLoadModuleDirective(content, "socache_shmcb_module", "modules/mod_socache_shmcb.so");
                content = EnsureLoadModuleDirective(content, "ssl_module", "modules/mod_ssl.so");

                content = SslSessionCacheRegex().Replace(content, string.Empty);
                content = SslSessionCacheTimeoutRegex().Replace(content, string.Empty);

                var logsDirectory = Path.Combine(apachePath, "logs");
                Directory.CreateDirectory(logsDirectory);
                var sslCachePath = Path.Combine(logsDirectory, "ssl_scache").Replace("\\", "/");

                var sessionCacheDirective = $"SSLSessionCache \"shmcb:{sslCachePath}(512000)\"";
                var sessionCacheTimeoutDirective = "SSLSessionCacheTimeout 300";

                if (!content.EndsWith("\n"))
                {
                    content += "\n";
                }

                content += $"{sessionCacheDirective}\n{sessionCacheTimeoutDirective}\n";
            }

            await File.WriteAllTextAsync(httpdConfPath, content, cancellationToken).ConfigureAwait(false);
            _logger?.LogInformation("Ensured Apache modules for path {ApachePath} (SSL enabled: {EnableSsl})", apachePath, enableSsl);
        }
        finally
        {
            ConfigLock.Release();
        }
    }

    private static string EnsureLoadModuleDirective(string content, string moduleName, string modulePath)
    {
        var moduleRegex = new Regex($@"^\s*#?\s*LoadModule\s+{Regex.Escape(moduleName)}\s+[^\n]*\n",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        content = moduleRegex.Replace(content, string.Empty);

        var directive = $"LoadModule {moduleName} {modulePath}\n";
        var matches = LoadModulePattern().Matches(content);

        if (matches.Count > 0)
        {
            var insertPosition = matches[^1].Index + matches[^1].Length;
            return content.Insert(insertPosition, directive);
        }

        return directive + content;
    }

    [GeneratedRegex(@"^\s*ServerName\s+\S+", RegexOptions.Multiline)]
    private static partial Regex ServerNameDirectivePattern();

    /// <summary>
    /// Ensures ServerName localhost is set in httpd.conf. Without this, Apache
    /// cannot reliably distinguish virtual hosts by name, leading to cross-project
    /// content serving.
    /// </summary>
    private static string EnsureServerNameDirective(string content)
    {
        // Check if ServerName is already set (not commented out)
        if (ServerNameDirectivePattern().IsMatch(content))
        {
            return content;
        }

        // Insert ServerName localhost after the ServerAdmin line
        var serverAdminRegex = new Regex(@"^(\s*ServerAdmin\s+[^\n]+\n)", RegexOptions.Multiline);
        var serverAdminMatch = serverAdminRegex.Match(content);

        if (serverAdminMatch.Success)
        {
            var insertPosition = serverAdminMatch.Index + serverAdminMatch.Length;
            return content.Insert(insertPosition, "ServerName localhost\n");
        }

        // Fallback: insert after Listen directives
        var listenRegex = new Regex(@"^(\s*Listen\s+[^\n]+\n)", RegexOptions.Multiline);
        var listenMatches = listenRegex.Matches(content);

        if (listenMatches.Count > 0)
        {
            var lastListen = listenMatches[^1];
            var insertPosition = lastListen.Index + lastListen.Length;
            return content.Insert(insertPosition, "ServerName localhost\n");
        }

        // Last resort: prepend
        return "ServerName localhost\n" + content;
    }
}

