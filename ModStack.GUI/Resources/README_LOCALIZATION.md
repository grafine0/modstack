# Internationalization (i18n) Guide

This guide explains how to use the internationalization system in ModStack.

## Overview

ModStack now supports internationalization using .NET resource files (.resx). All user-facing strings can be localized to support multiple languages.

## Architecture

### Components

1. **ILocalizationService**: Interface for localization operations
2. **LocalizationService**: Implementation that loads and manages resource strings
3. **LocalizeExtension**: XAML markup extension for easy string binding
4. **Strings.resx**: Default English resource file

## Adding Localized Strings

### In XAML Files

Use the `loc:Localize` markup extension:

```xaml
xmlns:loc="clr-namespace:ModStack.GUI.Converters"

<TextBlock Text="{loc:Localize AppName}"/>
<Button Content="{loc:Localize ButtonSave}"/>
```

You can also use positional syntax:

```xaml
<TextBlock Text="{loc:Localize AppName}"/>
```

### In Code-Behind (C#)

Access the localization service via ServiceLocator:

```csharp
using ModStack.GUI.Services.Interfaces;
using ModStack.GUI.Services;

var localizationService = ServiceLocator.GetService<ILocalizationService>();
string localizedText = localizationService.GetString("AppName");
```

### With Format Parameters

For strings with placeholders:

```csharp
// Resource string: "Loaded {0} log entries"
string message = localizationService.GetString("LoadedLogEntries", count);
```

## Adding New Resource Strings

1. Open `ModStack.GUI/Resources/Strings.resx`
2. Add a new `<data>` entry:

```xml
<data name="MyNewKey" xml:space="preserve">
  <value>My localized text</value>
</data>
```

3. Use the key in XAML or code:
   - XAML: `{loc:Localize MyNewKey}`
   - Code: `localizationService.GetString("MyNewKey")`

## Naming Conventions

Use clear, descriptive key names:
- `NavDashboard` - Navigation items
- `ButtonSave` - Button labels
- `SectionServices` - Section headers
- `ErrorGeneric` - Error messages
- `SuccessSaved` - Success messages

## Supported Languages

The application currently supports the following languages:

- **English (en-US)** - Default
- **French (fr-FR)** - Français
- **Spanish (es-ES)** - Español  
- **Arabic (ar-SA)** - العربية

## Adding New Languages

To add support for a new language (e.g., German):

1. Create a new resource file: `Strings.de-DE.resx`
2. Copy all keys from `Strings.resx`
3. Translate all values to German
4. Update `AvailableCultures` in `LocalizationService.cs`:

```csharp
yield return "de-DE";
```

5. Add the resource file to `ModStack.GUI.csproj`:

```xml
<EmbeddedResource Include="Resources\Strings.de-DE.resx">
  <Generator>ResXFileCodeGenerator</Generator>
</EmbeddedResource>
```

6. Use `SetCulture("de-DE")` to switch languages

## Changing Language at Runtime

```csharp
var localizationService = ServiceLocator.GetService<ILocalizationService>();
localizationService.SetCulture("es-ES"); // Switch to Spanish
```

The `CultureChanged` event will fire, allowing views to refresh their localized content.

## Resource File Location

- Default (English): `ModStack.GUI/Resources/Strings.resx`
- Language-specific: `ModStack.GUI/Resources/Strings.{culture}.resx`

Current resource files:
- `Strings.resx` (English - default)
- `Strings.fr-FR.resx` (French)
- `Strings.es-ES.resx` (Spanish)
- `Strings.ar-SA.resx` (Arabic)

## Best Practices

1. **Always use resource keys** - Never hardcode strings in XAML or code
2. **Use descriptive key names** - Make keys self-documenting
3. **Group related keys** - Use prefixes like `Nav`, `Button`, `Section`
4. **Keep translations consistent** - Use the same terminology throughout
5. **Test with different cultures** - Verify text fits in UI elements
6. **Consider text expansion** - Some languages are longer than English

## Example: Converting a View to Use Localization

### Before:
```xaml
<TextBlock Text="Service Logs"/>
<Button Content="Refresh"/>
```

### After:
```xaml
xmlns:loc="clr-namespace:ModStack.GUI.Converters"

<TextBlock Text="{loc:Localize ServiceLogsTitle}"/>
<Button Content="{loc:Localize ButtonRefresh}"/>
```

And add to `Strings.resx`:
```xml
<data name="ServiceLogsTitle" xml:space="preserve">
  <value>Service Logs</value>
</data>
<data name="ButtonRefresh" xml:space="preserve">
  <value>Refresh</value>
</data>
```

## Troubleshooting

### Resource not found
- Ensure the resource key exists in `Strings.resx`
- Check for typos in the key name
- Verify the resource file is set as `EmbeddedResource` in the project

### Language not switching
- Verify the culture code is correct (e.g., "en-US", "es-ES")
- Check that the culture is available in `AvailableCultures`
- Ensure the resource file for that culture exists

### Strings not updating
- Views need to subscribe to `CultureChanged` event to refresh
- Consider implementing `INotifyPropertyChanged` in ViewModels
- Some XAML bindings may need to be refreshed manually

