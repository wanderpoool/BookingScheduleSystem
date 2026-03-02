using BookingScheduleSystem.Contracts.Common;
using Npgsql;

namespace BookingScheduleSystem.Api.Infrastructure.Bookings;

public sealed class BookingReferenceNumberService
{
    private readonly string _connectionString;
    private readonly ILogger<BookingReferenceNumberService> _logger;

    public BookingReferenceNumberService(IConfiguration configuration, ILogger<BookingReferenceNumberService> logger)
    {
        _connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? "Host=localhost;Database=bookingschedule;Username=postgres;Password=postgres";
        _logger = logger;
    }

    public async Task<string> GetNextReferenceNumberAsync(TenantId tenantId, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        // Atomic upsert: insert counter row if it doesn't exist, otherwise increment.
        // PostgreSQL row-level lock on UPDATE guarantees no two concurrent transactions
        // get the same counter value.
        const string sql = """
            INSERT INTO mt_doc_bookingreferencecounter (id, data, mt_version)
            VALUES (@id, @initialData, @version)
            ON CONFLICT (id) DO UPDATE SET
              data = jsonb_set(
                mt_doc_bookingreferencecounter.data,
                '{CurrentValue}',
                (COALESCE((mt_doc_bookingreferencecounter.data->>'CurrentValue')::int, 0) + 1)::text::jsonb
              )
            RETURNING (data->>'CurrentValue')::int;
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", tenantId.Value);
        cmd.Parameters.AddWithValue("initialData", System.Text.Json.JsonSerializer.Serialize(new { CurrentValue = 1 }));
        cmd.Parameters.AddWithValue("version", Guid.NewGuid());

        var result = await cmd.ExecuteScalarAsync(ct);
        var nextValue = Convert.ToInt32(result);

        _logger.LogDebug("Generated reference number BK-{Value:D6} for tenant {TenantId}", nextValue, tenantId);

        return $"BK-{nextValue:D6}";
    }
}
