using Microsoft.Extensions.DependencyInjection;

namespace ShipExtract.UI.DependencyInjection;

/// <summary>Provides static access to the application's DI service provider.</summary>
public static class ServiceLocator
{
    private static IServiceProvider? _services;

    /// <summary>Gets the configured service provider.</summary>
    public static IServiceProvider Services =>
        _services ?? throw new InvalidOperationException("ServiceLocator has not been initialised.");

    /// <summary>Initialises the service locator with the application's DI container.</summary>
    public static void Initialize(IServiceProvider provider) => _services = provider;

    /// <summary>Resolves a required service of type <typeparamref name="T"/>.</summary>
    public static T Get<T>() where T : notnull => Services.GetRequiredService<T>();
}
