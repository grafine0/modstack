using System.Collections.Generic;
using System.Linq;

namespace ModStack.Common.Models;

/// <summary>
/// Results of uninstallation validation
/// </summary>
public class ServiceUninstallValidation
{
    public string PackageName { get; set; } = string.Empty;
    public string? Version { get; set; }
    public bool CanUninstall { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> RequiredActions { get; set; } = new();
    public List<ServiceDefinition> AffectedServices { get; set; } = new();
    public List<ServiceDefinition> RunningServices { get; set; } = new();
    public long FilesToDeleteCount { get; set; } = 0;

    public bool HasRunningServices => RunningServices.Any();
    public bool HasWarnings => Warnings.Any();
    public bool RequiresUserConfirmation => HasRunningServices || HasWarnings || RequiredActions.Any();
}
