using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ModStack.Common.Models;
using ModStack.Services;
using ModStack.GUI.Services;
using ModStack.Core.Interfaces;
using Microsoft.Win32;

namespace ModStack.GUI.Views
{
    /// <summary>
    /// Interaction logic for PackageInstallDialog.xaml
    /// </summary>
    public partial class PackageInstallDialog : Window
    {
        [GeneratedRegex("^[0-9\\-]$")]
        private static partial Regex NumericInputRegex();
        private readonly AvailablePackage _package;
        private readonly Dictionary<string, object> _configValues = [];
        private readonly List<string> _installedVersions = [];
        private ProjectPackageInstallationConfig? _projectInstallationConfig;
        private readonly ObservableCollection<ProjectSelectionItem> _availableProjects = [];
        private long? _selectedProjectId = null;

        private readonly IProjectManager _projectManager;
        private readonly FrameworkPackageInstaller _frameworkInstaller;
        private readonly PackageInstaller _packageInstaller;
        private readonly ModStack.GUI.Services.IViewFactoryService _viewFactoryService;

        public PackageInstallDialog(
            IProjectManager projectManager,
            FrameworkPackageInstaller frameworkInstaller,
            PackageInstaller packageInstaller,
            ModStack.GUI.Services.IViewFactoryService viewFactoryService,
            AvailablePackage package, 
            List<string>? installedVersions = null)
        {
            InitializeComponent();
            _package = package;
            _installedVersions = installedVersions ?? [];
            
            _projectManager = projectManager;
            _frameworkInstaller = frameworkInstaller;
            _packageInstaller = packageInstaller;
            _viewFactoryService = viewFactoryService;
            
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Debug: Log package type
            System.Diagnostics.Debug.WriteLine($"🔍 PackageInstallDialog - Package: {_package.Name}, PackageType: {_package.PackageType}");
            
            // Package Info
            PackageIconTextBlock.Text = _package.Icon;
            PackageNameTextBlock.Text = _package.DisplayName;
            PackageDescriptionTextBlock.Text = _package.Description;

            // Versions with installation status
            System.Diagnostics.Debug.WriteLine($"🔍 Creating version items for package: {_package.Name}");
            System.Diagnostics.Debug.WriteLine($"📁 Install path being checked: {_package.DefaultConfiguration?.InstallPath}");
            System.Diagnostics.Debug.WriteLine($"💿 Installed versions detected: [{string.Join(", ", _installedVersions)}]");
            System.Diagnostics.Debug.WriteLine($"📋 Available versions in package: [{string.Join(", ", _package.AvailableVersions?.Select(v => v.Version) ?? [])}]");
            
            var versionItems = (_package.AvailableVersions ?? []).Select(v => new VersionDisplayItem
            {
                Version = v,
                IsInstalled = IsVersionInstalled(v.Version, _installedVersions),
                DisplayText = v.DisplayName // Clean display text, indicator will be shown separately
            }).ToList();
            
            // Debug: Show detailed version matching
            foreach (var item in versionItems)
            {
                System.Diagnostics.Debug.WriteLine($"📦 Version {item.Version.Version}: Available={item.Version.Version}, IsInstalled={item.IsInstalled}");
            }
            
            VersionComboBox.ItemsSource = versionItems;
            
            if (versionItems.Count > 0)
            {
                // Select first non-installed version, or recommended if not installed
                var firstUninstalled = versionItems.FirstOrDefault(v => !v.IsInstalled);
                var recommended = versionItems.FirstOrDefault(v => v.Version.IsRecommended && !v.IsInstalled);
                
                VersionComboBox.SelectedItem = recommended ?? firstUninstalled ?? versionItems.First();
                
                var selectedItem = VersionComboBox.SelectedItem as VersionDisplayItem;
                System.Diagnostics.Debug.WriteLine($"🎯 Selected version: {selectedItem?.Version.Version ?? "None"} (IsInstalled: {selectedItem?.IsInstalled ?? false})");
            }

            // Installation Path - hide for project-based packages, show for service packages
            System.Diagnostics.Debug.WriteLine($"🎯 PackageType check: {_package.PackageType} (StandaloneWebApp: {_package.PackageType == Common.Enums.PackageType.StandaloneWebApp}, Framework: {_package.PackageType == Common.Enums.PackageType.Framework})");
            
            if (_package.PackageType == Common.Enums.PackageType.StandaloneWebApp || 
                _package.PackageType == Common.Enums.PackageType.Framework ||
                _package.PackageType == Common.Enums.PackageType.Tool)
            {
                // Show project selection for project-based packages
                System.Diagnostics.Debug.WriteLine($"🔧 Showing project selection for package type: {_package.PackageType}");
                ProjectSelectionSection.Visibility = Visibility.Visible;
                InstallPathSection.Visibility = Visibility.Collapsed;
                ServiceConfigurationSection.Visibility = Visibility.Collapsed;
                ServiceOptionsSection.Visibility = Visibility.Collapsed; // Hide service options for non-service packages
                
                // Load available projects
                LoadAvailableProjectsAsync();
                
                // Wire up project selection change event
                ProjectSelectionComboBox.SelectionChanged += ProjectSelectionComboBox_SelectionChanged;
            }
            else
            {
                // Show installation path and service options for service packages
                System.Diagnostics.Debug.WriteLine($"🔧 Showing installation path sections for package type: {_package.PackageType}");
                ProjectSelectionSection.Visibility = Visibility.Collapsed;
                ServiceOptionsSection.Visibility = Visibility.Visible; // Show service options for service packages
                var basePath = _package.DefaultConfiguration?.InstallPath ?? string.Empty;
                InstallPathTextBox.Text = basePath;
                InstallPathTextBox.IsReadOnly = true;
                UpdateInstallPathPreview();
            }

            // Service Options - only set for actual service packages
            if (_package.PackageType != Common.Enums.PackageType.StandaloneWebApp && 
                _package.PackageType != Common.Enums.PackageType.Framework &&
                _package.PackageType != Common.Enums.PackageType.Tool)
            {
                AutoStartCheckBox.IsChecked = _package.DefaultConfiguration?.AutoStart ?? false;
                RegisterServiceCheckBox.IsChecked = _package.DefaultConfiguration?.RegisterAsService ?? false;
            }

            // Package Info
            LicenseTextBlock.Text = _package.License;
            HomepageTextBlock.Text = _package.Homepage;

            // Generate Configuration UI
            GenerateConfigurationUI();
            
            // Update size display
            UpdateSizeDisplay();
        }

