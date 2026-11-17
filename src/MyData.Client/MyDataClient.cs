using System.Text;
using System.Xml.Linq;
using MyData.Client.Model;

namespace MyData.Client;

/// <param name="UserId">The aade-user-id from the myDATA REST API registration.</param>
/// <param name="SubscriptionKey">The Ocp-Apim-Subscription-Key from the same registration.</param>
public sealed record MyDataCredentials(string UserId, string SubscriptionKey);

public sealed class InvoiceValidationException(IReadOnlyList<string> errors)
    : Exception("Invoice is not valid:\n- " + string.Join("\n- ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>myDATA answered with something other than a ResponseDoc (wrong credentials, outage, throttling).</summary>
public sealed class MyDataTransportException(int statusCode, string body)
    : Exception($"myDATA returned HTTP {statusCode}: {body}")
{
    public int StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}

/// <summary>
/// Client for the myDATA REST API (ERP channel). Pass an <see cref="HttpClient"/> whose
/// BaseAddress points at the test or production environment from AADE's documentation.
/// </summary>
public sealed class MyDataClient(HttpClient http, MyDataCredentials credentials, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>Validates, converts to XML and sends invoices. Returns one result per invoice, in order.</summary>
    public async Task<IReadOnlyList<InvoiceResult>> SendInvoicesAsync(IReadOnlyList<Invoice> invoices, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        var errors = invoices
            .SelectMany((invoice, i) => InvoiceValidator.Validate(invoice, today).Select(e => $"Invoice {i + 1}: {e}"))
            .ToList();
        if (errors.Count > 0)
            throw new InvoiceValidationException(errors);

        var xml = InvoiceXmlWriter.Write(invoices).ToString(SaveOptions.DisableFormatting);
        return await PostAsync("SendInvoices", xml, cancellationToken);
    }

    /// <summary>Cancels a previously sent invoice by its MARK.</summary>
    public async Task<IReadOnlyList<InvoiceResult>> CancelInvoiceAsync(string mark, CancellationToken cancellationToken = default) =>
        await PostAsync($"CancelInvoice?mark={Uri.EscapeDataString(mark)}", body: null, cancellationToken);

    private async Task<IReadOnlyList<InvoiceResult>> PostAsync(string path, string? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("aade-user-id", credentials.UserId);
        request.Headers.Add("Ocp-Apim-Subscription-Key", credentials.SubscriptionKey);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/xml");

        using var response = await http.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new MyDataTransportException((int)response.StatusCode, text);

        return MyDataResponseParser.Parse(text);
    }
}
