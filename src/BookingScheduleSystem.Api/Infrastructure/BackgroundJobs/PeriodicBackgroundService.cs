namespace BookingScheduleSystem.Api.Infrastructure.BackgroundJobs;

/// <summary>
/// Base class for periodic background jobs. Handles the timer loop, DI scoping,
/// and error isolation so derived classes only implement the work.
/// </summary>
public abstract class PeriodicBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly TimeSpan _interval;

    protected PeriodicBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        TimeSpan interval)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = interval;
    }

    protected abstract string JobName { get; }

    protected abstract Task ExecuteJobAsync(IServiceScope scope, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{JobName} started. Running every {Interval}", JobName, _interval);

        using var timer = new PeriodicTimer(_interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await ExecuteJobAsync(scope, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown — not an error
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{JobName} encountered an error. Will retry on next interval", JobName);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("{JobName} stopped", JobName);
    }
}