        private async void LoadAvailableProjectsAsync()
        {
            try
            {
                if (_projectManager == null)
                {
                    ModernDialogService.ShowError("Project manager service not available.", "Service Error");
                    return;
                }

                var projects = await _projectManager.GetActiveProjectsAsync();
                _availableProjects.Clear();
                
                foreach (var project in projects)
                {
                    _availableProjects.Add(new ProjectSelectionItem
                    {
                        Id = project.Id,
                        DisplayName = project.Name,
                        DocumentRoot = project.DocumentRoot ?? string.Empty,
                        Domain = project.Domain ?? string.Empty
                    });
                }

                ProjectSelectionComboBox.ItemsSource = _availableProjects;
                
                // Select first project if available
                if (_availableProjects.Count > 0)
                {
                    ProjectSelectionComboBox.SelectedIndex = 0;
                    _selectedProjectId = _availableProjects[0].Id;
                    UpdateProjectInstallPath();
                }
                else
                {
                    ModernDialogService.ShowWarning("No active projects found. Please create a project first.", "No Projects Available");
                }
            }
            catch (Exception ex)
            {
                ModernDialogService.ShowError($"Failed to load projects: {ex.Message}", "Error");
            }
        }

        private void ProjectSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectSelectionComboBox.SelectedValue is long projectId)
            {
                _selectedProjectId = projectId;
                UpdateProjectInstallPath();
            }
        }

        private void UpdateProjectInstallPath()
        {
            if (_selectedProjectId.HasValue)
            {
                var selectedProject = _availableProjects.FirstOrDefault(p => p.Id == _selectedProjectId.Value);
                if (selectedProject != null)
                {
                    // For project-based packages, install directly in the project's document root
                    var installPath = selectedProject.DocumentRoot;
                    ProjectInstallPathTextBlock.Text = $"Installation will be at: {installPath}";
                    
                    // Update the installation config if we have one
                    if (_projectInstallationConfig != null)
                    {
                        _projectInstallationConfig.InstallationPath = installPath;
                    }
                }
            }
        }

        private void GenerateConfigurationUI()
        {
            ConfigurationPanel.Children.Clear();
            _configValues.Clear();

            foreach (var option in _package.DefaultConfiguration.AvailableOptions)
            {
                // Use Grid for better alignment, especially for boolean toggles
                if (string.Equals(option.Type, "boolean", StringComparison.OrdinalIgnoreCase))
                {
                    // Create Grid layout for toggle switches (label left, toggle right)
                    var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    
                    // Label
                    var label = new TextBlock 
                    { 
                        Text = option.DisplayName + (option.IsRequired ? " *" : ""),
                        FontWeight = option.IsRequired ? FontWeights.SemiBold : FontWeights.Normal,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                        FontSize = 14
                    };
                    
                    if (!string.IsNullOrEmpty(option.Description))
                    {
                        label.ToolTip = option.Description;
                    }
                    
                    Grid.SetColumn(label, 0);
                    grid.Children.Add(label);

                    // Toggle Switch
                    var inputControl = CreateBooleanInput(option);
                    Grid.SetColumn(inputControl, 1);
                    inputControl.HorizontalAlignment = HorizontalAlignment.Right;
                    inputControl.VerticalAlignment = VerticalAlignment.Center;
                    grid.Children.Add(inputControl);
                    
                    ConfigurationPanel.Children.Add(grid);
                }
                else
                {
                    // Use StackPanel for other input types (text, number, select)
                    var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
                    
                    // Label
                    var label = new TextBlock 
                    { 
                        Text = option.DisplayName + (option.IsRequired ? " *" : ""),
                        FontWeight = option.IsRequired ? FontWeights.SemiBold : FontWeights.Normal,
                        Margin = new Thickness(0, 0, 0, 4),
                        Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                        FontSize = 14
                    };
                    
                    if (!string.IsNullOrEmpty(option.Description))
                    {
                        label.ToolTip = option.Description;
                    }
                    
                    panel.Children.Add(label);

                    // Input Control
                    FrameworkElement inputControl = option.Type.ToLower() switch
                    {
                        "number" => CreateNumberInput(option),
                        "select" => CreateSelectInput(option),
                        _ => CreateTextInput(option)
                    };

                    panel.Children.Add(inputControl);
                    ConfigurationPanel.Children.Add(panel);
                }

                // Store initial value
                _configValues[option.Key] = option.DefaultValue;
            }
        }

        private CheckBox CreateBooleanInput(ConfigOption option)
        {
            var checkBox = new CheckBox
            {
                IsChecked = SafeConvertToBoolean(option.DefaultValue),
                Tag = option.Key,
                Style = (Style)FindResource("ModernToggleSwitch")
            };
            
            checkBox.Checked += (s, e) => _configValues[option.Key] = true;
            checkBox.Unchecked += (s, e) => _configValues[option.Key] = false;
            
            return checkBox;
        }

        private TextBox CreateNumberInput(ConfigOption option)
        {
            var textBox = new TextBox
            {
                Text = SafeConvertToString(option.DefaultValue),
                Tag = option.Key,
                Style = (Style)FindResource("ModernTextBox"),
                Height = 40
            };
            
            // Add numeric input validation
            textBox.PreviewTextInput += (s, e) =>
            {
                // Allow only digits and negative sign at the beginning
                e.Handled = !NumericInputRegex().IsMatch(e.Text);
                
                // Prevent multiple negative signs
                if (e.Text == "-" && textBox.Text.Contains('-'))
                {
                    e.Handled = true;
                }
                
                // Prevent negative sign in the middle
                if (e.Text == "-" && textBox.CaretIndex > 0)
                {
                    e.Handled = true;
                }
            };
            
            // Handle paste operations
            textBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    var clipboardText = Clipboard.GetText();
                    if (!int.TryParse(clipboardText, out _))
                    {
                        e.Handled = true;
                    }
                }
            };
            
            textBox.TextChanged += (s, e) =>
            {
                if (int.TryParse(textBox.Text, out int value))
                {
                    _configValues[option.Key] = value;
                }
                else if (string.IsNullOrEmpty(textBox.Text))
                {
                    _configValues[option.Key] = 0; // Default to 0 for empty input
                }
            };
            
            return textBox;
        }

        private ComboBox CreateSelectInput(ConfigOption option)
        {
            var comboBox = new ComboBox
            {
                ItemsSource = option.Options,
                SelectedItem = SafeConvertToString(option.DefaultValue),
                Tag = option.Key,
                Style = (Style)FindResource("ModernComboBox"),
                Height = 40
            };
            
            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedItem != null)
                {
                    _configValues[option.Key] = comboBox.SelectedItem;
                }
            };
            
            return comboBox;
        }

        private TextBox CreateTextInput(ConfigOption option)
        {
            var textBox = new TextBox
            {
                Text = SafeConvertToString(option.DefaultValue),
                Tag = option.Key,
                Style = (Style)FindResource("ModernTextBox"),
                Height = 40
            };
            
            textBox.TextChanged += (s, e) => _configValues[option.Key] = textBox.Text;
            
            return textBox;
        }

        /// <summary>
        /// Safely converts a value (which might be a JsonElement) to boolean
        /// </summary>
        private static bool SafeConvertToBoolean(object value)
        {
            if (value == null) return false;
            
            // Handle JsonElement
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => bool.TryParse(jsonElement.GetString(), out bool boolResult) && boolResult,
                    JsonValueKind.Number => jsonElement.GetInt32() != 0,
                    _ => false
                };
            }
            
            // Handle regular types
            if (value is bool boolValue) return boolValue;
            if (value is string stringValue) return bool.TryParse(stringValue, out bool result) && result;
            if (value is int intValue) return intValue != 0;
            
            // Fallback to Convert.ToBoolean for other types
            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Safely converts a value (which might be a JsonElement) to string
        /// </summary>
        private static string SafeConvertToString(object value)
        {
            if (value == null) return string.Empty;
            
            // Handle JsonElement
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
                    JsonValueKind.Number => jsonElement.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => string.Empty,
                    _ => jsonElement.GetRawText()
                };
            }
            
            // Handle regular types
            return value.ToString() ?? string.Empty;
        }

        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSizeDisplay();
            UpdateInstallButtonState();
            UpdateInstallPathPreview();
        }
        
        /// <summary>
        /// Updates the install path preview for multi-version packages
        /// </summary>
        private void UpdateInstallPathPreview()
        {
            if (VersionComboBox.SelectedItem is VersionDisplayItem selectedItem)
            {
                var basePath = InstallPathTextBox.Text;
                var versionSpecificPath = GetVersionSpecificInstallPath(basePath, _package.Name, selectedItem.Version.Version);
                
                InstallPathTextBox.ToolTip = $"Installation path will be:\n{versionSpecificPath}";
                
                // Optionally show a preview in a separate UI element (if you want to add one to XAML)
                System.Diagnostics.Debug.WriteLine($"📁 Version-specific install path: {versionSpecificPath}");
            }
        }
        
        
        /// <summary>
        /// Checks if a version is installed, supporting both exact and fuzzy matching
        /// </summary>
        /// <param name="availableVersion">Version from available versions list</param>
        /// <param name="installedVersions">List of detected installed versions</param>
        /// <returns>True if version is considered installed</returns>
        private static bool IsVersionInstalled(string availableVersion, List<string> installedVersions)
        {
            if (installedVersions == null || installedVersions.Count == 0)
                return false;
                
            // Exact match first
            if (installedVersions.Contains(availableVersion, StringComparer.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debug.WriteLine($"✅ Exact match found for {availableVersion}");
                return true;
            }
            
            // Parse available version for fuzzy matching
            var (major, minor, patch) = ParseVersionForMatching(availableVersion);
            System.Diagnostics.Debug.WriteLine($"🔍 Checking fuzzy matches for {availableVersion} (parsed: {major}.{minor}.{patch})");
            
            foreach (var installedVersion in installedVersions)
            {
                var (installedMajor, installedMinor, installedPatch) = ParseVersionForMatching(installedVersion);
                System.Diagnostics.Debug.WriteLine($"  🔍 Comparing with installed {installedVersion} (parsed: {installedMajor}.{installedMinor}.{installedPatch})");
                
                // Match on major.minor (e.g., 8.1.0 matches 8.1.27)
                if (major == installedMajor && 
                    minor == installedMinor)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ FUZZY MATCH CONFIRMED: {availableVersion} matches installed {installedVersion} - RETURNING TRUE");
                    return true;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"❌ No match found for {availableVersion} in [{string.Join(", ", installedVersions)}]");
            return false;
        }
        
        /// <summary>
        /// Parses version string into comparable components
        /// </summary>
        /// <param name="version">Version string to parse</param>
        /// <returns>Version object for comparison</returns>
        private static (int Major, int Minor, int Patch) ParseVersionForMatching(string version)
        {
            try
            {
                // Remove pre-release suffix
                var versionPart = version.Split('-')[0];
                var parts = versionPart.Split('.');
                
                var major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
                var minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
                var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;
                
                System.Diagnostics.Debug.WriteLine($"🔢 Parsed version '{version}' → Major={major}, Minor={minor}, Patch={patch}");
                return (major, minor, patch);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"💥 Error parsing version '{version}': {ex.Message}");
                return (0, 0, 0);
            }
        }


        /// <summary>
        /// Gets the actual installation path for a specific version
        /// </summary>
        /// <param name="basePath">Base installation path</param>
        /// <param name="packageName">Package name</param>
        /// <param name="version">Version to install</param>
        /// <returns>Version-specific installation path</returns>
        private static string GetVersionSpecificInstallPath(string basePath, string packageName, string version)
        {
            // All packages now support multiple versions
            var versionFolder = $"{packageName.ToLower()}-{version}";
            return Path.Combine(basePath, versionFolder);
        }

        /// <summary>
        /// Optimized visual tree search with depth limiting
        /// </summary>
        /// <typeparam name="T">Type of control to find</typeparam>
        /// <param name="parent">Parent element to search from</param>
        /// <param name="name">Optional name to match</param>
        /// <param name="maxDepth">Maximum search depth to prevent deep recursion</param>
        /// <returns>Found element or null</returns>
        private T? FindVisualChild<T>(DependencyObject parent, string? name = null, int maxDepth = 10) where T : DependencyObject
        {
            if (parent == null || maxDepth <= 0)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild)
                {
                    if (string.IsNullOrEmpty(name) || (child as FrameworkElement)?.Name == name)
                    {
                        return typedChild;
                    }
                }
                
                var childOfChild = FindVisualChild<T>(child, name, maxDepth - 1);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private void UpdateSizeDisplay()
        {
            if (VersionComboBox.SelectedItem is VersionDisplayItem selectedItem)
            {
                SizeTextBlock.Text = FormatFileSize(selectedItem.Version.Size);
            }
        }
        
        /// <summary>
        /// Helper method to set InstallButton state and appearance
        /// </summary>
        private void SetInstallButtonState(string icon, string text, Brush background, Brush textColor, bool isEnabled, string tooltip)
        {
            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            stackPanel.Children.Add(new TextBlock { 
                Text = icon, 
                Margin = new Thickness(0, 0, 8, 0), 
                FontSize = 14 
            });
            stackPanel.Children.Add(new TextBlock { 
                Text = text, 
                Foreground = textColor, 
                FontWeight = FontWeights.SemiBold 
            });

            InstallButton.Content = stackPanel;
            InstallButton.Background = background;
            InstallButton.ToolTip = tooltip;
            InstallButton.IsEnabled = isEnabled;
        }

        private void UpdateInstallButtonState()
        {
            if (VersionComboBox.SelectedItem is VersionDisplayItem selectedItem)
            {
                if (selectedItem.IsInstalled)
                {
                    SetInstallButtonState(
                        "✅", 
                        "Already Installed", 
                        System.Windows.Media.Brushes.DarkGray,
                        System.Windows.Media.Brushes.Gray,
                        false,
                        $"Version {selectedItem.Version.Version} is already installed"
                    );
                }
                else
                {
                    SetInstallButtonState(
                        "📥", 
                        "Install", 
                        System.Windows.Media.Brushes.DodgerBlue,
                        System.Windows.Media.Brushes.White,
                        true,
                        $"Install version {selectedItem.Version.Version}"
                    );
                }
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB"];
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private void BrowsePathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                folderDialog.Description = $"Select installation directory for {_package.DisplayName}";
                folderDialog.UseDescriptionForTitle = true;
                
                // Set initial directory to current path if it exists
                if (!string.IsNullOrWhiteSpace(InstallPathTextBox.Text) && Directory.Exists(InstallPathTextBox.Text))
                {
                    folderDialog.SelectedPath = InstallPathTextBox.Text;
                }
                else
                {
                    // Default to ModStack services directory
                    var defaultPath = @"C:\ModStack\Services";
                    if (Directory.Exists(defaultPath))
                    {
                        folderDialog.SelectedPath = defaultPath;
                    }
                    else
                    {
                        // Fallback to user's Documents folder
                        folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    }
                }

                var result = folderDialog.ShowDialog();
                
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                {
                    InstallPathTextBox.Text = folderDialog.SelectedPath;
                    System.Diagnostics.Debug.WriteLine($"📁 Selected installation path: {folderDialog.SelectedPath}");
                }
            }
            catch (Exception ex)
            {
                ModernDialogService.ShowError($"Error opening folder browser: {ex.Message}", "Error");
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetInstallButtonState(
                    "⏳", 
                    "Installing...", 
                    System.Windows.Media.Brushes.Orange,
                    System.Windows.Media.Brushes.White,
                    false,
                    "Installation in progress..."
                );

                // Validate configuration
                if (!ValidateConfiguration())
                {
                    return;
                }

                // Get selected version
                if (VersionComboBox.SelectedItem is not VersionDisplayItem selectedItem)
                {
                    ModernDialogService.ShowWarning("Please select a version.", "Validation Error");
                    return;
                }
                
                var selectedVersion = selectedItem.Version;
                
                if (selectedItem.IsInstalled)
                {
                    ModernDialogService.ShowInformation($"Version {selectedVersion.Version} is already installed.", "Already Installed");
                    return;
                }

                // Create installation configuration
                string installPath;
                if (_package.PackageType == Common.Enums.PackageType.StandaloneWebApp || 
                    _package.PackageType == Common.Enums.PackageType.Framework ||
                    _package.PackageType == Common.Enums.PackageType.Tool)
                {
                    // Use project selection for project-based packages
                    if (!_selectedProjectId.HasValue)
                    {
                        ModernDialogService.ShowError("Please select a target project.", "Project Selection Required");
                        return;
                    }
                    
                    var selectedProject = _availableProjects.FirstOrDefault(p => p.Id == _selectedProjectId.Value);
                    if (selectedProject == null)
                    {
                        ModernDialogService.ShowError("Selected project not found.", "Project Error");
                        return;
                    }
                    
                    installPath = selectedProject.DocumentRoot;
                }
                else
                {
                    // Use default path for service packages
                    installPath = GetVersionSpecificInstallPath(InstallPathTextBox.Text, _package.Name, selectedVersion.Version);
                }
                
                var installConfig = new PackageInstallationConfig
                {
                    Package = _package,
                    Version = selectedVersion,
                    InstallPath = installPath,
                    AutoStart = (_package.PackageType != Common.Enums.PackageType.StandaloneWebApp && 
                               _package.PackageType != Common.Enums.PackageType.Framework &&
                               _package.PackageType != Common.Enums.PackageType.Tool) 
                               ? (AutoStartCheckBox.IsChecked ?? false) : false,
                    RegisterAsService = (_package.PackageType != Common.Enums.PackageType.StandaloneWebApp && 
                                        _package.PackageType != Common.Enums.PackageType.Framework &&
                                        _package.PackageType != Common.Enums.PackageType.Tool) 
                                        ? (RegisterServiceCheckBox.IsChecked ?? false) : false,
                    Configuration = _configValues
                };
                
                System.Diagnostics.Debug.WriteLine($"📁 Installation will use path: {installConfig.InstallPath}");
                System.Diagnostics.Debug.WriteLine($"⚙️ Configuration values being sent to installer:");
                System.Diagnostics.Debug.WriteLine($"   Configuration count: {_configValues.Count}");
                foreach (var kvp in _configValues)
                {
                    System.Diagnostics.Debug.WriteLine($"   {kvp.Key} = {kvp.Value} (Type: {kvp.Value?.GetType().Name ?? "null"})");
                }

                // Show progress window and start installation
                var progressWindow = new ServiceInstallProgressWindow($"Installing {_package.DisplayName}")
                {
                    Owner = this
                };
                progressWindow.Show();

                var progressReporter = new ProgressWindowReporter(progressWindow);
                
                // Run installation in background task to prevent UI blocking
                bool success = false;
                
                // Create cancellation token source with timeout (30 minutes)
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutCts.Token, 
                    progressWindow.CancellationToken);
                
                try
                {
                    // Start installation in background task with timeout
                    var installTask = Task.Run(async () =>
                    {
                        try
                        {
                            // Route to appropriate installer based on package type
                            if (_package.PackageType == Common.Enums.PackageType.Framework || 
                                _package.PackageType == Common.Enums.PackageType.StandaloneWebApp)
                            {
                                // Create project installation config from selected project
                                var selectedProject = _availableProjects.FirstOrDefault(p => p.Id == _selectedProjectId!.Value);
                                if (selectedProject != null)
                                {
                                    _projectInstallationConfig = new ProjectPackageInstallationConfig
                                    {
                                        InstallationPath = installPath,
                                        TargetProject = new ModStack.Common.Models.Project
                                        {
                                            Id = selectedProject.Id,
                                            Name = selectedProject.DisplayName,
                                            Path = selectedProject.DocumentRoot,
                                            Domain = selectedProject.Domain,
                                            DocumentRoot = selectedProject.DocumentRoot
                                        }
                                    };
                                }
                                
                                if (_package.PackageType == Common.Enums.PackageType.Framework)
                                {
                                    return await _frameworkInstaller.InstallFrameworkPackageAsync(
                                        _package, 
                                        selectedVersion, 
                                        _projectInstallationConfig!, 
                                        progressReporter);
                                }
                                else
                                {
                                    return await _frameworkInstaller.InstallStandaloneWebAppAsync(
                                        _package, 
                                        selectedVersion, 
                                        _projectInstallationConfig!, 
                                        progressReporter);
                                }
                            }
                            else
                            {
                                return await _packageInstaller.InstallPackageAsync(installConfig, progressReporter);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log the error and return false
                            System.Diagnostics.Debug.WriteLine($"Installation error: {ex.Message}");
                            return false;
                        }
                    }, linkedCts.Token);
                    
                    // Wait for installation to complete with timeout
                    success = await installTask;
                }
                catch (TaskCanceledException)
                {
                    if (timeoutCts.IsCancellationRequested)
                    {
                        ModernDialogService.ShowError(
                            "Installation timed out after 30 minutes. The installation may still be running in the background.", 
                            "Installation Timeout");
                    }
                    else
                    {
                        ModernDialogService.ShowInformation("Installation was cancelled.", "Installation Cancelled");
                    }
                    return;
                }
                catch (OperationCanceledException)
                {
                    if (timeoutCts.IsCancellationRequested)
                    {
                        ModernDialogService.ShowError(
                            "Installation timed out after 30 minutes. The installation may still be running in the background.", 
                            "Installation Timeout");
                    }
                    else
                    {
                        ModernDialogService.ShowInformation("Installation was cancelled.", "Installation Cancelled");
                    }
                    return;
                }
                finally
                {
                    progressWindow.Close();
                }

                if (success)
                {
                    var message = _package.PackageType == Common.Enums.PackageType.Framework || 
                                  _package.PackageType == Common.Enums.PackageType.StandaloneWebApp
                        ? $"{_package.DisplayName} has been installed successfully!\n\n" +
                          $"Access it at: {GetProjectAccessUrl()}"
                        : $"{_package.DisplayName} has been installed successfully!";
                    
                    ModernDialogService.ShowInformation(message, "Installation Complete");
                    
                    // Notify all views that a service has been installed
                    MainWindow.NotifyServiceInstalled();
                    
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    ModernDialogService.ShowError($"Failed to install {_package.DisplayName}. Please check the logs for details.", "Installation Failed");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                ModernDialogService.ShowError($"Access denied: {ex.Message}\n\nPlease run as administrator or check folder permissions.", "Permission Error");
            }
            catch (DirectoryNotFoundException ex)
            {
                ModernDialogService.ShowError($"Directory not found: {ex.Message}", "Path Error");
            }
            catch (IOException ex)
            {
                ModernDialogService.ShowError($"File system error: {ex.Message}", "File System Error");
            }
            catch (Exception ex)
            {
                ModernDialogService.ShowError($"Unexpected installation error: {ex.Message}", "Installation Error");
            }
            finally
            {
                // Reset button to normal install state
                UpdateInstallButtonState();
            }
        }

        private bool ValidateConfiguration()
        {
            // Check required fields
            foreach (var option in _package.DefaultConfiguration.AvailableOptions.Where(o => o.IsRequired))
            {
                if (!_configValues.TryGetValue(option.Key, out var value) || 
                    value == null ||
                    string.IsNullOrWhiteSpace(value.ToString()))
                {
                    ModernDialogService.ShowWarning($"Please provide a value for {option.DisplayName}.", "Validation Error");
                    return false;
                }
            }

            // Validate version selection
            if (VersionComboBox.SelectedItem is not VersionDisplayItem selectedItem)
            {
                ModernDialogService.ShowWarning("Please select a version to install.", "Validation Error");
                return false;
            }

            // Validate installation path or project selection based on package type
            string actualInstallPath;
            if (_package.PackageType == Common.Enums.PackageType.StandaloneWebApp || 
                _package.PackageType == Common.Enums.PackageType.Framework ||
                _package.PackageType == Common.Enums.PackageType.Tool)
            {
                // For project-based packages, validate project selection
                if (!_selectedProjectId.HasValue)
                {
                    ModernDialogService.ShowWarning("Please select a target project.", "Project Selection Required");
                    return false;
                }
                
                var selectedProject = _availableProjects.FirstOrDefault(p => p.Id == _selectedProjectId.Value);
                if (selectedProject == null)
                {
                    ModernDialogService.ShowWarning("Selected project not found.", "Project Error");
                    return false;
                }
                
                actualInstallPath = selectedProject.DocumentRoot;
            }
            else
            {
                // For service packages, validate installation path
                if (string.IsNullOrWhiteSpace(InstallPathTextBox.Text))
                {
                    ModernDialogService.ShowWarning("Please provide an installation path.", "Validation Error");
                    return false;
                }
                
                actualInstallPath = GetVersionSpecificInstallPath(
                    InstallPathTextBox.Text, 
                    _package.Name, 
                    selectedItem.Version.Version);
            }

            // All packages now support multiple versions, check if this specific version is already installed
            if (!ValidateDirectoryAccess(actualInstallPath, true))
            {
                return false;
            }
            
            try
            {
                if (Directory.Exists(actualInstallPath) && Directory.GetFileSystemEntries(actualInstallPath).Length > 0)
                {
                    var result = ModernDialogService.Show(
                        $"Version {selectedItem.Version.Version} appears to already be installed at:\n{actualInstallPath}\n\nContinue anyway?", 
                        "Version Already Installed", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Question);
                    if (result != MessageBoxResult.Yes)
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                ModernDialogService.ShowError($"Error checking installation directory: {ex.Message}", "Directory Access Error");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates directory access permissions and path validity
        /// </summary>
        /// <param name="path">Directory path to validate</param>
        /// <param name="createIfMissing">Whether to attempt creating the directory if it doesn't exist</param>
        /// <returns>True if directory is accessible</returns>
        private static bool ValidateDirectoryAccess(string path, bool createIfMissing)
        {
            try
            {
                // Check if path is valid
                if (string.IsNullOrWhiteSpace(path))
                {
                    ModernDialogService.ShowWarning("Installation path cannot be empty.", "Validation Error");
                    return false;
                }

                // Check for invalid path characters
                if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    ModernDialogService.ShowWarning("Installation path contains invalid characters.", "Validation Error");
                    return false;
                }

                // Check if directory exists
                if (!Directory.Exists(path))
                {
                    if (createIfMissing)
                    {
                        // Try to create the directory to test permissions
                        var parentDir = Directory.GetParent(path)?.FullName;
                        if (parentDir != null && !Directory.Exists(parentDir))
                        {
                            Directory.CreateDirectory(parentDir);
                        }
                        Directory.CreateDirectory(path);
                    }
                    else
                    {
                        // Check if parent directory exists and is writable
                        var parentDir = Directory.GetParent(path)?.FullName;
                        if (parentDir == null || !Directory.Exists(parentDir))
                        {
                            ModernDialogService.ShowWarning($"Parent directory does not exist: {parentDir}", "Path Error");
                            return false;
                        }

                        // Test write access to parent directory
                        var testFile = Path.Combine(parentDir, Path.GetRandomFileName());
                        File.WriteAllText(testFile, "test");
                        File.Delete(testFile);
                    }
                }
                else
                {
                    // Test write access to existing directory
                    var testFile = Path.Combine(path, Path.GetRandomFileName());
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                ModernDialogService.ShowError($"Access denied to directory: {path}\n\nPlease run as administrator or choose a different location.", "Permission Error");
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                ModernDialogService.ShowError($"Directory not found: {path}", "Path Error");
                return false;
            }
            catch (IOException ex)
            {
                ModernDialogService.ShowError($"File system error: {ex.Message}", "File System Error");
                return false;
            }
            catch (Exception ex)
            {
                ModernDialogService.ShowError($"Error validating directory access: {ex.Message}", "Validation Error");
                return false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Handle window activation to ensure parent windows are also brought to front
        /// </summary>
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            
            // Bring parent windows to front when this window is activated
            BringParentWindowsToFront();
        }
        
        /// <summary>
        /// Brings all parent windows to the front
        /// </summary>
        private void BringParentWindowsToFront()
        {
            if (Owner != null)
            {
                // Bring the immediate parent to front
                Owner.Activate();
                Owner.BringIntoView();
                
                // If the parent has an owner, bring that to front too (recursive)
                var currentParent = Owner;
                while (currentParent != null)
                {
                    currentParent.Activate();
                    currentParent.BringIntoView();
                    currentParent = currentParent.Owner;
                }
            }
        }

    }

    /// <summary>
    /// Progress reporter that updates the progress window
    /// </summary>
    public class ProgressWindowReporter(ServiceInstallProgressWindow progressWindow) : IProgressReporter
    {
        private readonly ServiceInstallProgressWindow _progressWindow = progressWindow;

        public void UpdateProgress(string message, int percentage)
        {
            _progressWindow.UpdateProgress(message, percentage);
        }
    }

    /// <summary>
    /// Wrapper class for version display with installation status
    /// </summary>
    public class VersionDisplayItem
    {
        public PackageVersion Version { get; set; } = new();
        public bool IsInstalled { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        
        /// <summary>
        /// Enhanced display text with status indicators
        /// </summary>
        public string EnhancedDisplayText
        {
            get
            {
                var text = DisplayText;
                
                if (IsInstalled)
                {
                    text += " (Installed)";
                }
                else if (Version.IsRecommended)
                {
                    text += " (Recommended)";
                }
                
                return text;
            }
        }
        
        public override string ToString() => EnhancedDisplayText;
    }

    /// <summary>
    /// Helper methods for PackageInstallDialog
    /// </summary>
    public partial class PackageInstallDialog
    {
        private string GetProjectAccessUrl()
        {
            if (_projectInstallationConfig != null)
            {
                string domain;
                int port;

                if (!string.IsNullOrEmpty(_projectInstallationConfig.CustomDomain))
                {
                    domain = _projectInstallationConfig.CustomDomain;
                    port = _projectInstallationConfig.CustomPort > 0 ? _projectInstallationConfig.CustomPort : 80;
                }
                else if (_projectInstallationConfig.CreateSubdomain && !string.IsNullOrEmpty(_projectInstallationConfig.Subdomain))
                {
                    var baseDomain = _projectInstallationConfig.TargetProject.Domain ?? 
                                   $"{_projectInstallationConfig.TargetProject.Name.ToLower()}.local";
                    domain = $"{_projectInstallationConfig.Subdomain}.{baseDomain}";
                    port = 80;
                }
                else
                {
                    domain = _projectInstallationConfig.TargetProject.Domain ?? 
                            $"{_projectInstallationConfig.TargetProject.Name.ToLower()}.local";
                    port = 80;
                }

                var scheme = _projectInstallationConfig.EnableSSL ? "https" : "http";
                var portString = (port == 80 || port == 443) ? "" : $":{port}";
                return $"{scheme}://{domain}{portString}/{_projectInstallationConfig.InstallationPath}";
            }

            if (_package.ProjectConfiguration?.DefaultDomain != null)
            {
                var scheme = _package.ProjectConfiguration.EnableSSL ? "https" : "http";
                var port = _package.ProjectConfiguration.DefaultPort == 80 || _package.ProjectConfiguration.DefaultPort == 443 
                    ? "" 
                    : $":{_package.ProjectConfiguration.DefaultPort}";
                return $"{scheme}://{_package.ProjectConfiguration.DefaultDomain}{port}";
            }
            
            return $"{_package.Name.ToLower()}.local";
        }

        private async Task<bool?> ShowProjectSelectionDialogAsync()
        {
            try
            {
                // Get available projects
                var projects = await _projectManager.GetActiveProjectsAsync();
                if (projects == null)
                {
                    ModernDialogService.ShowError("Project manager service not available.", "Service Error");
                    return false;
                }

                var availableProjects = projects;
                if (availableProjects.Count == 0)
                {
                    var createProjectResult = ModernDialogService.Show(
                        "No active projects found. Would you like to create a new project first?",
                        "No Projects Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (createProjectResult == MessageBoxResult.Yes)
                    {
                        // Open project creation dialog
                        // This would need to be implemented based on your project creation UI
                        ModernDialogService.ShowInformation("Please create a project first using the Project Manager.", "Create Project Required");
                    }
                    return false;
                }

                // Show project selection dialog
                var selectedVersionItem = VersionComboBox.SelectedItem as VersionDisplayItem;
                if (selectedVersionItem?.Version == null)
                {
                    ModernDialogService.ShowError("Please select a version before proceeding.", "Version Required");
                    return false;
                }
                
                var dialog = _viewFactoryService.CreateDialog<ProjectSelectionDialog>(_package, selectedVersionItem.Version, availableProjects);
                dialog.Owner = this;

                var result = dialog.ShowDialog();
                if (result == true)
                {
                    _projectInstallationConfig = dialog.InstallationConfig;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                ModernDialogService.ShowError($"Failed to show project selection: {ex.Message}", "Error");
                return false;
            }
        }
    }

    /// <summary>
    /// Represents a project item for selection in the ComboBox
    /// </summary>
    public class ProjectSelectionItem
    {
        public long Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string DocumentRoot { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
    }
}
