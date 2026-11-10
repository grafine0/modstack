using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ModStack.GUI.Views.Dialogs;
using ModStack.Common.Models;

namespace ModStack.GUI.Services;

/// <summary>
/// Service for showing modern confirmation dialogs
/// </summary>
public static class ModernDialogService
{
    /// <summary>
    /// Show a modern confirmation dialog
    /// </summary>
    public static MessageBoxResult Show(string message, string title = "Confirmation", 
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information,
        List<string>? serviceList = null, string? details = null)
    {
        return ModernConfirmationDialog.Show(message, title, buttons, icon, serviceList, details);
    }

    /// <summary>
    /// Show a modern confirmation dialog for stack creation
    /// </summary>
    public static MessageBoxResult ShowStackCreationConfirmation(string stackName, List<string> services)
    {
        var message = $"Stack '{stackName}' would be created with {services.Count} services:";
        return ModernConfirmationDialog.Show(
            message: message,
            title: "Stack Creation",
            buttons: MessageBoxButton.OK,
            icon: MessageBoxImage.Information,
            serviceList: services
        );
    }

    /// <summary>
    /// Show a modern confirmation dialog for service deletion
    /// </summary>
    public static MessageBoxResult ShowServiceDeletionConfirmation(string serviceName, string servicePath)
    {
        var message = $"Are you sure you want to delete the service '{serviceName}'?";
        var details = $@"This will:
• Stop the service if it's running
• Uninstall the Windows service (if registered)
• Delete all service files from: {servicePath}
• Remove the service from ModStack

⚠️ WARNING: This action cannot be undone!

Do you want to continue?";
        
        return ModernConfirmationDialog.Show(
            message: message,
            title: "Delete Service",
            buttons: MessageBoxButton.YesNo,
            icon: MessageBoxImage.Warning,
            details: details
        );
    }

    /// <summary>
    /// Show a modern confirmation dialog for project deletion
    /// </summary>
    public static ProjectDeletionResult ShowProjectDeletionConfirmation(string projectName, string projectPath, string stackName,
        List<string> virtualHostFiles, List<string> serviceNames)
    {
        var dialog = new ProjectDeletionConfirmationDialog(projectName, projectPath, stackName, virtualHostFiles, serviceNames);
        
        // Set owner to the main window if possible
        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow != null && mainWindow.IsLoaded)
        {
            dialog.Owner = mainWindow;
        }
        
        Helpers.DialogBlurHelper.ShowDialogWithBlur(dialog);
        
