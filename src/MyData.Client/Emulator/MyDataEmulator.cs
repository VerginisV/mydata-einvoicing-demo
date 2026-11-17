using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace MyData.Client.Emulator;

/// <summary>
/// An offline stand-in for the myDATA API, used by the demo and the tests when no AADE
/// credentials are configured. It checks the same basics myDATA checks (credentials,
/// XML structure, VAT and totals arithmetic) and answers with a ResponseDoc containing a MARK.
/// It is not a full schema validator: always test against AADE's real test environment too.
/// </summary>
public sealed class MyDataEmulator : HttpMessageHandler
{
    private static readonly XNamespace Inv = InvoiceXmlWriter.Inv;
    private static readonly Dictionary<string, decimal> Rates = new()
    {
        ["1"] = 0.24m, ["2"] = 0.13m, ["3"] = 0.06m, ["4"] = 0.17m,
        ["5"] = 0.09m, ["6"] = 0.04m, ["7"] = 0m, ["8"] = 0m
    };

    private readonly string _userId;
    private readonly string _subscriptionKey;
    private readonly HashSet<string> _issuedMarks = [];
    private readonly HashSet<string> _cancelledMarks = [];
    private long _nextMark = 400_001_000_000_000;

    public MyDataEmulator(string userId = "emulator-user", string subscriptionKey = "emulator-key")
    {
        _userId = userId;
        _subscriptionKey = subscriptionKey;
    }

    /// <summary>The last InvoicesDoc received, for inspection in tests.</summary>
    public XDocument? LastRequest { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!HasHeader(request, "aade-user-id", _userId) || !HasHeader(request, "Ocp-Apim-Subscription-Key", _subscriptionKey))
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("Invalid credentials") };

        var path = request.RequestUri!.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/SendInvoices", StringComparison.Ordinal))
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return Xml(SendInvoices(body));
        }

        if (path.EndsWith("/CancelInvoice", StringComparison.Ordinal))
        {
            var mark = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["mark"] ?? "";
            return Xml(Cancel(mark));
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private XElement SendInvoices(string body)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(body);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException)
        {
            return ResponseDoc(Error(1, "XMLSyntaxError", "101", "The XML is not well formed."));
        }

        LastRequest = doc;
        if (doc.Root?.Name != Inv + "InvoicesDoc")
            return ResponseDoc(Error(1, "XMLSyntaxError", "101", "Root element must be InvoicesDoc in the myDATA invoice namespace."));

        var results = doc.Root.Elements(Inv + "invoice").Select((invoice, i) => ProcessInvoice(invoice, i + 1));
        return ResponseDoc([.. results]);
    }

    private XElement ProcessInvoice(XElement invoice, int index)
    {
        var errors = new List<(string Code, string Message)>();

        var lines = invoice.Elements(Inv + "invoiceDetails").ToList();
        if (lines.Count == 0)
            errors.Add(("201", "Invoice has no invoiceDetails."));

        decimal net = 0, vat = 0;
        foreach (var line in lines)
        {
            var lineNet = Dec(line, "netValue");
            var lineVat = Dec(line, "vatAmount");
            var category = line.Element(Inv + "vatCategory")?.Value ?? "";
            net += lineNet;
            vat += lineVat;

            if (!Rates.TryGetValue(category, out var rate))
                errors.Add(("202", $"Line {line.Element(Inv + "lineNumber")?.Value}: unknown vatCategory '{category}'."));
            else if (Math.Abs(Math.Round(lineNet * rate, 2, MidpointRounding.AwayFromZero) - lineVat) > 0.01m)
                errors.Add(("203", $"Line {line.Element(Inv + "lineNumber")?.Value}: vatAmount does not match vatCategory."));
        }

        var summary = invoice.Element(Inv + "invoiceSummary");
        if (summary is null)
        {
            errors.Add(("204", "Invoice has no invoiceSummary."));
        }
        else
        {
            if (Dec(summary, "totalNetValue") != net) errors.Add(("205", "totalNetValue does not equal the sum of the lines."));
            if (Dec(summary, "totalVatAmount") != vat) errors.Add(("206", "totalVatAmount does not equal the sum of the lines."));
            if (Dec(summary, "totalGrossValue") != net + vat) errors.Add(("207", "totalGrossValue is not net plus VAT."));
        }

        if (errors.Count > 0)
            return Error(index, "ValidationError", errors);

        var mark = (_nextMark++).ToString(CultureInfo.InvariantCulture);
        _issuedMarks.Add(mark);
        var uid = Uid(invoice);

        return new XElement("response",
            new XElement("index", index),
            new XElement("invoiceUid", uid),
            new XElement("invoiceMark", mark),
            new XElement("qrUrl", $"https://example.invalid/mydata-emulator/qr/{uid}"),
            new XElement("statusCode", "Success"));
    }

    private XElement Cancel(string mark)
    {
        if (!_issuedMarks.Contains(mark))
            return ResponseDoc(Error(1, "ValidationError", "301", $"No invoice with MARK {mark}."));
        if (!_cancelledMarks.Add(mark))
            return ResponseDoc(Error(1, "ValidationError", "302", $"Invoice with MARK {mark} is already cancelled."));

        return ResponseDoc(new XElement("response",
            new XElement("index", 1),
            new XElement("cancellationMark", (_nextMark++).ToString(CultureInfo.InvariantCulture)),
            new XElement("statusCode", "Success")));
    }

    private static string Uid(XElement invoice)
    {
        var header = invoice.Element(Inv + "invoiceHeader");
        var issuer = invoice.Element(Inv + "issuer");
        var key = string.Join("-",
            issuer?.Element(Inv + "vatNumber")?.Value,
            header?.Element(Inv + "issueDate")?.Value,
            issuer?.Element(Inv + "branch")?.Value,
            header?.Element(Inv + "invoiceType")?.Value,
            header?.Element(Inv + "series")?.Value,
            header?.Element(Inv + "aa")?.Value);
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key)));
    }

    private static decimal Dec(XElement parent, string name) =>
        decimal.TryParse(parent.Element(Inv + name)?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;

    private static bool HasHeader(HttpRequestMessage request, string name, string expected) =>
        request.Headers.TryGetValues(name, out var values) && values.Contains(expected);

    private static XElement Error(int index, string status, string code, string message) => Error(index, status, [(code, message)]);

    private static XElement Error(int index, string status, IEnumerable<(string Code, string Message)> errors) =>
        new("response",
            new XElement("index", index),
            new XElement("statusCode", status),
            new XElement("errors", errors.Select(e => new XElement("error",
                new XElement("message", e.Message),
                new XElement("code", e.Code)))));

    private static XElement ResponseDoc(params XElement[] responses) => new("ResponseDoc", responses);

    private static HttpResponseMessage Xml(XElement root) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(new XDocument(root).ToString(), Encoding.UTF8, "application/xml")
    };
}
