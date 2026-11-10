using System;
using System.Windows.Markup;
using ModStack.GUI.Services.Interfaces;
using ModStack.GUI.Services;

namespace ModStack.GUI.Converters;

/// <summary>
/// XAML Markup Extension for easy resource string binding
/// Usage: {loc:Localize Key=WindowTitle} or {loc:Localize WindowTitle}
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public class LocalizeExtension : MarkupExtension
{
    private static ILocalizationService? _localizationService;

    /// <summary>
    /// The resource key to localize
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Constructor for property syntax: {loc:Localize Key=WindowTitle}
    /// </summary>
    public LocalizeExtension()
    {
    }

    /// <summary>
    /// Constructor for positional syntax: {loc:Localize WindowTitle}
    /// </summary>
    public LocalizeExtension(string key)
    {
        Key = key;
    }

    /// <summary>
    /// Provides the localized string value
    /// </summary>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
            return string.Empty;

        // Lazy initialization of localization service
        if (_localizationService == null)
        {
            // Try to get from ServiceLocator first, then create new instance if needed
            try
            {
                _localizationService = ServiceLocator.GetService<ILocalizationService>();
            }
            catch
            {
                // If ServiceLocator is not available yet, create a new instance
                // This can happen during XAML parsing before DI is fully initialized
            }

            if (_localizationService == null)
            {
                _localizationService = new LocalizationService();
            }
        }

        return _localizationService.GetString(Key);
    }

    /// <summary>
    /// Sets the localization service instance (typically called from App initialization)
    /// </summary>
    public static void SetLocalizationService(ILocalizationService service)
    {
        _localizationService = service;
    }
}

