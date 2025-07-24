using NSubstitute;
using System.Net;

namespace TestUtilities;

public static class MockHttpClientFactory
{
    public static HttpClient CreateMockHttpClient(HttpStatusCode statusCode = HttpStatusCode.OK, string responseContent = "", Dictionary<string, string> headers = null)
    {
        var handler = new MockHttpMessageHandler(statusCode, responseContent, headers);
        return new HttpClient(handler);
    }

    public static HttpClient CreateMockHttpClientWithDelay(TimeSpan delay, HttpStatusCode statusCode = HttpStatusCode.OK, string responseContent = "")
    {
        var handler = new MockHttpMessageHandler(statusCode, responseContent, null, delay);
        return new HttpClient(handler);
    }

    public static HttpClient CreateMockHttpClientWithException(Exception exception)
    {
        var handler = new MockHttpMessageHandler(exception);
        return new HttpClient(handler);
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseContent;
    private readonly Dictionary<string, string> _headers;
    private readonly TimeSpan? _delay;
    private readonly Exception _exception;

    public MockHttpMessageHandler(HttpStatusCode statusCode, string responseContent, Dictionary<string, string> headers = null, TimeSpan? delay = null)
    {
        _statusCode = statusCode;
        _responseContent = responseContent;
        _headers = headers ?? new Dictionary<string, string>();
        _delay = delay;
    }

    public MockHttpMessageHandler(Exception exception)
    {
        _exception = exception;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_exception != null)
        {
            throw _exception;
        }

        if (_delay.HasValue)
        {
            await Task.Delay(_delay.Value, cancellationToken);
        }

        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseContent)
        };

        foreach (var header in _headers)
        {
            response.Headers.Add(header.Key, header.Value);
        }

        return response;
    }
}