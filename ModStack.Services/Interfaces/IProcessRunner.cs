using System.Diagnostics;

namespace ModStack.Services.Interfaces;

/// <summary>
/// Result of a process execution
/// </summary>
public record ProcessExecutionResult(
    int ExitCode,
    string Output,
    string Error,
    bool TimedOut = false,
    bool WasCancelled = false
);

/// <summary>
/// Abstraction for running external processes (for testability and dependency injection)
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Executes a process with the given parameters
    /// </summary>
    /// <param name="fileName">The executable file name</param>
    /// <param name="arguments">Command line arguments</param>
    /// <param name="workingDirectory">Working directory for the process</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default: 30 seconds)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Process execution result</returns>
    Task<ProcessExecutionResult> ExecuteAsync(
        string fileName,
        string arguments = "",
        string? workingDirectory = null,
        int timeoutMs = 30000,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of IProcessRunner using System.Diagnostics.Process
/// </summary>
public class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessExecutionResult> ExecuteAsync(
        string fileName,
        string arguments = "",
        string? workingDirectory = null,
        int timeoutMs = 30000,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };

        using var process = new Process { StartInfo = startInfo };
        
        try
        {
            process.Start();
            
            // Read output and error streams asynchronously
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            // Wait for process with timeout and cancellation
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            try
            {
                await process.WaitForExitAsync(combinedCts.Token);
                
                var output = await outputTask;
                var error = await errorTask;
                
                return new ProcessExecutionResult(
                    ExitCode: process.ExitCode,
                    Output: output,
                    Error: error
                );
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Ignore errors when killing process
                }
                
                return new ProcessExecutionResult(
                    ExitCode: -1,
                    Output: "",
                    Error: "Process timed out",
                    TimedOut: true
                );
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation requested
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Ignore errors when killing process
                }
                
                return new ProcessExecutionResult(
                    ExitCode: -1,
                    Output: "",
                    Error: "Process was cancelled",
                    WasCancelled: true
                );
            }
        }
        catch (Exception ex)
        {
            return new ProcessExecutionResult(
                ExitCode: -1,
                Output: "",
                Error: ex.Message
            );
        }
    }
}
