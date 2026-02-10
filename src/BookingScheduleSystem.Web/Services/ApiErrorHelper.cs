using System.Text.Json;

namespace BookingScheduleSystem.Web.Services;

/// <summary>
/// Extracts user-friendly error messages from FastEndpoints RFC 7807-style error responses.
/// </summary>
public static class ApiErrorHelper
{
    /// <summary>
    /// Attempts to extract the first generalError from a FastEndpoints error response body.
    /// Returns null if the body is not valid JSON or doesn't contain generalErrors.
    /// </summary>
    public static string? ExtractMessage(string? errorBody)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            var root = doc.RootElement;

            // FastEndpoints format: { "errors": { "generalErrors": ["msg"] } }
            if (root.TryGetProperty("errors", out var errors)
                && errors.TryGetProperty("generalErrors", out var generalErrors)
                && generalErrors.ValueKind == JsonValueKind.Array
                && generalErrors.GetArrayLength() > 0)
            {
                return generalErrors[0].GetString();
            }

            // Fallback: { "message": "..." }
            if (root.TryGetProperty("message", out var message)
                && message.GetString() is { } msg
                && msg != "One or more errors occurred!")
            {
                return msg;
            }
        }
        catch
        {
            // Not JSON or unexpected format
        }

        return null;
    }
}
