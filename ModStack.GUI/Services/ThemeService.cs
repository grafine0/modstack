using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ModStack.GUI.Models;
using ModStack.GUI.Services.Interfaces;

namespace ModStack.GUI.Services
{
    /// <summary>
    /// Service for managing application themes using ModernWpf
    /// </summary>
    public class ThemeService : IThemeService
    {
        private ThemeOption _currentTheme = ThemeOption.Light;
        private bool _isListeningToSystemTheme = false;

        public ThemeOption CurrentTheme => _currentTheme;

        public event EventHandler<ThemeOption> ThemeChanged;

        public void ApplyTheme(ThemeOption theme)
        {
            try
            {
                var previousTheme = _currentTheme;
                _currentTheme = theme;

                // Handle System theme option
                if (theme == ThemeOption.System)
                {
                    var systemTheme = GetSystemTheme();
                    ApplyActualTheme(systemTheme);
                    StartListeningToSystemThemeChanges();
                    LogService.Info($"System theme mode enabled, current system theme is {systemTheme}");
                }
                else
                {
                    StopListeningToSystemThemeChanges();
                    ApplyActualTheme(theme);
                }

                // Notify subscribers if theme actually changed
                if (previousTheme != _currentTheme)
                {
                    ThemeChanged?.Invoke(this, _currentTheme);
                    LogService.Info($"Theme changed from {previousTheme} to {_currentTheme}");
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"Failed to apply theme '{theme}': {ex}");
                throw;
            }
        }

        private void ApplyActualTheme(ThemeOption theme)
        {
            // Apply ModernWpf theme
            if (theme == ThemeOption.Dark || theme == ThemeOption.System)
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Dark;
                App.ApplyDarkTheme();
                LogService.Info("Dark theme applied via ThemeService");
            }
            else
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Light;
                App.ApplyLightTheme();
                LogService.Info("Light theme applied via ThemeService");
            }
        }

        private ThemeOption GetSystemTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key?.GetValue("AppsUseLightTheme");
                    if (value is int intValue)
                    {
                        return intValue == 0 ? ThemeOption.Dark : ThemeOption.Light;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Warning($"Failed to get system theme, defaulting to Light: {ex.Message}");
            }

            return ThemeOption.Light;
        }

        private void StartListeningToSystemThemeChanges()
        {
            if (_isListeningToSystemTheme) return;

            try
            {
                SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;
                _isListeningToSystemTheme = true;
                LogService.Info("Started listening to system theme changes");
            }
            catch (Exception ex)
            {
                LogService.Error($"Failed to start listening to system theme changes: {ex.Message}");
            }
        }

        private void StopListeningToSystemThemeChanges()
        {
            if (!_isListeningToSystemTheme) return;

            try
            {
                SystemEvents.UserPreferenceChanged -= OnSystemThemeChanged;
                _isListeningToSystemTheme = false;
                LogService.Info("Stopped listening to system theme changes");
            }
            catch (Exception ex)
            {
                LogService.Error($"Failed to stop listening to system theme changes: {ex.Message}");
            }
        }

        private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General && _currentTheme == ThemeOption.System)
            {
                var systemTheme = GetSystemTheme();
                LogService.Info($"System theme changed, applying {systemTheme}");
                ApplyActualTheme(systemTheme);
            }
        }
    }
}
