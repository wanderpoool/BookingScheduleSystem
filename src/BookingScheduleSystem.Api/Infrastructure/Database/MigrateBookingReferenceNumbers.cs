using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Contracts.Common;
using Marten;

namespace BookingScheduleSystem.Api.Infrastructure.Database;

/// <summary>
/// One-time startup migration: assigns sequential reference numbers to existing bookings
/// that don't have one. Groups by tenant and orders by BookedAt so earlier bookings get
/// lower numbers. Idempotent — skips bookings that already have a reference number.
/// </summary>
public sealed class MigrateBookingReferenceNumbers : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MigrateBookingReferenceNumbers> _logger;

    public MigrateBookingReferenceNumbers(IServiceProvider services, ILogger<MigrateBookingReferenceNumbers> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
            var referenceService = scope.ServiceProvider.GetRequiredService<BookingReferenceNumberService>();

            await using var session = store.LightweightSession();

            var bookingsWithoutRef = await session.Query<Booking>()
                .Where(b => b.ReferenceNumber == null)
                .OrderBy(b => b.BookedAt)
                .ToListAsync(cancellationToken);

            if (bookingsWithoutRef.Count == 0)
            {
                _logger.LogInformation("Booking reference migration: all bookings already have reference numbers");
                return;
            }

            // Group by tenant so each tenant gets independent sequential numbers
            var grouped = bookingsWithoutRef.GroupBy(b => b.TenantId);
            var migrated = 0;

            foreach (var tenantGroup in grouped)
            {
                foreach (var booking in tenantGroup.OrderBy(b => b.BookedAt))
                {
                    booking.ReferenceNumber = await referenceService.GetNextReferenceNumberAsync(
                        tenantGroup.Key, cancellationToken);
                    session.Update(booking);
                    migrated++;
                }
            }

            if (migrated > 0)
            {
                await session.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Booking reference migration: migrated {Count} bookings with reference numbers", migrated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Booking reference migration failed — will retry on next startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
