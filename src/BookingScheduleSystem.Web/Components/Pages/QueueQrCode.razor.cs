using BookingScheduleSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace BookingScheduleSystem.Web.Components.Pages;

public partial class QueueQrCode
{
    [Parameter] public string? Slug { get; set; }

    [Inject] private IQueueService QueueService { get; set; } = default!;
    [Inject] private IQrCodeService QrCodeService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private string? _qrCodeBase64;
    private bool _isLoading = true;
    private bool _featureDisabled;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await GenerateQrCode();
    }

    private async Task GenerateQrCode()
    {
        _isLoading = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            var queue = await QueueService.GetTodayQueueAsync();
            if (queue != null)
            {
                var baseUrl = Navigation.BaseUri.TrimEnd('/');
                var joinUrl = $"{baseUrl}/queue/join?token={Uri.EscapeDataString(queue.DailyToken)}";
                _qrCodeBase64 = QrCodeService.GenerateQrCodeBase64(joinUrl, 10);
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("not enabled", StringComparison.OrdinalIgnoreCase))
        {
            _featureDisabled = true;
        }
        catch (HttpRequestException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception)
        {
            _errorMessage = "Failed to generate QR code. Please try again.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task Print()
    {
        await JS.InvokeVoidAsync("window.print");
    }
}
