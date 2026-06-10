using ShipExtract.Domain.Enums;

namespace ShipExtract.Domain.Interfaces;

/// <summary>Detects the logistics carrier from raw document text.</summary>
public interface ICarrierDetector
{
    /// <summary>
    /// Analyses <paramref name="text"/> and returns the detected <see cref="CarrierType"/>.
    /// Returns <see cref="CarrierType.Unknown"/> when no carrier can be identified.
    /// </summary>
    CarrierType Detect(string text);
}
