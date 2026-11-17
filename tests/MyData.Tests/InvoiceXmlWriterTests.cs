using System.Xml.Linq;
using MyData.Client;
using MyData.Client.Model;

namespace MyData.Tests;

public class InvoiceXmlWriterTests
{
    private static readonly XNamespace Inv = InvoiceXmlWriter.Inv;
    private static readonly XNamespace Icls = InvoiceXmlWriter.Icls;

    private static XElement WriteSingle(Invoice invoice) =>
        InvoiceXmlWriter.Write([invoice]).Root!.Element(Inv + "invoice")!;

    [Fact]
    public void Uses_the_official_myDATA_namespaces()
    {
        var root = InvoiceXmlWriter.Write([Samples.Invoice()]).Root!;

        Assert.Equal(Inv + "InvoicesDoc", root.Name);
        Assert.Equal("http://www.aade.gr/myDATA/invoice/v1.0", Inv.NamespaceName);
        Assert.Equal("https://www.aade.gr/myDATA/incomeClassificaton/v1.0", Icls.NamespaceName); // AADE's spelling
    }

    [Fact]
    public void Invoice_sections_follow_the_schema_order()
    {
        var invoice = WriteSingle(Samples.Invoice(
            new InvoiceLine(100m, VatCategory.Vat24, Samples.Goods),
            new InvoiceLine(50m, VatCategory.Vat13, Samples.Goods)));

        var order = invoice.Elements().Select(e => e.Name.LocalName).ToList();

        Assert.Equal(
            new[] { "issuer", "counterpart", "invoiceHeader", "paymentMethods", "invoiceDetails", "invoiceDetails", "invoiceSummary" },
            order);
    }

    [Fact]
    public void Header_contains_series_number_date_and_type()
    {
        var header = WriteSingle(Samples.Invoice()).Element(Inv + "invoiceHeader")!;

        Assert.Equal(new[] { "series", "aa", "issueDate", "invoiceType", "currency" }, header.Elements().Select(e => e.Name.LocalName).ToList());
        Assert.Equal("2026-11-17", header.Element(Inv + "issueDate")!.Value);
        Assert.Equal("1.1", header.Element(Inv + "invoiceType")!.Value);
    }

    [Fact]
    public void Amounts_always_have_two_decimals_and_a_dot()
    {
        var invoice = WriteSingle(Samples.Invoice(new InvoiceLine(100m, VatCategory.Vat24, Samples.Goods)));
        var line = invoice.Element(Inv + "invoiceDetails")!;
        var summary = invoice.Element(Inv + "invoiceSummary")!;

        Assert.Equal("100.00", line.Element(Inv + "netValue")!.Value);
        Assert.Equal("24.00", line.Element(Inv + "vatAmount")!.Value);
        Assert.Equal("124.00", summary.Element(Inv + "totalGrossValue")!.Value);
        Assert.Equal("124.00", invoice.Descendants(Inv + "amount").Single().Value);
    }

    [Fact]
    public void Lines_and_summary_carry_income_classifications()
    {
        var invoice = WriteSingle(Samples.Invoice());
        var lineClassification = invoice.Element(Inv + "invoiceDetails")!.Element(Inv + "incomeClassification")!;
        var summaryClassification = invoice.Element(Inv + "invoiceSummary")!.Element(Inv + "incomeClassification")!;

        Assert.Equal("E3_561_001", lineClassification.Element(Icls + "classificationType")!.Value);
        Assert.Equal("category1_1", lineClassification.Element(Icls + "classificationCategory")!.Value);
        Assert.Equal("100.00", summaryClassification.Element(Icls + "amount")!.Value);
    }

    [Fact]
    public void Vat_exemption_is_written_only_when_present()
    {
        var invoice = WriteSingle(Samples.Invoice(
            new InvoiceLine(100m, VatCategory.Vat24, Samples.Goods),
            new InvoiceLine(100m, VatCategory.Vat0, Samples.Goods, VatExemptionCategory: 14)));

        var lines = invoice.Elements(Inv + "invoiceDetails").ToList();

        Assert.Null(lines[0].Element(Inv + "vatExemptionCategory"));
        Assert.Equal("14", lines[1].Element(Inv + "vatExemptionCategory")!.Value);
    }

    [Fact]
    public void Several_invoices_go_in_one_document()
    {
        var doc = InvoiceXmlWriter.Write([Samples.Invoice(), Samples.Invoice() with { Number = "2" }]);

        Assert.Equal(2, doc.Root!.Elements(Inv + "invoice").Count());
    }
}
