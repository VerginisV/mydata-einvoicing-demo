# myDATA E-Invoicing Client (.NET)

[![CI](https://github.com/VerginisV/mydata-einvoicing-demo/actions/workflows/ci.yml/badge.svg)](https://github.com/VerginisV/mydata-einvoicing-demo/actions/workflows/ci.yml)

A .NET 10 client for **myDATA**, the Greek tax authority's (AADE) electronic books and e-invoicing platform. It turns an invoice into myDATA's XML format, checks it before sending, submits it, and reads back the **MARK** (Μοναδικός Αριθμός Καταχώρισης), the registration number that makes an invoice official.

It includes an **offline emulator** of the myDATA API, so the whole flow runs without AADE credentials.

## Why this is harder than it looks

- **Strict XML.** The AADE schema uses ordered sequences: elements in the wrong order are rejected. Amounts need exactly two decimals with a dot, whatever the server's culture.
- **Rounding must match exactly.** VAT is rounded per line, and the invoice totals must equal the sum of the rounded lines. Rounding the total instead is one of the most common rejections (see `Totals_are_the_sum_of_the_rounded_lines`).
- **Greek VAT numbers have a check digit.** A typo in an ΑΦΜ is caught locally, before anything is sent.
- **Quirks that must be kept.** AADE's official namespace is spelled `incomeClassificaton` (missing "i"). Fixing the typo breaks the integration.

## What it does

```
Invoice (C# record) ─▶ InvoiceValidator ─▶ InvoiceCalculator ─▶ InvoiceXmlWriter ─▶ myDATA SendInvoices
                         ΑΦΜ check digit      VAT per line          ordered XML          (or the emulator)
                         dates, lines         totals, grouping      official namespaces          │
                                                                                                 ▼
                                                     MARK, UID, QR URL ◀── MyDataResponseParser
```

| Part | Responsibility |
| --- | --- |
| `Model/Invoice.cs` | Invoice, lines, parties, VAT categories, invoice types and payment methods |
| `InvoiceValidator` | ΑΦΜ check digit, required fields, no future dates, 0% VAT needs an exemption reason |
| `InvoiceCalculator` | VAT per line (half away from zero), totals, income classification totals |
| `InvoiceXmlWriter` | InvoicesDoc XML in schema order with the official namespaces |
| `MyDataClient` | Sends invoices and cancellations with the `aade-user-id` and `Ocp-Apim-Subscription-Key` headers |
| `MyDataResponseParser` | Reads MARK, UID, QR URL and errors from the ResponseDoc |
| `Emulator/MyDataEmulator` | Offline stand-in for myDATA: checks credentials, VAT and totals, returns MARKs |

## Run the demo

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/MyData.Demo
```

It builds a sales invoice with two VAT rates, prints the XML, sends it, shows a bad invoice being caught before sending, and cancels the first invoice:

```
Invoice A-1001: net 149.99, VAT 30.50, total 180.49 EUR

Send invoice: Success
  MARK:             400001000000000
  UID:              3AC14CC36618094E02281574BDAA5FD3209EA3A3

Validation caught a bad invoice before sending:
- Invoice 1: Counterpart: '123456789' is not a valid Greek VAT number (ΑΦΜ).

Cancel invoice: Success
  Cancellation MARK: 400001000000001
```

### Against AADE's test environment

1. Register for the myDATA REST API **test environment** on aade.gr to get a user ID and subscription key.
2. Replace the example VAT numbers in `src/MyData.Demo/Program.cs` with your own registered ones.
3. Set the credentials and run:

```bash
export MYDATA_USER_ID=your-user-id
export MYDATA_SUBSCRIPTION_KEY=your-subscription-key
export MYDATA_BASE_URL=https://mydataapidev.aade.gr/   # check AADE's current docs for the test URL
dotnet run --project src/MyData.Demo
```

## Use the client

```csharp
var http = new HttpClient { BaseAddress = new Uri("https://mydataapidev.aade.gr/") };
var client = new MyDataClient(http, new MyDataCredentials(userId, subscriptionKey));

var invoice = new Invoice(
    Issuer: new Party("123456783"),
    Counterpart: new Party("090000045"),
    Series: "A", Number: "1001",
    IssueDate: DateOnly.FromDateTime(DateTime.Today),
    InvoiceType: InvoiceTypes.SalesInvoice,
    Lines: [new InvoiceLine(100m, VatCategory.Vat24, new IncomeClassification("E3_561_001", "category1_1"))],
    PaymentMethodType: PaymentMethodTypes.WebBanking);

var result = (await client.SendInvoicesAsync([invoice]))[0];
if (result.IsSuccess)
    Console.WriteLine($"Registered with MARK {result.Mark}");
else
    foreach (var e in result.Errors) Console.WriteLine($"{e.Code}: {e.Message}");
```

## Tests

```bash
dotnet test
```

40 xUnit tests: VAT rounding and totals, the ΑΦΜ check digit, XML namespaces and element order, response parsing (success, validation errors, namespaced responses), HTTP headers and error handling, and full send and cancel flows against the emulator. CI also runs the demo on every push.

## Scope and limitations

This is a focused portfolio project, not a complete myDATA implementation.

- **Covered:** sales and services invoices (1.1, 2.1), all VAT categories, income classifications, payment methods, sending and cancelling.
- **Not covered yet:** credit notes linked to an earlier MARK, withholding taxes, stamp duty and other taxes, delivery notes, expense classifications, retail receipts, and fetching documents (`RequestDocs`).
- **Not validated against the official XSD in this repo.** The format follows AADE's published samples and documentation; always run invoices through AADE's test environment before production.
- **Since 2026, B2B e-invoices must go through a certified provider** (or AADE's own apps) rather than the direct ERP channel. In production, this XML-building and validation logic would sit in front of a provider's API.

## License

MIT
