namespace ShipExtract.Domain.Interfaces;

/// <summary>Provides structured logging operations for use throughout the application layers.</summary>
public interface ILoggingService
{
    /// <summary>Logs an informational message.</summary>
    /// <param name="message">Message template (supports structured logging placeholders).</param>
    /// <param name="args">Values for the message template placeholders.</param>
    void LogInformation(string message, params object[] args);

    /// <summary>Logs a warning message indicating a non-fatal issue.</summary>
    /// <param name="message">Message template.</param>
    /// <param name="args">Values for the message template placeholders.</param>
    void LogWarning(string message, params object[] args);

    /// <summary>Logs an error message, optionally including exception details.</summary>
    /// <param name="message">Message template.</param>
    /// <param name="ex">The exception associated with the error, if any.</param>
    /// <param name="args">Values for the message template placeholders.</param>
    void LogError(string message, Exception? ex = null, params object[] args);

    /// <summary>Logs a debug-level message for diagnostic purposes.</summary>
    /// <param name="message">Message template.</param>
    /// <param name="args">Values for the message template placeholders.</param>
    void LogDebug(string message, params object[] args);
}
