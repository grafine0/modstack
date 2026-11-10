namespace ModStack.GUI.Services.Interfaces;

/// <summary>
/// Interface for localization service to handle internationalization (i18n)
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Gets a localized string by key
    /// </summary>
    /// <param name="key">The resource key</param>
    /// <returns>The localized string, or the key if not found</returns>
    string GetString(string key);

    /// <summary>
    /// Gets a localized string by key with format parameters
    /// </summary>
    /// <param name="key">The resource key</param>
    /// <param name="args">Format arguments</param>
    /// <returns>The localized and formatted string, or the key if not found</returns>
    string GetString(string key, params object[] args);

    /// <summary>
    /// Gets the current culture/language code
    /// </summary>
    string CurrentCulture { get; }

    /// <summary>
    /// Sets the culture/language
    /// </summary>
    /// <param name="cultureCode">Culture code (e.g., "en-US", "es-ES", "fr-FR")</param>
    void SetCulture(string cultureCode);

    /// <summary>
    /// Event raised when culture changes
    /// </summary>
    event EventHandler? CultureChanged;

    /// <summary>
    /// Gets available cultures
    /// </summary>
    IEnumerable<string> AvailableCultures { get; }
}

