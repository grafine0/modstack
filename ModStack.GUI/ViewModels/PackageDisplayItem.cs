using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModStack.Common.Models;

namespace ModStack.GUI.ViewModels
{
    /// <summary>
    /// Display item for package list with UI-specific properties
    /// </summary>
    public class PackageDisplayItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsPopular { get; set; }
        public bool IsInstalled { get; set; }
        public string? InstalledVersion { get; set; }
        public string FormattedSize { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public AvailablePackage SourcePackage { get; set; } = new();

        // Version-specific installation tracking
        public List<string> InstalledVersions { get; set; } = new();
        
        public bool HasAnyVersionInstalled => InstalledVersions.Any();
        
        public bool HasAllVersionsInstalled => SourcePackage?.AvailableVersions?.All(v => InstalledVersions.Contains(v.Version)) ?? false;
        
        public string InstallationStatus
        {
            get
            {
                // For StandaloneWebApp packages (like phpMyAdmin)
                // These can be installed multiple times in different projects
                if (SourcePackage?.PackageType == Common.Enums.PackageType.StandaloneWebApp)
                {
                    return "Multiple Installations Allowed";
                }
                
                // For Framework packages (Laravel, CodeIgniter, etc.)
                // These can be installed multiple times in different projects
                if (SourcePackage?.PackageType == Common.Enums.PackageType.Framework)
                {
                    return "Multiple Installations Allowed";
                }
                
                // For regular service packages (Apache, MySQL, PHP, etc.)
                // Show standard version-based status
                if (!HasAnyVersionInstalled) return "Not Installed";
                if (HasAllVersionsInstalled) return "All Versions Installed";
                return $"{InstalledVersions.Count}/{SourcePackage?.AvailableVersions?.Count ?? 0} Versions Installed";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
