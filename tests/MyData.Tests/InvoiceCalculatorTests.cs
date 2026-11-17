using System.Globalization;
using MyData.Client;
using MyData.Client.Model;

namespace MyData.Tests;

public class InvoiceCalculatorTests
{
    private static decimal D(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("100.00", VatCategory.Vat24, "24.00")]
    [InlineData("49.99", VatCategory.Vat13, "6.50")]   // 6.4987
    [InlineData("10.10", VatCategory.Vat6, "0.61")]    // 0.606
    [InlineData("12.50", VatCategory.Vat24, "3.00")]
    [InlineData("0.10", VatCategory.Vat24, "0.02")]    // 0.024
    [InlineData("100.00", VatCategory.Vat17, "17.00")]
    [InlineData("100.00", VatCategory.Vat0, "0.00")]
    public void Vat_is_rounded_per_line_to_two_decimals(string net, VatCategory category, string expectedVat)
    {
        var line = new InvoiceLine(D(net), category, Samples.Goods, VatExemptionCategory: 1);

        var totals = InvoiceCalculator.Calculate(Samples.Invoice(line));

        Assert.Equal(D(expectedVat), totals.Lines[0].VatAmount);
    }

    [Fact]
    public void Midpoints_round_away_from_zero_not_to_even()
    {
        // 0.75 at 6% is exactly 0.045: away from zero gives 0.05, banker's rounding would give 0.04.
        var totals = InvoiceCalculator.Calculate(Samples.Invoice(new InvoiceLine(0.75m, VatCategory.Vat6, Samples.Goods)));

        Assert.Equal(0.05m, totals.Lines[0].VatAmount);
    }

    [Fact]
    public void Totals_are_the_sum_of_the_rounded_lines()
    {
        var totals = InvoiceCalculator.Calculate(Samples.Invoice(
            new InvoiceLine(0.10m, VatCategory.Vat24, Samples.Goods),
            new InvoiceLine(0.10m, VatCategory.Vat24, Samples.Goods),
            new InvoiceLine(0.10m, VatCategory.Vat24, Samples.Goods)));

        // Each line: 0.024 rounds to 0.02. The total is 0.06, not round(0.072) = 0.07,
        // so the summary always equals the sum of the lines.
        Assert.Equal(0.30m, totals.TotalNet);
        Assert.Equal(0.06m, totals.TotalVat);
        Assert.Equal(0.36m, totals.TotalGross);
    }

    [Fact]
    public void Classification_totals_are_grouped()
    {
        var totals = InvoiceCalculator.Calculate(Samples.Invoice(
            new InvoiceLine(100m, VatCategory.Vat24, Samples.Goods),
            new InvoiceLine(50m, VatCategory.Vat13, Samples.Goods),
            new InvoiceLine(30m, VatCategory.Vat24, Samples.Services)));

        Assert.Equal(2, totals.ClassificationTotals.Count);
        Assert.Contains((Samples.Goods, 150m), totals.ClassificationTotals);
        Assert.Contains((Samples.Services, 30m), totals.ClassificationTotals);
    }
}
