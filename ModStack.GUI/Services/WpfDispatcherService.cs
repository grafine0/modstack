using System;
using System.Threading.Tasks;
using System.Windows;
using ModStack.GUI.Services.Interfaces;

namespace ModStack.GUI.Services
{
    /// <summary>
    /// WPF implementation of IDispatcherService using Application.Current.Dispatcher
    /// </summary>
    public class WpfDispatcherService : IDispatcherService
    {
        /// <summary>
        /// Checks if the current thread is the UI thread
        /// </summary>
        public bool IsUIThread => Application.Current?.Dispatcher?.CheckAccess() ?? false;

        /// <summary>
        /// Executes an action on the UI thread
        /// </summary>
        public async Task InvokeOnUIThreadAsync(Action action)
        {
            if (Application.Current?.Dispatcher == null)
            {
                // Fallback: execute directly if no dispatcher available
                action();
                return;
            }

            if (IsUIThread)
            {
                // Already on UI thread, execute directly
                action();
            }
            else
            {
                // Marshal to UI thread
                await Application.Current.Dispatcher.InvokeAsync(action);
            }
        }

        /// <summary>
        /// Executes a function on the UI thread and returns the result
        /// </summary>
        public async Task<T> InvokeOnUIThreadAsync<T>(Func<T> function)
        {
            if (Application.Current?.Dispatcher == null)
            {
                // Fallback: execute directly if no dispatcher available
                return function();
            }

            if (IsUIThread)
            {
                // Already on UI thread, execute directly
                return function();
            }
            else
            {
                // Marshal to UI thread
                return await Application.Current.Dispatcher.InvokeAsync(function);
            }
        }
    }
}
