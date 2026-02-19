using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.Notifications;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Bookings;

public sealed class BookingActionRequest
{
    [QueryParam]
    public string Token { get; set; } = "";
}

public sealed class BookingAction : Endpoint<BookingActionRequest>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required BookingActionTokenService TokenService { get; init; }
    public required BookingNotificationService NotificationService { get; init; }

    public override void Configure()
    {
        Get("/api/bookings/action");
        AllowAnonymous();
        Throttle(10, 60);
        Description(d => d
            .WithTags("Bookings")
            .WithSummary("Handle booking action via magic link")
            .WithDescription("Accepts or rejects a booking via a signed magic link token. Returns HTML."));
    }

    public override async Task HandleAsync(BookingActionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Token))
        {
            await SendHtmlResponse("Invalid Link", "This link is invalid or malformed.", "error");
            return;
        }

        var payload = TokenService.ValidateToken(req.Token);
        if (payload is null)
        {
            await SendHtmlResponse("Link Expired or Invalid",
                "This link has expired or is invalid. Please log in to the app to manage this booking.", "error");
            return;
        }

        await using var session = DocumentStore.LightweightSession();

        var booking = await session.LoadAsync<Booking>(payload.BookingId, ct);
        if (booking is null)
        {
            await SendHtmlResponse("Booking Not Found",
                "The booking associated with this link could not be found.", "error");
            return;
        }

        var schedule = await session.LoadAsync<Schedule>(booking.ScheduleId, ct);
        if (schedule is null)
        {
            await SendHtmlResponse("Schedule Not Found",
                "The schedule associated with this booking could not be found.", "error");
            return;
        }

        // Idempotency: if already handled, show info page
        if (booking.Status != BookingStatus.Pending)
        {
            var statusLabel = booking.Status switch
            {
                BookingStatus.Confirmed => "already approved",
                BookingStatus.Cancelled => "already declined",
                BookingStatus.Completed => "already completed",
                _ => "already handled"
            };
            await SendHtmlResponse("Already Handled",
                $"This booking has been {statusLabel}. No further action is needed.", "info");
            return;
        }

        if (payload.Action == "approve")
        {
            booking.Status = BookingStatus.Confirmed;
            session.Update(booking);
            NotificationService.NotifyBookingConfirmed(session, booking, schedule);
            await session.SaveChangesAsync(ct);

            Logger.LogInformation("Booking {BookingId} approved via magic link", booking.Id);

            await SendHtmlResponse("Booking Approved",
                $"You have approved the booking for \"{schedule.Title}\" on {schedule.StartTime:MMM dd, yyyy 'at' h:mm tt}. The customer has been notified.",
                "success");
        }
        else if (payload.Action == "reject")
        {
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.SpecifyKind(DateTimeOffset.UtcNow.DateTime, DateTimeKind.Unspecified);
            booking.CancellationReason = "Declined by provider via email";
            schedule.CurrentBookings = Math.Max(0, schedule.CurrentBookings - 1);
            session.Update(booking);
            session.Update(schedule);
            NotificationService.NotifyBookingRejected(session, booking, schedule, "Declined by provider");
            await session.SaveChangesAsync(ct);

            Logger.LogInformation("Booking {BookingId} rejected via magic link", booking.Id);

            await SendHtmlResponse("Booking Declined",
                $"You have declined the booking for \"{schedule.Title}\" on {schedule.StartTime:MMM dd, yyyy 'at' h:mm tt}. The customer has been notified.",
                "error");
        }
        else
        {
            await SendHtmlResponse("Invalid Action",
                "The action specified in this link is not recognized.", "error");
        }
    }

    private async Task SendHtmlResponse(string title, string message, string colorType)
    {
        var color = colorType switch
        {
            "success" => "#16a34a",
            "info" => "#2563EB",
            _ => "#dc2626"
        };

        HttpContext.Response.ContentType = "text/html; charset=utf-8";
        HttpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
        HttpContext.Response.Headers["X-Frame-Options"] = "DENY";
        HttpContext.Response.Headers["Cache-Control"] = "no-store";
        HttpContext.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'";

        var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>{System.Net.WebUtility.HtmlEncode(title)} - BookMeApp</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background: #f3f4f6;"">
    <div style=""max-width: 500px; margin: 60px auto; padding: 20px;"">
        <div style=""text-align: center; margin-bottom: 24px;"">
            <div style=""display: inline-flex; align-items: center; gap: 8px;"">
                <div style=""width: 38px; height: 38px; background: #E85D2A; border-radius: 10px; display: inline-flex; align-items: center; justify-content: center; color: white; font-weight: 700; font-size: 18px; font-family: Georgia, serif;"">B</div>
                <span style=""font-family: Georgia, serif; font-weight: 700; font-size: 20px; color: #333;"">Book Me App!</span>
            </div>
        </div>
        <div style=""background: white; border-radius: 12px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); overflow: hidden;"">
            <div style=""background-color: {color}; color: white; padding: 24px; text-align: center;"">
                <h1 style=""margin: 0; font-size: 22px;"">{System.Net.WebUtility.HtmlEncode(title)}</h1>
            </div>
            <div style=""padding: 30px; text-align: center;"">
                <p style=""font-size: 16px; color: #4b5563;"">{System.Net.WebUtility.HtmlEncode(message)}</p>
                <p style=""margin-top: 24px; color: #9ca3af; font-size: 13px;"">You can close this page.</p>
            </div>
        </div>
        <p style=""text-align: center; color: #9ca3af; font-size: 12px; margin-top: 16px;"">BookMeApp</p>
    </div>
</body>
</html>";
        await HttpContext.Response.WriteAsync(html);
    }
}
