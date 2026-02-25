using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;

namespace BookingScheduleSystem.Web.Infrastructure.Telemetry;

/// <summary>
/// Signs outbound HTTP requests with AWS SigV4 for the X-Ray OTLP endpoint.
/// Uses AWSCredentials which auto-refresh via ECS task role.
/// </summary>
public sealed class XRaySigV4Handler : DelegatingHandler
{
    private readonly AWSCredentials _credentials;
    private readonly string _region;
    private readonly ILogger? _logger;
    private const string Service = "xray";
    private const string Algorithm = "AWS4-HMAC-SHA256";

    public XRaySigV4Handler(AWSCredentials credentials, string region, ILogger? logger = null)
        : base(new HttpClientHandler())
    {
        _credentials = credentials;
        _region = region;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ImmutableCredentials creds;
        try
        {
            creds = await _credentials.GetCredentialsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve AWS credentials for SigV4 signing");
            throw;
        }

        var now = DateTime.UtcNow;
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

        // Read and hash the payload
        var payloadBytes = request.Content != null
            ? await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false)
            : [];
        var payloadHash = HexEncode(SHA256.HashData(payloadBytes));

        // Set required SigV4 headers
        request.Headers.Remove("x-amz-date");
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.Remove("x-amz-content-sha256");
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);

        if (!string.IsNullOrEmpty(creds.Token))
        {
            request.Headers.Remove("x-amz-security-token");
            request.Headers.TryAddWithoutValidation("x-amz-security-token", creds.Token);
        }

        // Build canonical request
        var uri = request.RequestUri!;
        var canonicalUri = uri.AbsolutePath;
        var canonicalQueryString = uri.Query.TrimStart('?');

        // Collect and sort headers for signing
        var sortedHeaders = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        sortedHeaders["host"] = uri.Host;
        foreach (var header in request.Headers)
        {
            sortedHeaders[header.Key.ToLowerInvariant()] = string.Join(",", header.Value).Trim();
        }
        if (request.Content?.Headers != null)
        {
            foreach (var header in request.Content.Headers)
            {
                sortedHeaders[header.Key.ToLowerInvariant()] = string.Join(",", header.Value).Trim();
            }
        }

        var canonicalHeaders = new StringBuilder();
        var signedHeadersList = new List<string>();
        foreach (var kvp in sortedHeaders)
        {
            canonicalHeaders.Append(kvp.Key).Append(':').Append(kvp.Value).Append('\n');
            signedHeadersList.Add(kvp.Key);
        }
        var signedHeaders = string.Join(";", signedHeadersList);

        var canonicalRequest = string.Join("\n",
            request.Method.Method,
            canonicalUri,
            canonicalQueryString,
            canonicalHeaders.ToString(),
            signedHeaders,
            payloadHash);

        // String to sign
        var credentialScope = $"{dateStamp}/{_region}/{Service}/aws4_request";
        var stringToSign = string.Join("\n",
            Algorithm,
            amzDate,
            credentialScope,
            HexEncode(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

        // Derive signing key and compute signature
        var signingKey = DeriveSigningKey(creds.SecretKey, dateStamp, _region, Service);
        var signature = HexEncode(HmacSha256(signingKey, stringToSign));

        // Set Authorization header
        var authValue = $"Credential={creds.AccessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.Authorization = new AuthenticationHeaderValue(Algorithm, authValue);

        // Re-set content since we already read the bytes — preserve all content headers
        if (payloadBytes.Length > 0)
        {
            var originalContentHeaders = request.Content?.Headers.ToList() ?? [];
            request.Content = new ByteArrayContent(payloadBytes);
            foreach (var header in originalContentHeaders)
            {
                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger?.LogError(
                "X-Ray OTLP export failed: {StatusCode} {ReasonPhrase} — {ResponseBody}",
                (int)response.StatusCode, response.ReasonPhrase, body);
        }

        return response;
    }

    private static byte[] DeriveSigningKey(string secretKey, string dateStamp, string region, string service)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + secretKey), dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string HexEncode(byte[] data) =>
        Convert.ToHexString(data).ToLowerInvariant();
}
