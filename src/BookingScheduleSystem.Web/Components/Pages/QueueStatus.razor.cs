using BookingScheduleSystem.Contracts.Queue;
using BookingScheduleSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace BookingScheduleSystem.Web.Components.Pages;

public partial class QueueStatus
{
    [Parameter] public Guid EntryId { get; set; }

    [Inject] private IQueueService QueueService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private QueueEntryResponse? _entry;
    private bool _isLoading = true;
    private bool _isRefreshing;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadEntry();
    }

    private async Task LoadEntry()
    {
        try
        {
            _entry = await QueueService.GetEntryStatusAsync(EntryId);
        }
        catch (HttpRequestException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception)
        {
            _errorMessage = "Unable to load your queue status. Please try again.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshStatus()
    {
        _isRefreshing = true;
        StateHasChanged();

        try
        {
            _entry = await QueueService.GetEntryStatusAsync(EntryId);
        }
        catch (HttpRequestException ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        catch (Exception)
        {
            Snackbar.Add("Unable to refresh. Please try again.", Severity.Error);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private Color GetStatusColor() => _entry?.Status switch
    {
        QueueEntryStatus.Waiting => Color.Warning,
        QueueEntryStatus.Called => Color.Success,
        QueueEntryStatus.InService => Color.Info,
        QueueEntryStatus.Completed => Color.Default,
        QueueEntryStatus.Skipped => Color.Error,
        QueueEntryStatus.NoShow => Color.Error,
        _ => Color.Default
    };

    private string GetStatusLabel() => _entry?.Status switch
    {
        QueueEntryStatus.Waiting => "Waiting",
        QueueEntryStatus.Called => "Called",
        QueueEntryStatus.InService => "In Service",
        QueueEntryStatus.Completed => "Completed",
        QueueEntryStatus.Skipped => "Skipped",
        QueueEntryStatus.NoShow => "No Show",
        _ => "Unknown"
    };

    private string GetStatusCardStyle()
    {
        var borderColor = _entry?.Status switch
        {
            QueueEntryStatus.Called => "var(--mud-palette-success)",
            QueueEntryStatus.Waiting => "var(--mud-palette-warning)",
            QueueEntryStatus.InService => "var(--mud-palette-info)",
            _ => "var(--mud-palette-divider)"
        };
        return $"border: 2px solid {borderColor}; border-radius: 14px;";
    }
}
