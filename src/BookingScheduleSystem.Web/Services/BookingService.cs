using System.Net;
using System.Net.Http.Json;
using BookingScheduleSystem.Contracts.Bookings;
using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Web.Services;

public class BookingService : IBookingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BookingService> _logger;

    public BookingService(HttpClient httpClient, ILogger<BookingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BookingResponse?> CreateBookingAsync(CreateBookingRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/bookings", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<BookingResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to create booking: {StatusCode} - {Error}", response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetCreateBookingError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    public async Task<ListBookingsResponse?> ListBookingsAsync(BookingStatus? status = null, int page = 1, int pageSize = 20)
    {
        try
        {
            var url = $"/api/bookings?pageNumber={page}&pageSize={pageSize}";
            if (status.HasValue)
                url += $"&status={status.Value}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ListBookingsResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to list bookings: {StatusCode} - {Error}", response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetListBookingsError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing bookings");
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    public async Task<BookingResponse?> GetBookingAsync(Guid bookingId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/bookings/{bookingId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<BookingResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to get booking {BookingId}: {StatusCode} - {Error}", bookingId, response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetBookingDetailError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking {BookingId}", bookingId);
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    public async Task<BookingResponse?> CancelBookingAsync(Guid bookingId, string? reason = null)
    {
        try
        {
            var request = new CancelBookingRequest { CancellationReason = reason };
            var response = await _httpClient.PostAsJsonAsync($"/api/bookings/{bookingId}/cancel", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<BookingResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to cancel booking: {StatusCode} - {Error}", response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetCancelBookingError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling booking {BookingId}", bookingId);
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    public async Task<BookingResponse?> ApproveBookingAsync(Guid bookingId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/bookings/{bookingId}/approve", null);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<BookingResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to approve booking {BookingId}: {StatusCode} - {Error}", bookingId, response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetApproveBookingError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving booking {BookingId}", bookingId);
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    public async Task<BookingResponse?> RejectBookingAsync(Guid bookingId, string? reason = null)
    {
        try
        {
            var request = new { Reason = reason };
            var response = await _httpClient.PostAsJsonAsync($"/api/bookings/{bookingId}/reject", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<BookingResponse>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to reject booking {BookingId}: {StatusCode} - {Error}", bookingId, response.StatusCode, error);
            var message = ApiErrorHelper.ExtractMessage(error) ?? GetRejectBookingError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting booking {BookingId}", bookingId);
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    private static string GetCreateBookingError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "This schedule is no longer available. It may have been removed or rescheduled.",
        HttpStatusCode.Conflict => "This schedule may already be fully booked, or you already have a booking for it.",
        HttpStatusCode.BadRequest => "We couldn't process your booking. Please check the details and try again.",
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again to continue.",
        HttpStatusCode.Forbidden => "You don't have permission to book this schedule.",
        _ => "Something went wrong while creating your booking. Please try again in a moment."
    };

    private static string GetListBookingsError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again to view your bookings.",
        HttpStatusCode.Forbidden => "You don't have permission to view these bookings.",
        _ => "We couldn't load your bookings right now. Please try again in a moment."
    };

    private static string GetBookingDetailError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "This booking could not be found. It may have been removed.",
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
        HttpStatusCode.Forbidden => "You don't have permission to view this booking.",
        _ => "We couldn't load the booking details. Please try again in a moment."
    };

    private static string GetCancelBookingError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "This booking could not be found. It may have already been cancelled or removed.",
        HttpStatusCode.Conflict => "This booking can no longer be cancelled. It may have already been completed or cancelled.",
        HttpStatusCode.BadRequest => "We couldn't cancel this booking. Please try again.",
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again to continue.",
        HttpStatusCode.Forbidden => "You don't have permission to cancel this booking.",
        _ => "Something went wrong while cancelling your booking. Please try again in a moment."
    };

    private static string GetApproveBookingError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "This booking could not be found.",
        HttpStatusCode.BadRequest => "This booking is not pending and cannot be approved.",
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again to continue.",
        HttpStatusCode.Forbidden => "You don't have permission to approve this booking.",
        _ => "Something went wrong while approving the booking. Please try again in a moment."
    };

    private static string GetRejectBookingError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "This booking could not be found.",
        HttpStatusCode.BadRequest => "This booking is not pending and cannot be rejected.",
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again to continue.",
        HttpStatusCode.Forbidden => "You don't have permission to reject this booking.",
        _ => "Something went wrong while rejecting the booking. Please try again in a moment."
    };

}
