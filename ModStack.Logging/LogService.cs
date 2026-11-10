using System;
using System.IO;

namespace ModStack.Logging;

public static class LogService
{
    private static readonly string LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModStack");
    private static readonly string LogFilePath = Path.Combine(LogDirectory, "application.log");
    private static readonly object LogLock = new object();
    private static bool _loggingEnabled = true;

    static LogService()
    {
        // Ensure log directory exists
        if (!Directory.Exists(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }
    }

    public static void Info(string message)
    {
        WriteLog(LogLevel.Info, message);
    }

    public static void Warning(string message)
    {
        WriteLog(LogLevel.Warning, message);
    }

    public static void Error(string message)
    {
        WriteLog(LogLevel.Error, message);
    }

    public static void Error(string message, Exception ex)
    {
        WriteLog(LogLevel.Error, $"{message}: {ex.Message}");
    }

    public static void SetLoggingEnabled(bool enabled)
    {
        _loggingEnabled = enabled;
    }

    private static void WriteLog(LogLevel level, string message)
    {
        // Check if logging is enabled
        if (!_loggingEnabled)
        {
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logEntry = $"[{timestamp}] [{level}] {message}";

        // Write to file
        lock (LogLock)
        {
            try
            {
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // If file writing fails, continue silently
            }
        }

        // Write to debug output
        System.Diagnostics.Debug.WriteLine(logEntry);
    }

    public static string GetLogFilePath()
    {
        return LogFilePath;
    }

    public static void ClearLogs()
    {
        lock (LogLock)
        {
            try
            {
                if (File.Exists(LogFilePath))
                {
                    File.WriteAllText(LogFilePath, string.Empty);
                }
            }
            catch
            {
                // If file clearing fails, continue silently
            }
        }
    }
}

public enum LogLevel
{
    Info,
    Warning,
    Error
}
