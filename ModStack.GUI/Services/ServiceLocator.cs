using Microsoft.Extensions.DependencyInjection;

namespace ModStack.GUI.Services;

/// <summary>
/// Simple service locator for dependency injection in WPF
/// </summary>
public static class ServiceLocator
{
    private static IServiceProvider? _serviceProvider;

    public static void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public static IServiceProvider ServiceProvider 
    { 
        get => _serviceProvider ?? throw new InvalidOperationException("Service provider has not been initialized"); 
    }

    public static T GetService<T>() where T : notnull
    {
        if (_serviceProvider == null)
            throw new InvalidOperationException("Service provider has not been initialized");

        return _serviceProvider.GetRequiredService<T>();
    }

    public static T? GetServiceOptional<T>() where T : class
    {
        if (_serviceProvider == null)
            return null;

        return _serviceProvider.GetService<T>();
    }
}
