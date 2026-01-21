using System.Net;

namespace Hazina.AgentFactory.Tests.Fixtures;

/// <summary>
/// Mock HttpMessageHandler for testing HTTP-based services.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<HttpRequestMessage> _requests = new();

    /// <summary>
    /// Gets all captured requests.
    /// </summary>
    public IReadOnlyList<HttpRequestMessage> CapturedRequests => _requests.AsReadOnly();

    /// <summary>
    /// Enqueues a response to be returned by the next request.
    /// </summary>
    public void EnqueueResponse(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Enqueues a simple JSON response.
    /// </summary>
    public void EnqueueJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Enqueues an error response.
    /// </summary>
    public void EnqueueErrorResponse(HttpStatusCode statusCode, string message = "Error")
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(message)
        };
        _responses.Enqueue(response);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add(request);

        if (_responses.Count == 0)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("No mock response configured")
            });
        }

        return Task.FromResult(_responses.Dequeue());
    }

    /// <summary>
    /// Creates an HttpClient using this mock handler.
    /// </summary>
    public HttpClient CreateClient()
    {
        return new HttpClient(this);
    }
}
