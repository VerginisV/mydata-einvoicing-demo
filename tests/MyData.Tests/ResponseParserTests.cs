using MyData.Client;

namespace MyData.Tests;

public class ResponseParserTests
{
    [Fact]
    public void Parses_a_successful_response()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <ResponseDoc xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <response>
                <index>1</index>
                <invoiceUid>3AC14CC36618094E02281574BDAA5FD3209EA3A3</invoiceUid>
                <invoiceMark>400001953410227</invoiceMark>
                <qrUrl>https://mydataapidev.aade.gr/TimologioQR/QRInfo?q=abc</qrUrl>
                <statusCode>Success</statusCode>
              </response>
            </ResponseDoc>
            """;

        var result = Assert.Single(MyDataResponseParser.Parse(xml));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Index);
        Assert.Equal("400001953410227", result.Mark);
        Assert.Equal("3AC14CC36618094E02281574BDAA5FD3209EA3A3", result.InvoiceUid);
        Assert.StartsWith("https://", result.QrUrl);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parses_validation_errors()
    {
        const string xml = """
            <ResponseDoc>
              <response>
                <index>1</index>
                <statusCode>ValidationError</statusCode>
                <errors>
                  <error><message>Invalid counterpart VAT number</message><code>104</code></error>
                  <error><message>totalNetValue mismatch</message><code>203</code></error>
                </errors>
              </response>
            </ResponseDoc>
            """;

        var result = Assert.Single(MyDataResponseParser.Parse(xml));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Mark);
        Assert.Equal(new[] { "104", "203" }, result.Errors.Select(e => e.Code).ToList());
    }

    [Fact]
    public void Ignores_namespaces_on_response_elements()
    {
        const string xml = """
            <ResponseDoc xmlns="http://www.aade.gr/myDATA/response/v1.0">
              <response><index>2</index><invoiceMark>1</invoiceMark><statusCode>Success</statusCode></response>
            </ResponseDoc>
            """;

        var result = Assert.Single(MyDataResponseParser.Parse(xml));

        Assert.Equal(2, result.Index);
        Assert.Equal("1", result.Mark);
    }
}