        return new ProjectDeletionResult
        {
            Confirmed = dialog.DialogResult == true,
            DeleteFiles = dialog.DeleteFiles
        };
    }

    /// <summary>
    /// Show a modern confirmation dialog for service deletion with file deletion options
    /// </summary>
    public static ServiceDeletionResult ShowServiceDeletionConfirmation(ServiceDisplayItem service)
    {
        var dialog = new ServiceDeletionConfirmationDialog(service);
        
        // Set owner to the main window if possible
        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow != null && mainWindow.IsLoaded)
        {
            dialog.Owner = mainWindow;
        }
        
        Helpers.DialogBlurHelper.ShowDialogWithBlur(dialog);
        
        return new ServiceDeletionResult
        {
            Confirmed = dialog.DialogResult == true,
            DeleteFiles = dialog.DeleteFiles
        };
    }

    /// <summary>
    /// Show a modern confirmation dialog for stack deletion
    /// </summary>
    public static MessageBoxResult ShowStackDeletionConfirmation(string stackName, int serviceCount)
    {
        var message = $"Are you sure you want to delete the stack '{stackName}'?";
        var details = $@"This will:
• Remove the stack from ModStack
• Remove {serviceCount} service(s) from the stack
• Keep individual services intact (they can be used in other stacks)

⚠️ WARNING: This action cannot be undone!

Do you want to continue?";
        
        return ModernConfirmationDialog.Show(
            message: message,
            title: "Delete Stack",
            buttons: MessageBoxButton.YesNo,
            icon: MessageBoxImage.Warning,
            details: details
        );
    }

    /// <summary>
    /// Show a modern confirmation dialog for Apache installation options
    /// </summary>
    public static MessageBoxResult ShowApacheInstallationOptions()
    {
        var message = "Apache HTTP Server is not installed. Choose an option:";
        var details = @"🚀 RECOMMENDED: Development Server (Instant)
• No download required
• Perfect for testing projects
• Starts immediately

📦 Full Apache Installation
• Download and install real Apache
• Production-ready features
• Requires internet connection

Click 'Yes' for Development Server (recommended)
Click 'No' for Full Installation
Click 'Cancel' to abort";
        
        return ModernConfirmationDialog.Show(
            message: message,
            title: "Choose Apache Option",
            buttons: MessageBoxButton.YesNoCancel,
            icon: MessageBoxImage.Question,
            details: details
        );
    }

    /// <summary>
    /// Show a modern confirmation dialog for PHP installation
    /// </summary>
    public static MessageBoxResult ShowPHPInstallationConfirmation()
    {
        var message = "PHP is not installed. Would you like ModStack to download and install PHP automatically?";
        var details = @"This will:
• Download PHP 8.3.0 (latest stable)
• Install to C:\ModStack\Services\PHP
• Configure with common extensions
• Add to Windows PATH

Download size: ~25MB
Estimated time: 1-2 minutes";
        
        return ModernConfirmationDialog.Show(
            message: message,
            title: "Install PHP",
            buttons: MessageBoxButton.YesNo,
            icon: MessageBoxImage.Question,
            details: details
        );
    }

    /// <summary>
    /// Show a modern confirmation dialog for MySQL installation
    /// </summary>
    public static MessageBoxResult ShowMySQLInstallationConfirmation()
    {
        var message = "MySQL is not installed. Would you like ModStack to download and install MySQL Server automatically?";
        var details = @"This will:
• Download MySQL 8.0.35 (latest stable)
• Install to C:\ModStack\Services\MySQL
• Configure as Windows Service
• Set up default configuration

Download size: ~350MB
Estimated time: 3-5 minutes";
        
        return ModernConfirmationDialog.Show(
            message: message,
            title: "Install MySQL Server",
            buttons: MessageBoxButton.YesNo,
            icon: MessageBoxImage.Question,
            details: details
        );
    }

    /// <summary>
    /// Show a modern information dialog
    /// </summary>
    public static MessageBoxResult ShowInformation(string message, string title = "Information")
    {
        return ModernConfirmationDialog.Show(
            message: message,
            title: title,
            buttons: MessageBoxButton.OK,
            icon: MessageBoxImage.Information
        );
    }

    /// <summary>
    /// Show a modern warning dialog
    /// </summary>
    public static MessageBoxResult ShowWarning(string message, string title = "Warning")
    {
        return ModernConfirmationDialog.Show(
            message: message,
            title: title,
            buttons: MessageBoxButton.OK,
            icon: MessageBoxImage.Warning
        );
    }

    /// <summary>
    /// Show a modern error dialog
    /// </summary>
    public static MessageBoxResult ShowError(string message, string title = "Error")
    {
        return ModernConfirmationDialog.Show(
            message: message,
            title: title,
            buttons: MessageBoxButton.OK,
            icon: MessageBoxImage.Error
        );
    }

    /// <summary>
    /// Show a modern confirmation dialog for package deletion
    /// </summary>
    public static MessageBoxResult ShowPackageDeletionConfirmation(string packageName, int versionCount)
    {
        var message = $"Are you sure you want to delete the package '{packageName}'?";
        var details = $@"This will:
• Remove the package from the catalog
• Delete all associated versions ({versionCount} version(s))
• Remove package configuration settings
• NOT affect any currently installed instances

⚠️ WARNING: This action cannot be undone!

Do you want to continue?";
        
        return ModernConfirmationDialog.Show(
            message: message,
            title: "Delete Package",
            buttons: MessageBoxButton.YesNo,
            icon: MessageBoxImage.Warning,
            details: details
        );
    }

    /// <summary>
    /// Show a modern confirmation dialog for service uninstallation
    /// </summary>
    public static MessageBoxResult ShowServiceUninstallConfirmation(ServiceUninstallValidation validation)
    {
        var packageInfo = validation.Version != null ? 
            $"{validation.PackageName} v{validation.Version}" : 
            validation.PackageName;

        var message = $"Are you sure you want to uninstall '{packageInfo}'?";
        
        var details = "This will:\n";
        
        // Add affected services
        if (validation.AffectedServices.Any())
        {
            details += $"• Remove {validation.AffectedServices.Count} installed service(s):\n";
            foreach (var service in validation.AffectedServices)
            {
                details += $"  - {service.DisplayName}\n";
            }
        }

        // Add required actions
        if (validation.RequiredActions.Any())
        {
            details += "\nRequired actions:\n";
            foreach (var action in validation.RequiredActions)
            {
                details += $"• {action}\n";
            }
        }

        // Add warnings
        if (validation.HasWarnings)
        {
            details += "\n⚠️ WARNINGS:\n";
            foreach (var warning in validation.Warnings)
            {
                details += $"• {warning}\n";
            }
        }

        // Add running services warning
        if (validation.HasRunningServices)
        {
            details += $"\n🛑 RUNNING SERVICES WILL BE STOPPED:\n";
            foreach (var service in validation.RunningServices)
            {
                details += $"• {service.DisplayName} (currently running)\n";
            }
        }

        details += "\n⚠️ WARNING: This action cannot be undone!\n\nDo you want to continue?";

        var icon = validation.HasRunningServices ? MessageBoxImage.Exclamation : MessageBoxImage.Warning;

        return ModernConfirmationDialog.Show(
            message: message,
            title: "Uninstall Service",
            buttons: MessageBoxButton.YesNo,
            icon: icon,
            details: details
        );
    }

    /// <summary>
    /// Shows a comprehensive version deletion confirmation dialog
    /// </summary>
    /// <param name="packageName">Name of the package</param>
    /// <param name="version">Version to delete</param>
    /// <param name="validation">Service validation results</param>
    /// <returns>Dialog result with deletion preferences</returns>
    public static VersionDeletionResult ShowVersionDeletionConfirmation(string packageName, string version, ServiceUninstallValidation validation)
    {
        var dialog = new VersionDeletionConfirmationDialog(packageName, version, validation);
        
        // Set owner to the main window if possible
        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow != null && mainWindow.IsLoaded)
        {
            dialog.Owner = mainWindow;
        }
        
        dialog.ShowDialog();
        
        return new VersionDeletionResult
        {
            Confirmed = dialog.DialogResult == true,
            DeleteFiles = dialog.DeleteFiles
        };
    }
}

/// <summary>
/// Result of version deletion confirmation dialog
/// </summary>
public class VersionDeletionResult
{
    public bool Confirmed { get; set; }
    public bool DeleteFiles { get; set; }
}

/// <summary>
/// Result of project deletion confirmation dialog
/// </summary>
public class ProjectDeletionResult
{
    public bool Confirmed { get; set; }
    public bool DeleteFiles { get; set; }
}

public class ServiceDeletionResult
{
    public bool Confirmed { get; set; }
    public bool DeleteFiles { get; set; }
}
