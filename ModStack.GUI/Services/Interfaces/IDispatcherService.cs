using System;
using System.Threading.Tasks;

namespace ModStack.GUI.Services.Interfaces
{
    /// <summary>
    /// Abstracts UI thread dispatching for better testability and platform independence
    /// </summary>
    public interface IDispatcherService
    {
        /// <summary>
        /// Executes an action on the UI thread
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <returns>A task that represents the completion of the action</returns>
        Task InvokeOnUIThreadAsync(Action action);

        /// <summary>
        /// Executes a function on the UI thread and returns the result
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="function">The function to execute</param>
        /// <returns>A task that represents the completion of the function with its result</returns>
        Task<T> InvokeOnUIThreadAsync<T>(Func<T> function);

        /// <summary>
        /// Checks if the current thread is the UI thread
        /// </summary>
        /// <returns>True if current thread is the UI thread, false otherwise</returns>
        bool IsUIThread { get; }
    }
}
