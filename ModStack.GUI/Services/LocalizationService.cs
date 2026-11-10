using System.Globalization;
using System.Resources;
using System.Reflection;
using ModStack.GUI.Services.Interfaces;

namespace ModStack.GUI.Services;

/// <summary>
/// Service for handling application localization and internationalization
/// </summary>
public class LocalizationService : ILocalizationService
{
    private ResourceManager? _resourceManager;
    private CultureInfo _currentCulture;
    private const string DefaultCulture = "en-US";

    public LocalizationService()
    {
        // Initialize with default culture
        _currentCulture = new CultureInfo(DefaultCulture);
        InitializeResourceManager();
    }

    /// <summary>
    /// Gets the current culture/language code
    /// </summary>
    public string CurrentCulture => _currentCulture.Name;

    /// <summary>
    /// Event raised when culture changes
    /// </summary>
    public event EventHandler? CultureChanged;

    /// <summary>
    /// Gets available cultures supported by the application
    /// </summary>
    public IEnumerable<string> AvailableCultures
    {
        get
        {
            yield return "en-US";
            yield return "fr-FR";
            yield return "es-ES";
            yield return "ar-SA";
        }
    }

    /// <summary>
    /// Gets a localized string by key
    /// </summary>
    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        try
        {
            var value = _resourceManager?.GetString(key, _currentCulture);
            return value ?? key;
        }
        catch
        {
            return key;
        }
    }

    /// <summary>
    /// Gets a localized string by key with format parameters
    /// </summary>
    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(_currentCulture, format, args);
        }
        catch
        {
            return format;
        }
    }

    /// <summary>
    /// Sets the culture/language
    /// </summary>
    public void SetCulture(string cultureCode)
    {
        try
        {
            var newCulture = new CultureInfo(cultureCode);
            if (_currentCulture.Name != newCulture.Name)
            {
                _currentCulture = newCulture;
                CultureInfo.CurrentCulture = newCulture;
                CultureInfo.CurrentUICulture = newCulture;
                
                // Notify subscribers of culture change
                CultureChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (CultureNotFoundException)
        {
            // If culture not found, fall back to default
            if (_currentCulture.Name != DefaultCulture)
            {
                SetCulture(DefaultCulture);
            }
        }
    }

    private void InitializeResourceManager()
    {
        try
        {
            // Get the ResourceManager for Strings resource file
            // The resource name should match the .resx file name without extension
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "ModStack.GUI.Resources.Strings";
            _resourceManager = new ResourceManager(resourceName, assembly);
            
            // Test that we can access resources
            var testValue = _resourceManager.GetString("AppName", _currentCulture);
            if (testValue == null)
            {
                LogService.Warning("Resource file loaded but test key 'AppName' not found");
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to initialize resource manager: {ex.Message}");
            _resourceManager = null;
        }
    }
}

