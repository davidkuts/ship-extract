using Serilog;
using Serilog.Events;

namespace ShipExtract.Infrastructure.Logging;

/// <summary>Factory for creating the application's configured Serilog logger instance.</summary>
public static class ShipExtractLoggerFactory
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Creates and returns a fully configured Serilog <see cref="ILogger"/> that writes to
    /// a daily-rolling log file and to the console.
    /// </summary>
    /// <param name="logDirectory">Directory where log files will be written.</param>
    /// <returns>A configured <see cref="ILogger"/> instance.</returns>
    public static Serilog.ILogger CreateLogger(string logDirectory)
    {
        var logPath = Path.Combine(logDirectory, "shipextract-.log");

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: OutputTemplate)
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: OutputTemplate)
            .CreateLogger();
    }
}
