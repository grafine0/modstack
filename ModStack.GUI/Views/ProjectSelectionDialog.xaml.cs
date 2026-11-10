using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ModStack.Common.Models;
using ModStack.Core.Interfaces;

namespace ModStack.GUI.Views
{
    /// <summary>
    /// Interaction logic for ProjectSelectionDialog.xaml
    /// </summary>
    public partial class ProjectSelectionDialog : Window
    {
        public AvailablePackage Package { get; }
        public PackageVersion Version { get; }
        public List<Project> Projects { get; }
        public Project? SelectedProject { get; set; }
        public ProjectPackageInstallationConfig InstallationConfig { get; set; }

        private readonly ModStack.GUI.Services.Interfaces.IDialogService _dialogService;

        public ProjectSelectionDialog(
            ModStack.GUI.Services.Interfaces.IDialogService dialogService,
            AvailablePackage package, 
            PackageVersion version, 
            List<Project> availableProjects)
        {
            InitializeComponent();
            _dialogService = dialogService;
            
            Package = package ?? throw new ArgumentNullException(nameof(package));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Projects = availableProjects ?? throw new ArgumentNullException(nameof(availableProjects));
            
            InstallationConfig = new ProjectPackageInstallationConfig
            {
                InstallationPath = GetDefaultInstallationPath(),
                CreateSubdomain = ShouldShowSubdomainOption(),
                Subdomain = GetDefaultSubdomain(),
                DatabaseConfig = new DatabaseConfiguration
                {
                    DatabaseName = $"{package.Name.ToLower()}_db",
                    Username = $"{package.Name.ToLower()}_user",
                    Password = GenerateRandomPassword(),
                    Host = "localhost",
                    Port = 3306,
                    CreateDatabase = true,
                    CreateUser = true
                }
            };

            // Set data context
            DataContext = this;
            
            // Initialize UI
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Set package info
            PackageDisplayName = $"{Package.DisplayName} {Version.Version}";
            PackageDescription = Package.Description;
            
            // Set default installation path
            InstallationPath = InstallationConfig.InstallationPath;
            
            // Set database defaults
            DatabaseName = InstallationConfig.DatabaseConfig?.DatabaseName ?? "";
            DatabaseUser = InstallationConfig.DatabaseConfig?.Username ?? "";
            
            // Select first project if available
            if (Projects.Count > 0)
            {
                SelectedProject = Projects[0];
                InstallationConfig.TargetProject = SelectedProject;
            }
        }

        private string GetDefaultInstallationPath()
        {
            return Package.Name.ToLower() switch
            {
                "laravel" => "laravel",
                "wordpress" => "wordpress",
                "phpmyadmin" => "phpmyadmin",
                "adminer" => "adminer",
                _ => Package.Name.ToLower()
            };
        }

        private bool ShouldShowSubdomainOption()
        {
            return Package.Name.ToLower() switch
            {
                "phpmyadmin" => true,
                "adminer" => true,
                "laravel" => false, // Laravel typically goes in project root or subdirectory
                "wordpress" => false, // WordPress typically goes in project root
                _ => true
            };
        }

        private string GetDefaultSubdomain()
        {
            return Package.Name.ToLower() switch
            {
                "phpmyadmin" => "phpmyadmin",
                "adminer" => "adminer",
                _ => Package.Name.ToLower()
            };
        }

        private string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 12)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private void ProjectsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectsListView.SelectedItem is Project selectedProject)
            {
                SelectedProject = selectedProject;
                InstallationConfig.TargetProject = selectedProject;
                
                // Update database name with project context
                if (InstallationConfig.DatabaseConfig != null)
                {
                    InstallationConfig.DatabaseConfig.DatabaseName = $"{selectedProject.Name}_{Package.Name.ToLower()}";
                    InstallationConfig.DatabaseConfig.Username = $"{selectedProject.Name}_{Package.Name.ToLower()}";
                    
                    // Update UI
                    DatabaseName = InstallationConfig.DatabaseConfig.DatabaseName;
                    DatabaseUser = InstallationConfig.DatabaseConfig.Username;
                }
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate selection
            if (SelectedProject == null)
            {
                await _dialogService.ShowInformationAsync("Please select a project for installation.", "Selection Required");
                return;
            }

            // Validate installation path
            if (string.IsNullOrWhiteSpace(InstallPathTextBox.Text))
            {
                await _dialogService.ShowInformationAsync("Please enter an installation path.", "Path Required");
                return;
            }

            // Update configuration with UI values
            InstallationConfig.InstallationPath = InstallPathTextBox.Text.Trim();
            InstallationConfig.CreateSubdomain = CreateSubdomainCheckBox.IsChecked ?? false;
            InstallationConfig.Subdomain = SubdomainTextBox.Text.Trim();
            InstallationConfig.EnableSSL = EnableSSLCheckBox.IsChecked ?? false;
            InstallationConfig.CustomPort = int.TryParse(PortTextBox.Text, out var port) ? port : 0;
            InstallationConfig.AutoStart = AutoStartCheckBox.IsChecked ?? true;
            InstallationConfig.SetupDatabase = SetupDatabaseCheckBox.IsChecked ?? false;

            if (InstallationConfig.SetupDatabase && InstallationConfig.DatabaseConfig != null)
            {
                InstallationConfig.DatabaseConfig.DatabaseName = DatabaseNameTextBox.Text.Trim();
                InstallationConfig.DatabaseConfig.Username = DatabaseUserTextBox.Text.Trim();
                InstallationConfig.DatabaseConfig.Password = DatabasePasswordBox.Password;
                
                // Validate database configuration
                if (string.IsNullOrWhiteSpace(InstallationConfig.DatabaseConfig.DatabaseName) ||
                    string.IsNullOrWhiteSpace(InstallationConfig.DatabaseConfig.Username) ||
                    string.IsNullOrWhiteSpace(InstallationConfig.DatabaseConfig.Password))
                {
                    await _dialogService.ShowInformationAsync("Please provide database name and username.", "Database Information Required");
                    return;
                }
            }
            else
            {
                InstallationConfig.DatabaseConfig = null;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Properties for data binding
        public string PackageDisplayName { get; set; } = string.Empty;
        public string PackageDescription { get; set; } = string.Empty;
        public string InstallationPath { get; set; } = string.Empty;
        public bool CreateSubdomain { get; set; }
        public string Subdomain { get; set; } = string.Empty;
        public bool SetupDatabase { get; set; } = true;
        public string DatabaseName { get; set; } = string.Empty;
        public string DatabaseUser { get; set; } = string.Empty;
    }
}
