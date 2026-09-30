using System.Globalization;

namespace OpenMenu.Application.Shared;

/// <summary>
/// Formats prices the way guests read them: "$9.90" for USD, "9,900 ریال"
/// for IRR (whole numbers — cent fragments like ".00" are not common for
/// Rial), "990 تومان" for TOMAN. Other currencies keep two decimals with
/// their symbol/ISO code.
/// </summary>
public static class PriceFormatter
{
    /// <summary>Label shown after the amount; null means "symbol before the amount".</summary>
    private static readonly Dictionary<string, string> SuffixLabels = new()
    {
        ["IRR"] = "ریال",
        ["TOMAN"] = "تومان",
        ["TRY"] = "TL",
        ["AED"] = "د.إ",
    };

    private static readonly HashSet<string> LeadingSymbols =
    [
        "USD", // $
        "EUR", // €
        "GBP", // £
    ];

    public static string Format(decimal price, string? currency, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var code = currency?.Trim().ToUpperInvariant() ?? string.Empty;

        // Whole-number currencies (Rial/Toman-type): never show cent fragments.
        var isWholeCurrency = code is "IRR" or "TOMAN" or "AFN" or "IQD" or "LYD" or "JOD" or "BHD" or "KWD" or "OMR" or "TND";
        if (isWholeCurrency)
        {
            return $"{price.ToString("N0", culture)} {SuffixLabels.GetValueOrDefault(code, code)}";
        }

        var amount = price.ToString("N2", culture);
        return LeadingSymbols.Contains(code)
            ? $"{SymbolOf(code)}{amount}"
            : $"{amount} {code}";
    }

    private static string SymbolOf(string code) => code switch
    {
        "USD" => "$",
        "EUR" => "€",
        "GBP" => "£",
        _ => code,
    };
}
