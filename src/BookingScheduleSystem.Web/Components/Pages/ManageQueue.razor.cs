using BookingScheduleSystem.Contracts.Queue;
using BookingScheduleSystem.Web.Components.Shared;
using BookingScheduleSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace BookingScheduleSystem.Web.Components.Pages;

public partial class ManageQueue
{
    [Parameter] public string? Slug { get; set; }

    [Inject] private IQueueService QueueService { get; set; } = default!;
    [Inject] private ITenantSlugService SlugService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private List<QueueEntryResponse>? _entries;
    private bool _isLoading = true;
    private bool _featureDisabled;
    private int _totalWaiting;
    private int _totalCalled;
    private int _totalCompleted;

    protected override async Task OnInitializedAsync()
    {
        await LoadQueue();
    }

    private async Task LoadQueue()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            // Initialize today's queue first
            var queue = await QueueService.GetTodayQueueAsync();
            if (queue != null)
            {
                _entries = queue.Entries
                    .OrderBy(e => e.Status switch
                    {
                        QueueEntryStatus.Called => 0,
                        QueueEntryStatus.InService => 1,
                        QueueEntryStatus.Waiting => 2,
                        _ => 3
                    })
                    .ThenBy(e => e.QueueNumber)
                    .ToList();

                _totalWaiting = queue.TotalWaiting;
                _totalCalled = queue.TotalCalled;
                _totalCompleted = queue.TotalCompleted;
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("not enabled", StringComparison.OrdinalIgnoreCase))
        {
            _featureDisabled = true;
        }
        catch (HttpRequestException ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        catch (Exception)
        {
            Snackbar.Add("Failed to load queue. Please try again.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task CallNext(QueueEntryResponse entry)
    {
        await UpdateStatus(entry, QueueEntryStatus.Called, "Customer called successfully.");
    }

    private async Task StartService(QueueEntryResponse entry)
    {
        await UpdateStatus(entry, QueueEntryStatus.InService, "Service started.");
    }

    private async Task MarkDone(QueueEntryResponse entry)
    {
        await UpdateStatus(entry, QueueEntryStatus.Completed, "Marked as completed.");
    }

    private async Task SkipEntry(QueueEntryResponse entry)
    {
        await UpdateStatus(entry, QueueEntryStatus.Skipped, "Customer skipped.");
    }

    private async Task MarkNoShow(QueueEntryResponse entry)
    {
        await UpdateStatus(entry, QueueEntryStatus.NoShow, "Marked as no-show.");
    }

    private async Task UpdateStatus(QueueEntryResponse entry, QueueEntryStatus newStatus, string successMessage)
    {
        try
        {
            var request = new UpdateQueueEntryStatusRequest { Status = newStatus };
            await QueueService.UpdateEntryStatusAsync(entry.Id.Value, request);
            Snackbar.Add(successMessage, Severity.Success);
            await LoadQueue();
        }
        catch (HttpRequestException ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task MoveUp(QueueEntryResponse entry)
    {
        await ReorderEntry(entry, -1);
    }

    private async Task MoveDown(QueueEntryResponse entry)
    {
        await ReorderEntry(entry, 1);
    }

    private async Task ReorderEntry(QueueEntryResponse entry, int offset)
    {
        var waitingEntries = _entries?
            .Where(e => e.Status == QueueEntryStatus.Waiting)
            .OrderBy(e => e.QueueNumber)
            .ToList();

        if (waitingEntries == null) return;

        var currentIndex = waitingEntries.FindIndex(e => e.Id == entry.Id);
        var newPosition = currentIndex + 1 + offset;

        if (newPosition < 1 || newPosition > waitingEntries.Count) return;

        try
        {
            var request = new ReorderQueueEntryRequest { NewPosition = newPosition };
            await QueueService.ReorderEntryAsync(entry.Id.Value, request);
            Snackbar.Add("Queue order updated.", Severity.Success);
            await LoadQueue();
        }
        catch (HttpRequestException ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task OpenAddCustomerDialog()
    {
        var dialog = await DialogService.ShowAsync<AddQueueEntryDialog>(
            "Add Customer to Queue",
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });

        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
        {
            await LoadQueue();
        }
    }

    private static Color GetEntryStatusColor(QueueEntryStatus status) => status switch
    {
        QueueEntryStatus.Waiting => Color.Warning,
        QueueEntryStatus.Called => Color.Success,
        QueueEntryStatus.InService => Color.Info,
        QueueEntryStatus.Completed => Color.Default,
        QueueEntryStatus.Skipped => Color.Error,
        QueueEntryStatus.NoShow => Color.Error,
        _ => Color.Default
    };

    private static string GetEntryStatusLabel(QueueEntryStatus status) => status switch
    {
        QueueEntryStatus.Waiting => "Waiting",
        QueueEntryStatus.Called => "Called",
        QueueEntryStatus.InService => "In Service",
        QueueEntryStatus.Completed => "Completed",
        QueueEntryStatus.Skipped => "Skipped",
        QueueEntryStatus.NoShow => "No Show",
        _ => "Unknown"
    };

    private static string GetEntryCardStyle(QueueEntryResponse entry)
    {
        var borderColor = entry.Status switch
        {
            QueueEntryStatus.Called => "var(--mud-palette-success)",
            QueueEntryStatus.InService => "var(--mud-palette-info)",
            QueueEntryStatus.Waiting when entry.IsPriority => "var(--mud-palette-error)",
            QueueEntryStatus.Waiting => "var(--mud-palette-warning)",
            _ => "var(--mud-palette-text-disabled)"
        };
        return $"border-left: 4px solid {borderColor}; border: 1px solid var(--mud-palette-divider); border-left: 4px solid {borderColor}; border-radius: 14px;";
    }
}
