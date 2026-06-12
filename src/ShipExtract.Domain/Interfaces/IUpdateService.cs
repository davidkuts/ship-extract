using ShipExtract.Domain.Models;

namespace ShipExtract.Domain.Interfaces;

/// <summary>Checks for application updates.</summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks GitHub Releases for a newer version of the application.
    /// This method must never throw — network failures return null.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="UpdateInfo"/> when the check succeeds, or <see langword="null"/> on failure.</returns>
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default);
}
