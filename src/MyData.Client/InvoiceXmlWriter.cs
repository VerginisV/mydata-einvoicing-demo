using System.Globalization;
using System.Xml.Linq;
using MyData.Client.Model;

namespace MyData.Client;

/// <summary>
/// Builds the myDATA InvoicesDoc XML. Element order matters: the AADE schema uses
/// xs:sequence, so elements must appear exactly in the order the XSD defines.
/// </summary>
public static class InvoiceXmlWriter
{
    public static readonly XNamespace Inv = "http://www.aade.gr/myDATA/invoice/v1.0";

    // "Classificaton" (missing "i") is AADE's official namespace spelling and must stay as-is.
    public static readonly XNamespace Icls = "https://www.aade.gr/myDATA/incomeClassificaton/v1.0";

    public static XDocument Write(IEnumerable<Invoice> invoices)
    {
        var root = new XElement(Inv + "InvoicesDoc",
            new XAttribute(XNamespace.Xmlns + "icls", Icls),
            invoices.Select(WriteInvoice));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static XElement WriteInvoice(Invoice invoice)
    {
        var totals = InvoiceCalculator.Calculate(invoice);

        return new XElement(Inv + "invoice",
            WriteParty("issuer", invoice.Issuer),
            WriteParty("counterpart", invoice.Counterpart),
            new XElement(Inv + "invoiceHeader",
                new XElement(Inv + "series", invoice.Series),
                new XElement(Inv + "aa", invoice.Number),
                new XElement(Inv + "issueDate", invoice.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new XElement(Inv + "invoiceType", invoice.InvoiceType),
                new XElement(Inv + "currency", invoice.Currency)),
            new XElement(Inv + "paymentMethods",
                new XElement(Inv + "paymentMethodDetails",
                    new XElement(Inv + "type", invoice.PaymentMethodType),
                    new XElement(Inv + "amount", Amount(totals.TotalGross)))),
            totals.Lines.Select(WriteLine),
            new XElement(Inv + "invoiceSummary",
                new XElement(Inv + "totalNetValue", Amount(totals.TotalNet)),
                new XElement(Inv + "totalVatAmount", Amount(totals.TotalVat)),
                new XElement(Inv + "totalWithheldAmount", Amount(0)),
                new XElement(Inv + "totalFeesAmount", Amount(0)),
                new XElement(Inv + "totalStampDutyAmount", Amount(0)),
                new XElement(Inv + "totalOtherTaxesAmount", Amount(0)),
                new XElement(Inv + "totalDeductionsAmount", Amount(0)),
                new XElement(Inv + "totalGrossValue", Amount(totals.TotalGross)),
                totals.ClassificationTotals.Select(c => WriteClassification(c.Classification, c.Amount))));
    }

    private static XElement WriteParty(string name, Party party) =>
        new(Inv + name,
            new XElement(Inv + "vatNumber", party.VatNumber),
            new XElement(Inv + "country", party.Country),
            new XElement(Inv + "branch", party.Branch));

    private static XElement WriteLine(LineTotals line) =>
        new(Inv + "invoiceDetails",
            new XElement(Inv + "lineNumber", line.LineNumber),
            new XElement(Inv + "netValue", Amount(line.Line.NetValue)),
            new XElement(Inv + "vatCategory", (int)line.Line.VatCategory),
            new XElement(Inv + "vatAmount", Amount(line.VatAmount)),
            line.Line.VatExemptionCategory is { } exemption ? new XElement(Inv + "vatExemptionCategory", exemption) : null,
            WriteClassification(line.Line.Classification, line.Line.NetValue));

    private static XElement WriteClassification(IncomeClassification classification, decimal amount) =>
        new(Inv + "incomeClassification",
            new XElement(Icls + "classificationType", classification.Type),
            new XElement(Icls + "classificationCategory", classification.Category),
            new XElement(Icls + "amount", Amount(amount)));

    private static string Amount(decimal value) =>
        InvoiceTotals.Round(value).ToString("0.00", CultureInfo.InvariantCulture);
}
