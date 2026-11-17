using System.Net;
using MyData.Client;
using MyData.Client.Emulator;
using MyData.Client.Model;

namespace MyData.Tests;

public class MyDataClientTests
{
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class CapturingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero));

    private static MyDataClient ClientFor(HttpMessageHandler handler, string user = "emulator-user", string key = "emulator-key") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://mydata.test/") }, new MyDataCredentials(user, key), Time);

    [Fact]
    public async Task Sends_credentials_and_xml_to_SendInvoices()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, "<ResponseDoc><response><index>1</index><statusCode>Success</statusCode></response></ResponseDoc>");

        await ClientFor(handler, "user-1", "key-1").SendInvoicesAsync([Samples.Invoice()]);

        Assert.Equal("https://mydata.test/SendInvoices", handler.Request!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal("user-1", handler.Request.Headers.GetValues("aade-user-id").Single());
        Assert.Equal("key-1", handler.Request.Headers.GetValues("Ocp-Apim-Subscription-Key").Single());
        Assert.Equal("application/xml", handler.Request.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("<InvoicesDoc", handler.RequestBody);
    }

    [Fact]
    public async Task Invalid_invoices_are_never_sent()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, "<ResponseDoc/>");
        var invalid = Samples.Invoice() with { Counterpart = new Party("123456789") };

        var ex = await Assert.ThrowsAsync<InvoiceValidationException>(() => ClientFor(handler).SendInvoicesAsync([invalid]));

        Assert.Null(handler.Request);
        Assert.Contains(ex.Errors, e => e.Contains("Invoice 1"));
    }

    [Fact]
    public async Task Http_errors_raise_a_transport_exception_with_the_body()
    {
        var handler = new CapturingHandler(HttpStatusCode.Unauthorized, "Access denied due to invalid subscription key.");

        var ex = await Assert.ThrowsAsync<MyDataTransportException>(() => ClientFor(handler).SendInvoicesAsync([Samples.Invoice()]));

        Assert.Equal(401, ex.StatusCode);
        Assert.Contains("invalid subscription key", ex.Body);
    }

    [Fact]
    public async Task Emulator_returns_a_mark_for_a_valid_invoice()
    {
        var results = await ClientFor(new MyDataEmulator()).SendInvoicesAsync([Samples.Invoice()]);

        var result = Assert.Single(results);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Mark);
        Assert.Equal(40, result.InvoiceUid!.Length);
    }

    [Fact]
    public async Task Emulator_returns_one_result_per_invoice_in_order()
    {
        var results = await ClientFor(new MyDataEmulator()).SendInvoicesAsync(
            [Samples.Invoice(), Samples.Invoice() with { Number = "2" }]);

        Assert.Equal(new[] { 1, 2 }, results.Select(r => r.Index).ToList());
        Assert.NotEqual(results[0].Mark, results[1].Mark);
    }

    [Fact]
    public async Task Emulator_rejects_wrong_credentials()
    {
        var client = ClientFor(new MyDataEmulator(), key: "wrong");

        var ex = await Assert.ThrowsAsync<MyDataTransportException>(() => client.SendInvoicesAsync([Samples.Invoice()]));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Cancel_returns_a_cancellation_mark_once()
    {
        var client = ClientFor(new MyDataEmulator());
        var mark = (await client.SendInvoicesAsync([Samples.Invoice()]))[0].Mark!;

        var first = (await client.CancelInvoiceAsync(mark))[0];
        var second = (await client.CancelInvoiceAsync(mark))[0];

        Assert.True(first.IsSuccess);
        Assert.NotNull(first.CancellationMark);
        Assert.False(second.IsSuccess);
        Assert.Equal("302", second.Errors.Single().Code);
    }
}
