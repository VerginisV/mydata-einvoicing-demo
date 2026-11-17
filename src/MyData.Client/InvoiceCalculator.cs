using MyData.Client.Model;

namespace MyData.Client;

public sealed record LineTotals(int LineNumber, InvoiceLine Line, decimal VatAmount);

public sealed record InvoiceTotals(IReadOnlyList<LineTotals> Lines, decimal TotalNet, decimal TotalVat)
{
    public decimal TotalGross => TotalNet + TotalVat;

    /// <summary>Net amount per income classification, as myDATA expects in the invoice summary.</summary>
    public IReadOnlyList<(IncomeClassification Classification, decimal Amount)> ClassificationTotals =>
        [.. Lines.GroupBy(l => l.Line.Classification).Select(g => (g.Key, g.Sum(l => Round(l.Line.NetValue))))];

    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Calculates VAT per line and the invoice totals. VAT is rounded per line to 2 decimals
/// (half away from zero), and totals are the sum of the rounded lines, so the summary always
/// matches the lines exactly. A mismatch here is one of the most common myDATA rejections.
/// </summary>
public static class InvoiceCalculator
{
    public static InvoiceTotals Calculate(Invoice invoice)
    {
        var lines = invoice.Lines
            .Select((line, i) => new LineTotals(
                i + 1,
                line,
                InvoiceTotals.Round(InvoiceTotals.Round(line.NetValue) * VatRates.For(line.VatCategory))))
            .ToList();

        return new InvoiceTotals(
            lines,
            lines.Sum(l => InvoiceTotals.Round(l.Line.NetValue)),
            lines.Sum(l => l.VatAmount));
    }
}
