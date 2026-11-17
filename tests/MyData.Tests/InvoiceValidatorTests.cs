using MyData.Client;
using MyData.Client.Model;

namespace MyData.Tests;

public class InvoiceValidatorTests
{
    [Theory]
    [InlineData("123456783", true)]
    [InlineData("090000045", true)]
    [InlineData("094019245", true)]
    [InlineData("123456789", false)] // wrong check digit
    [InlineData("12345678", false)]  // too short
    [InlineData("12345678A", false)]
    [InlineData("000000000", false)]
    [InlineData(null, false)]
    public void Greek_vat_number_check_digit(string? vatNumber, bool expected)
    {
        Assert.Equal(expected, InvoiceValidator.IsValidGreekVatNumber(vatNumber));
    }

    [Fact]
    public void A_correct_invoice_has_no_errors()
    {
        Assert.Empty(InvoiceValidator.Validate(Samples.Invoice(), Samples.Today));
    }

    [Fact]
    public void Foreign_counterparts_skip_the_greek_check_digit()
    {
        var invoice = Samples.Invoice() with { Counterpart = new Party("DE123456789", "DE") };

        Assert.Empty(InvoiceValidator.Validate(invoice, Samples.Today));
    }

    [Fact]
    public void Reports_every_problem_at_once()
    {
        var invoice = Samples.Invoice(
            new InvoiceLine(-5m, VatCategory.Vat24, Samples.Goods),
            new InvoiceLine(10.123m, VatCategory.Vat24, Samples.Goods),
            new InvoiceLine(10m, VatCategory.Vat0, Samples.Goods)) with
        {
            Counterpart = new Party("123456789"),
            Series = "",
            IssueDate = Samples.Today.AddDays(1)
        };

        var errors = InvoiceValidator.Validate(invoice, Samples.Today);

        Assert.Contains(errors, e => e.Contains("not a valid Greek VAT number"));
        Assert.Contains(errors, e => e.Contains("Series is required"));
        Assert.Contains(errors, e => e.Contains("future"));
        Assert.Contains(errors, e => e.StartsWith("Line 1") && e.Contains("positive"));
        Assert.Contains(errors, e => e.StartsWith("Line 2") && e.Contains("2 decimals"));
        Assert.Contains(errors, e => e.StartsWith("Line 3") && e.Contains("exemption"));
    }

    [Fact]
    public void Issuer_and_counterpart_must_differ()
    {
        var invoice = Samples.Invoice() with { Counterpart = Samples.Issuer };

        Assert.Contains(InvoiceValidator.Validate(invoice, Samples.Today), e => e.Contains("different"));
    }

    [Fact]
    public void Invoice_needs_lines()
    {
        var invoice = Samples.Invoice() with { Lines = [] };

        Assert.Contains(InvoiceValidator.Validate(invoice, Samples.Today), e => e.Contains("at least one line"));
    }
}
