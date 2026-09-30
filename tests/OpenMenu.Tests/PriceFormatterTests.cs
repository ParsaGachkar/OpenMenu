using OpenMenu.Application.Shared;

namespace OpenMenu.Tests;

/// <summary>
/// Guest-facing price formatting: symbols instead of ISO codes, whole numbers
/// for Rial-type currencies (no ".00" cents), Toman as its own unit.
/// </summary>
public class PriceFormatterTests
{
    [Theory]
    [InlineData(9.90, "USD", "$9.90")]
    [InlineData(1234.5, "EUR", "€1,234.50")]
    [InlineData(5, "GBP", "£5.00")]
    public void LeadingSymbol_Currencies_ShowSymbolBeforeAmount(decimal price, string currency, string expected)
    {
        var result = PriceFormatter.Format(price, currency, culture: Culture("en-US"));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IRR_ShowsWholeNumbers_WithRialSuffix()
    {
        var result = PriceFormatter.Format(99000m, "IRR", culture: Culture("en-US"));
        Assert.Equal("99,000 ریال", result);
    }

    [Fact]
    public void IRR_NeverShowsCentFragments()
    {
        var result = PriceFormatter.Format(99000.00m, "IRR", culture: Culture("en-US"));
        Assert.DoesNotContain(".00", result);
    }

    [Fact]
    public void TOMAN_ShowsWholeNumbers_WithTomanLabel()
    {
        var result = PriceFormatter.Format(990m, "TOMAN", culture: Culture("en-US"));
        Assert.Equal("990 تومان", result);
    }

    [Fact]
    public void OtherCurrencies_KeepIsoCode_Suffix()
    {
        var result = PriceFormatter.Format(10m, "CHF", culture: Culture("en-US"));
        Assert.Equal("10.00 CHF", result);
    }

    private static System.Globalization.CultureInfo Culture(string name) =>
        new(name, useUserOverride: false);
}
