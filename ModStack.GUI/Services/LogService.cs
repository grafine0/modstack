using System;
using System.IO;
using System.Threading.Tasks;
using ModStack.GUI.Views;
using ModStack.Logging;

namespace ModStack.GUI.Services;

public static class LogService
{
    private static ServiceLogsView? _serviceLogsView;

    public static void Info(string message)
    {
        ModStack.Logging.LogService.Info(message);
        UpdateLogViewers(message, ModStack.Logging.LogLevel.Info);
    }

    public static void Warning(string message)
    {
        ModStack.Logging.LogService.Warning(message);
        UpdateLogViewers(message, ModStack.Logging.LogLevel.Warning);
    }

    public static void Error(string message)
    {
        ModStack.Logging.LogService.Error(message);
        UpdateLogViewers(message, ModStack.Logging.LogLevel.Error);
    }

    public static void Error(string message, Exception ex)
    {
        ModStack.Logging.LogService.Error(message, ex);
        UpdateLogViewers($"{message}: {ex.Message}", ModStack.Logging.LogLevel.Error);
    }

    /// <summary>
    /// Logs an exception with consistent formatting including context, stack trace, and inner exception
    /// </summary>
    public static void LogException(string context, Exception ex)
    {
        Error($"❌ {context}: {ex.Message ?? "No message"}", ex);
        if (ex.StackTrace != null)
        {
            Error($"❌ StackTrace: {ex.StackTrace}");
        }
        if (ex.InnerException != null)
        {
            Error($"❌ Inner: {ex.InnerException.Message ?? "No inner message"}");
        }
    }

    public static void SetLoggingEnabled(bool enabled)
    {
        ModStack.Logging.LogService.SetLoggingEnabled(enabled);
    }

    private static void UpdateLogViewers(string message, ModStack.Logging.LogLevel level)
    {
        // Convert to GUI LogLevel
        var guiLevel = ConvertToGUILogLevel(level);

        // Update service logs view if open
        if (_serviceLogsView != null)
        {
            _serviceLogsView.Dispatcher.Invoke(() =>
            {
                _serviceLogsView.AddLog(message, guiLevel);
            });
        }
    }

    private static ModStack.GUI.Views.LogLevel ConvertToGUILogLevel(ModStack.Logging.LogLevel level)
    {
        return level switch
        {
            ModStack.Logging.LogLevel.Info => ModStack.GUI.Views.LogLevel.Info,
            ModStack.Logging.LogLevel.Warning => ModStack.GUI.Views.LogLevel.Warning,
            ModStack.Logging.LogLevel.Error => ModStack.GUI.Views.LogLevel.Error,
            _ => ModStack.GUI.Views.LogLevel.Info
        };
    }

    public static void RegisterServiceLogsView(ServiceLogsView view)
    {
        _serviceLogsView = view;
    }

    public static void UnregisterServiceLogsView()
    {
        _serviceLogsView = null;
    }

    public static string GetLogFilePath()
    {
        return ModStack.Logging.LogService.GetLogFilePath();
    }

    public static void ClearLogs()
    {
        ModStack.Logging.LogService.ClearLogs();
    }
}
