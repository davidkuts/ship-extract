using ShipExtract.Domain.Enums;

namespace ShipExtract.Domain.Exceptions;

/// <summary>
/// Domain exception that carries a typed <see cref="ExtractionErrorCode"/> alongside the message,
/// allowing callers to map failures to user-facing messages without string-parsing.
/// </summary>
public class ShipExtractException : Exception
{
    /// <summary>Gets the structured error category for this exception.</summary>
    public ExtractionErrorCode ErrorCode { get; }

    /// <summary>Initialises a new instance with a code and message.</summary>
    public ShipExtractException(ExtractionErrorCode code, string message)
        : base(message)
    {
        ErrorCode = code;
    }

    /// <summary>Initialises a new instance with a code, message and inner exception.</summary>
    public ShipExtractException(ExtractionErrorCode code, string message, Exception inner)
        : base(message, inner)
    {
        ErrorCode = code;
    }
}
