namespace OpenMenu.Application.Shared;

/// <summary>
/// The currency codes the settings form offers. Kept small on purpose for a
/// single-restaurant app; TOMAN is not ISO 4217 but Iranian restaurants quote
/// prices in Toman, so it is offered like any other option.
/// </summary>
public static class CurrencyOptions
{
    public static readonly (string Code, string Name)[] All =
    [
        ("USD", "US Dollar"), ("EUR", "Euro"), ("GBP", "British Pound"),
        ("IRR", "Iranian Rial"), ("TOMAN", "Iranian Toman"), ("TRY", "Turkish Lira"), ("AED", "UAE Dirham"),
        ("SAR", "Saudi Riyal"), ("IQD", "Iraqi Dinar"),
    ];
}
