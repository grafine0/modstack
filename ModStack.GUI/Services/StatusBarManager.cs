using System.Windows.Controls;
using ModStack.Common.Models;
using ModStack.Common;
using ModStack.GUI.Services;

namespace ModStack.GUI.Services;

/// <summary>
/// Manages status bar updates for views
/// </summary>
public class StatusBarManager(
    TextBlock? statusTextBlock = null,
    TextBlock? serviceCountTextBlock = null,
    TextBlock? runningCountTextBlock = null,
    TextBlock? unknownCountTextBlock = null)
{
    private readonly TextBlock? _statusTextBlock = statusTextBlock;
    private readonly TextBlock? _serviceCountTextBlock = serviceCountTextBlock;
    private readonly TextBlock? _runningCountTextBlock = runningCountTextBlock;
    private readonly TextBlock? _unknownCountTextBlock = unknownCountTextBlock;

    /// <summary>
    /// Updates the status bar with service information
    /// </summary>
    /// <param name="services">Collection of services to analyze</param>
    /// <param name="stackName">Name of the current stack</param>
    /// <param name="lastUpdated">When the data was last updated</param>
    public void UpdateServiceStatusBar<T>(IEnumerable<T> services, string? stackName = null, DateTime? lastUpdated = null) where T : class
    {
        try
        {
            // Check if UI elements are initialized
            if (!AreUIElementsInitialized())
            {
                LogService.Info("Status bar UI elements not yet initialized");
                return;
            }

            var totalServices = services?.Count() ?? 0;
            var runningServices = 0;
            var unknownServices = 0;

            // Count services by status if they have a Service property with Status
            if (services != null)
            {
                foreach (var service in services)
                {
                    var statusProperty = service.GetType().GetProperty("Service");
                    if (statusProperty?.GetValue(service) is ServiceDefinition serviceDef)
                    {
                        switch (serviceDef.Status)
                        {
                            case (int)ServiceStatus.Running:
                                runningServices++;
                                break;
                            case (int)ServiceStatus.Unknown:
                                unknownServices++;
                                break;
                        }
                    }
                }
            }

            UpdateTextBlocks(totalServices, runningServices, unknownServices, stackName, lastUpdated);
        }
        catch (Exception ex)
        {
            LogService.Error($"Error updating status bar: {ex.Message}");
            SetErrorStatus();
        }
    }

    private void UpdateTextBlocks(int totalServices, int runningServices, int unknownServices, string? stackName, DateTime? lastUpdated)
    {
        if (_serviceCountTextBlock != null)
            _serviceCountTextBlock.Text = $"{totalServices} services";
        
        if (_runningCountTextBlock != null)
            _runningCountTextBlock.Text = $"{runningServices} running";
        
        if (_unknownCountTextBlock != null)
            _unknownCountTextBlock.Text = $"{unknownServices} unknown";

        // Update status text with more detailed information
        if (_statusTextBlock != null)
        {
            if (totalServices > 0)
            {
                var stackInfo = !string.IsNullOrEmpty(stackName) ? $"Stack: {stackName} | " : "";
                var timeInfo = lastUpdated.HasValue ? $"Last updated: {lastUpdated.Value:HH:mm:ss}" : "";
                _statusTextBlock.Text = $"{stackInfo}{timeInfo}";
            }
            else
            {
                _statusTextBlock.Text = "No services available";
            }
        }
    }

    private bool AreUIElementsInitialized()
    {
        return _statusTextBlock != null && 
               _serviceCountTextBlock != null && 
               _runningCountTextBlock != null && 
               _unknownCountTextBlock != null;
    }

    private void SetErrorStatus()
    {
        if (_statusTextBlock != null)
        {
            _statusTextBlock.Text = "Error updating status";
        }
    }

    /// <summary>
    /// Sets the UI elements for this status bar manager
    /// </summary>
    public static void SetUIElements(
        TextBlock? _ = null,
        TextBlock? __ = null,
        TextBlock? ___ = null,
        TextBlock? ____ = null)
    {
        // This method can be used to update UI elements after initialization
        // For now, we'll keep the constructor-based approach
    }
}
