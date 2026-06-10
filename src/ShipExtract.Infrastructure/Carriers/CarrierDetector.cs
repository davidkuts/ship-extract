using System.Text.RegularExpressions;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;

namespace ShipExtract.Infrastructure.Carriers;

/// <summary>
/// Detects the logistics carrier from raw document text using compiled regex patterns.
/// Stateless and Singleton-safe.
/// </summary>
public sealed class CarrierDetector : ICarrierDetector
{
    // ── Detection rules, ordered by priority ────────────────────────────────
    // More-specific signatures listed before generic catch-alls within each carrier.

    private static readonly (CarrierType Carrier, Regex Pattern)[] Rules =
    [
        // DHL — specific DHL-branded markers only; bare "waybill" is too generic
        // (FedEx and others also use "Air Waybill" terminology)
        (CarrierType.DHL, Build(
            "dhl express", "dhl global", "dhl freight", @"dhl\.com", "dhl waybill", "dhl")),

        // FedEx — "awb no." is FedEx-specific; check before others
        (CarrierType.FedEx, Build(
            "fedex", "federal express", @"fed\s+ex", @"fedex\.com",
            @"air waybill no\.", @"awb no\.")),

        // UPS — "1z tracking" and "ups supply chain" before bare "ups"
        (CarrierType.UPS, Build(
            "ups supply chain", "ups capital", "united parcel",
            @"ups\.com", @"1z tracking", "ups freight", "ups")),

        // TNT
        (CarrierType.TNT, Build(
            "tnt express", @"tnt\.com", "tnt")),

        // DPD
        (CarrierType.DPD, Build(
            "dpd group", @"dpd\.com", "dpd")),

        // GLS
        (CarrierType.GLS, Build(
            "gls group", @"gls\-group\.eu", "gls parcel")),

        // Schenker / DB Schenker
        (CarrierType.Schenker, Build(
            "db schenker", "schenker ag", "schenker")),

        // Kuehne+Nagel
        (CarrierType.Kuehne, Build(
            @"kuehne\+nagel", "kuehne nagel", "kn login", "knl", "kuehne")),

        // Panalpina (now part of DSC Logistics / CMA CGM)
        (CarrierType.Panalpina, Build(
            "panalpina", "dsc logistics")),
    ];

    /// <inheritdoc/>
    public CarrierType Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return CarrierType.Unknown;

        foreach (var (carrier, pattern) in Rules)
        {
            if (pattern.IsMatch(text))
                return carrier;
        }

        return CarrierType.Unknown;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a single compiled regex that matches ANY of the supplied terms
    /// (longest first, case-insensitive).
    /// </summary>
    private static Regex Build(params string[] terms)
    {
        var pattern = string.Join("|",
            terms
                .OrderByDescending(t => t.Length)
                .Select(t => t.Contains('\\') || t.Contains('.') || t.Contains('+')
                    ? t            // already a regex fragment — use as-is
                    : Regex.Escape(t)));

        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
