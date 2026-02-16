using System.Net;

namespace BookingScheduleSystem.Web.Services;

/// <summary>
/// DelegatingHandler that retries HTTP requests on transient 502/503/504 responses
/// with exponential backoff. Applies to all API calls through the HttpClient pipeline.
/// </summary>
public class TransientRetryHandler : DelegatingHandler
{
    private const int MaxRetries = 3;
    private readonly ILogger<TransientRetryHandler> _logger;

    public TransientRetryHandler(ILogger<TransientRetryHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var requestToSend = attempt == 0 ? request : await CloneRequestAsync(request);

            response = await base.SendAsync(requestToSend, cancellationToken);

            if (!IsTransientError(response.StatusCode) || attempt == MaxRetries)
                return response;

            var delay = 1000 * (1 << attempt); // 1s, 2s, 4s
            _logger.LogWarning(
                "Transient {StatusCode} from {Method} {Url}, retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})",
                (int)response.StatusCode, request.Method, request.RequestUri, delay, attempt + 1, MaxRetries);

            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }

        return response!;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version
        };

        if (original.Content != null)
        {
            var contentBytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        foreach (var option in original.Options)
            clone.Options.TryAdd(option.Key, option.Value);

        return clone;
    }

    private static bool IsTransientError(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
}
