namespace MyData.Client.Model;

/// <summary>myDATA VAT categories (vatCategory) and their rates.</summary>
public enum VatCategory
{
    Vat24 = 1,
    Vat13 = 2,
    Vat6 = 3,
    /// <summary>Reduced island rate (17%).</summary>
    Vat17 = 4,
    /// <summary>Reduced island rate (9%).</summary>
    Vat9 = 5,
    /// <summary>Reduced island rate (4%).</summary>
    Vat4 = 6,
    /// <summary>0% VAT: requires a VAT exemption reason.</summary>
    Vat0 = 7,
    /// <summary>Entries without VAT (for example fees).</summary>
    NoVat = 8
}

public static class VatRates
{
    public static decimal For(VatCategory category) => category switch
    {
        VatCategory.Vat24 => 0.24m,
        VatCategory.Vat13 => 0.13m,
        VatCategory.Vat6 => 0.06m,
        VatCategory.Vat17 => 0.17m,
        VatCategory.Vat9 => 0.09m,
        VatCategory.Vat4 => 0.04m,
        VatCategory.Vat0 or VatCategory.NoVat => 0m,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown VAT category.")
    };
}

/// <summary>Common myDATA invoice types (invoiceType).</summary>
public static class InvoiceTypes
{
    /// <summary>Sales invoice (Τιμολόγιο Πώλησης).</summary>
    public const string SalesInvoice = "1.1";

    /// <summary>Services invoice (Τιμολόγιο Παροχής Υπηρεσιών).</summary>
    public const string ServicesInvoice = "2.1";

    /// <summary>Credit note linked to an earlier invoice (Πιστωτικό Τιμολόγιο Συσχετιζόμενο).</summary>
    public const string CreditNote = "5.1";
}

/// <summary>Common myDATA payment method codes (paymentMethodDetails/type).</summary>
public static class PaymentMethodTypes
{
    public const int DomesticBankAccount = 1;
    public const int ForeignBankAccount = 2;
    public const int Cash = 3;
    public const int Cheque = 4;
    public const int OnCredit = 5;
    public const int WebBanking = 6;
    public const int Pos = 7;
}

/// <summary>A business identified by its Greek VAT number (ΑΦΜ) and branch (0 = head office).</summary>
public sealed record Party(string VatNumber, string Country = "GR", int Branch = 0);

/// <summary>An income classification (χαρακτηρισμός εσόδων), for example E3_561_001 / category1_1.</summary>
public sealed record IncomeClassification(string Type, string Category);

public sealed record InvoiceLine(
    decimal NetValue,
    VatCategory VatCategory,
    IncomeClassification Classification,
    int? VatExemptionCategory = null);

public sealed record Invoice(
    Party Issuer,
    Party Counterpart,
    string Series,
    string Number,
    DateOnly IssueDate,
    string InvoiceType,
    IReadOnlyList<InvoiceLine> Lines,
    int PaymentMethodType,
    string Currency = "EUR");
