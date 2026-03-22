using System.Net;
using System.Net.Http.Json;
using BookingScheduleSystem.Contracts.Queue;

namespace BookingScheduleSystem.Web.Services;

public class QueueService : IQueueService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<QueueService> _logger;

    public QueueService(HttpClient httpClient, ILogger<QueueService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GetTodayQueueResponse?> GetTodayQueueAsync()
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/queue/today", new { });
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<GetTodayQueueResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to get today's queue: {StatusCode} - {Error}", response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetTodayQueueError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today's queue");
            throw new HttpRequestException("Unable to connect to the server. Please try again.", ex);
        }
    }

    public async Task<List<QueueEntryResponse>?> GetTodayQueueEntriesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/queue/today/entries");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<QueueEntryResponse>>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to get queue entries: {StatusCode} - {Error}", response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetQueueEntriesError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue entries");
            throw new HttpRequestException("Unable to connect to the server. Please try again.", ex);
        }
    }

    public async Task<QueueEntryResponse?> AddQueueEntryAsync(AddQueueEntryRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/queue/entries", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<QueueEntryResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to add queue entry: {StatusCode} - {Error}", response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetAddEntryError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding queue entry");
            throw new HttpRequestException("Unable to connect to the server. Please try again.", ex);
        }
    }

    public async Task<QueueEntryResponse?> UpdateEntryStatusAsync(Guid entryId, UpdateQueueEntryStatusRequest request)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/queue/entries/{entryId}/status")
            {
                Content = JsonContent.Create(request)
            };
            var response = await _httpClient.SendAsync(httpRequest);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<QueueEntryResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to update queue entry {EntryId}: {StatusCode} - {Error}", entryId, response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetUpdateStatusError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating queue entry {EntryId}", entryId);
            throw new HttpRequestException("Unable to connect to the server. Please try again.", ex);
        }
    }

    public async Task<QueueEntryResponse?> ReorderEntryAsync(Guid entryId, ReorderQueueEntryRequest request)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/queue/entries/{entryId}/reorder")
            {
                Content = JsonContent.Create(request)
            };
            var response = await _httpClient.SendAsync(httpRequest);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<QueueEntryResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to reorder queue entry {EntryId}: {StatusCode} - {Error}", entryId, response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetReorderError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering queue entry {EntryId}", entryId);
            throw new HttpRequestException("Unable to connect to the server. Please try again.", ex);
        }
    }

    public async Task<QueueInfoResponse?> GetQueueInfoAsync(string token)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/queue/info?token={Uri.EscapeDataString(token)}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<QueueInfoResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to get queue info: {StatusCode} - {Error}", response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetQueueInfoError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue info for token");
            throw new HttpRequestException("Unable to connect to the server. Please try again.", ex);
        }
    }

    public async Task<JoinQueueResponse?> JoinQueueAsync(JoinQueueRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/queue/join", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<JoinQueueResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to join queue: {StatusCode} - {Error}", response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetJoinQueueError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining queue");
            throw new HttpRequestException("Unable to connect to the server. Please try again.", ex);
        }
    }

    public async Task<QueueEntryResponse?> GetEntryStatusAsync(Guid entryId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/queue/entries/{entryId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<QueueEntryResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to get queue entry {EntryId}: {StatusCode} - {Error}", entryId, response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetEntryStatusError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue entry status {EntryId}", entryId);
            throw new HttpRequestException("Unable to connect to the server. Please try again.", ex);
        }
    }

    private static string GetTodayQueueError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
        HttpStatusCode.Forbidden => "You don't have permission to manage the queue.",
        _ => "Something went wrong loading the queue. Please try again."
    };

    private static string GetQueueEntriesError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
        HttpStatusCode.Forbidden => "You don't have permission to view queue entries.",
        _ => "Something went wrong loading queue entries. Please try again."
    };

    private static string GetAddEntryError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => "Please check the customer details and try again.",
        HttpStatusCode.Conflict => "This phone number is already in today's queue.",
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
        HttpStatusCode.Forbidden => "You don't have permission to add queue entries.",
        _ => "Something went wrong adding the customer. Please try again."
    };

    private static string GetUpdateStatusError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "This queue entry could not be found.",
        HttpStatusCode.BadRequest => "This status change is not allowed.",
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
        HttpStatusCode.Forbidden => "You don't have permission to update queue entries.",
        _ => "Something went wrong updating the status. Please try again."
    };

    private static string GetReorderError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "This queue entry could not be found.",
        HttpStatusCode.BadRequest => "Invalid position. Please try again.",
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
        HttpStatusCode.Forbidden => "You don't have permission to reorder queue entries.",
        _ => "Something went wrong reordering. Please try again."
    };

    private static string GetQueueInfoError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "This queue link is invalid or has expired. Please scan today's QR code.",
        HttpStatusCode.BadRequest => "Invalid queue token.",
        _ => "Something went wrong loading queue information. Please try again."
    };

    private static string GetJoinQueueError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "This queue link has expired. Please scan today's QR code.",
        HttpStatusCode.Conflict => "This phone number is already in today's queue.",
        HttpStatusCode.BadRequest => "Please check your details and try again.",
        _ => "Something went wrong joining the queue. Please try again."
    };

    private static string GetEntryStatusError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "This queue entry could not be found.",
        _ => "Something went wrong checking your status. Please try again."
    };
}
