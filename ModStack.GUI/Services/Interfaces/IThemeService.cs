using System;
using ModStack.GUI.Models;

namespace ModStack.GUI.Services.Interfaces
{
    /// <summary>
    /// Service for managing application themes
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Apply the specified theme to the application
        /// </summary>
        /// <param name="theme">Theme option enum</param>
        void ApplyTheme(ThemeOption theme);
        
        /// <summary>
        /// Get the current applied theme
        /// </summary>
        ThemeOption CurrentTheme { get; }
        
        /// <summary>
        /// Event raised when theme changes
        /// </summary>
        event EventHandler<ThemeOption> ThemeChanged;
    }
}
