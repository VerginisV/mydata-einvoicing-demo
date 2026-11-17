using MyData.Client.Model;

namespace MyData.Tests;

public static class Samples
{
    public static readonly DateOnly Today = new(2026, 11, 17);
    public static readonly Party Issuer = new("123456783");
    public static readonly Party Customer = new("090000045");
    public static readonly IncomeClassification Goods = new("E3_561_001", "category1_1");
    public static readonly IncomeClassification Services = new("E3_561_001", "category1_3");

    public static Invoice Invoice(params InvoiceLine[] lines) => new(
        Issuer,
        Customer,
        Series: "A",
        Number: "1",
        IssueDate: Today,
        InvoiceType: InvoiceTypes.SalesInvoice,
        Lines: lines.Length > 0 ? lines : [new InvoiceLine(100m, VatCategory.Vat24, Goods)],
        PaymentMethodType: PaymentMethodTypes.WebBanking);
}
