using System;
using System.Collections.Generic;

namespace ModStack.Common.Models
{
    /// <summary>
    /// Configuration for package installation
    /// </summary>
    public class PackageInstallationConfig
    {
        public AvailablePackage Package { get; set; } = new();
        public PackageVersion Version { get; set; } = new();
        public string InstallPath { get; set; } = string.Empty;
        public bool AutoStart { get; set; }
        public bool RegisterAsService { get; set; }
        public bool RequireChecksumVerification { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    /// <summary>
    /// Interface for reporting installation progress
    /// </summary>
    public interface IProgressReporter
    {
        void UpdateProgress(string message, int percentage);
    }
}
