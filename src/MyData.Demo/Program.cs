using System.Xml.Linq;
using MyData.Client;
using MyData.Client.Emulator;
using MyData.Client.Model;

// Without credentials the demo runs against the offline emulator.
// To use AADE's test environment, register for the myDATA REST API test environment and set:
//   MYDATA_USER_ID, MYDATA_SUBSCRIPTION_KEY and MYDATA_BASE_URL (the test URL from AADE's docs).
// Real AADE test credentials also need real VAT numbers: replace the example ones below.
var userId = Environment.GetEnvironmentVariable("MYDATA_USER_ID");
var subscriptionKey = Environment.GetEnvironmentVariable("MYDATA_SUBSCRIPTION_KEY");
var baseUrl = Environment.GetEnvironmentVariable("MYDATA_BASE_URL") ?? "https://mydataapidev.aade.gr/";
var useEmulator = string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(subscriptionKey);

HttpClient http;
MyDataCredentials credentials;
MyDataEmulator? emulator = null;

if (useEmulator)
{
    emulator = new MyDataEmulator();
    http = new HttpClient(emulator) { BaseAddress = new Uri("https://emulator.local/") };
    credentials = new MyDataCredentials("emulator-user", "emulator-key");
    Console.WriteLine("Mode: offline emulator (set MYDATA_USER_ID and MYDATA_SUBSCRIPTION_KEY to use AADE's test environment)\n");
}
else
{
    http = new HttpClient { BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/") };
    credentials = new MyDataCredentials(userId!, subscriptionKey!);
    Console.WriteLine($"Mode: AADE myDATA at {http.BaseAddress}\n");
}

var client = new MyDataClient(http, credentials);

// Example VAT numbers with valid check digits. They are not real businesses.
var issuer = new Party("123456783");
var customer = new Party("090000045");
var goods = new IncomeClassification("E3_561_001", "category1_1"); // Wholesale sales of goods

var invoice = new Invoice(
    Issuer: issuer,
    Counterpart: customer,
    Series: "A",
    Number: "1001",
    IssueDate: DateOnly.FromDateTime(DateTime.Today),
    InvoiceType: InvoiceTypes.SalesInvoice,
    Lines:
    [
        new InvoiceLine(100.00m, VatCategory.Vat24, goods),
        new InvoiceLine(49.99m, VatCategory.Vat13, goods)
    ],
    PaymentMethodType: PaymentMethodTypes.WebBanking);

var totals = InvoiceCalculator.Calculate(invoice);
Console.WriteLine($"Invoice {invoice.Series}-{invoice.Number}: net {totals.TotalNet:0.00}, VAT {totals.TotalVat:0.00}, total {totals.TotalGross:0.00} EUR");
Console.WriteLine("\nXML sent to myDATA:\n");
Console.WriteLine(InvoiceXmlWriter.Write([invoice]));

// 1. Send the invoice and read back the MARK.
var result = (await client.SendInvoicesAsync([invoice]))[0];
Print("Send invoice", result);

// 2. Show client-side validation catching a mistake before anything is sent.
var broken = invoice with { Number = "1002", Counterpart = new Party("123456789") };
try
{
    await client.SendInvoicesAsync([broken]);
}
catch (InvoiceValidationException ex)
{
    Console.WriteLine($"\nValidation caught a bad invoice before sending:\n{ex.Message}");
}

// 3. Cancel the first invoice by its MARK.
if (result is { IsSuccess: true, Mark: { } mark })
    Print("Cancel invoice", (await client.CancelInvoiceAsync(mark))[0]);

static void Print(string step, InvoiceResult r)
{
    Console.WriteLine($"\n{step}: {r.StatusCode}");
    if (r.Mark is not null) Console.WriteLine($"  MARK:             {r.Mark}");
    if (r.InvoiceUid is not null) Console.WriteLine($"  UID:              {r.InvoiceUid}");
    if (r.QrUrl is not null) Console.WriteLine($"  QR URL:           {r.QrUrl}");
    if (r.CancellationMark is not null) Console.WriteLine($"  Cancellation MARK: {r.CancellationMark}");
    foreach (var e in r.Errors) Console.WriteLine($"  Error {e.Code}: {e.Message}");
}
