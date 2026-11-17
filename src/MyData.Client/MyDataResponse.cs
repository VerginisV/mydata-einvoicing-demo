using System.Xml.Linq;

namespace MyData.Client;

public sealed record MyDataError(string Code, string Message);

/// <summary>The result for one invoice in a SendInvoices call.</summary>
/// <param name="Mark">ΜΑΡΚ: the registration number myDATA assigns. Present only on success.</param>
public sealed record InvoiceResult(
    int Index,
    string StatusCode,
    string? InvoiceUid,
    string? Mark,
    string? QrUrl,
    string? CancellationMark,
    IReadOnlyList<MyDataError> Errors)
{
    public bool IsSuccess => StatusCode == "Success";
}

public static class MyDataResponseParser
{
    /// <summary>
    /// Parses a ResponseDoc. Elements are matched by local name, because the namespace
    /// declarations in responses have changed between API versions.
    /// </summary>
    public static IReadOnlyList<InvoiceResult> Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        return [.. doc.Root!.Elements().Where(e => e.Name.LocalName == "response").Select(ParseResponse)];
    }

    private static InvoiceResult ParseResponse(XElement response)
    {
        string? Value(string name) => response.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

        var errors = response.Elements()
            .Where(e => e.Name.LocalName == "errors")
            .SelectMany(e => e.Elements().Where(x => x.Name.LocalName == "error"))
            .Select(e => new MyDataError(
                e.Elements().FirstOrDefault(x => x.Name.LocalName == "code")?.Value ?? "",
                e.Elements().FirstOrDefault(x => x.Name.LocalName == "message")?.Value ?? ""))
            .ToList();

        return new InvoiceResult(
            int.TryParse(Value("index"), out var index) ? index : 0,
            Value("statusCode") ?? "Unknown",
            Value("invoiceUid"),
            Value("invoiceMark"),
            Value("qrUrl"),
            Value("cancellationMark"),
            errors);
    }
}
