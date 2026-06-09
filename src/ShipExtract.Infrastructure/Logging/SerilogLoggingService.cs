using ShipExtract.Domain.Interfaces;
using Serilog;

namespace ShipExtract.Infrastructure.Logging;

/// <summary>
/// Implements <see cref="ILoggingService"/> by delegating to a Serilog <see cref="ILogger"/> instance.
/// </summary>
public sealed class SerilogLoggingService : ILoggingService
{
    private readonly Serilog.ILogger _logger;

    /// <summary>Initialises a new instance with the provided Serilog logger.</summary>
    /// <param name="logger">The configured Serilog logger to write to.</param>
    public SerilogLoggingService(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void LogInformation(string message, params object[] args) =>
        _logger.Information(message, args);

    /// <inheritdoc/>
    public void LogWarning(string message, params object[] args) =>
        _logger.Warning(message, args);

    /// <inheritdoc/>
    public void LogError(string message, Exception? ex = null, params object[] args)
    {
        if (ex is not null)
            _logger.Error(ex, message, args);
        else
            _logger.Error(message, args);
    }

    /// <inheritdoc/>
    public void LogDebug(string message, params object[] args) =>
        _logger.Debug(message, args);
}
