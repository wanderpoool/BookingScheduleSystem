using BookingScheduleSystem.Contracts.Queue;
using BookingScheduleSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace BookingScheduleSystem.Web.Components.Pages;

public partial class JoinQueue
{
    [Inject] private IQueueService QueueService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "token")]
    private string? Token { get; set; }

    private QueueInfoResponse? _queueInfo;
    private JoinQueueResponse? _joinResult;
    private bool _isLoading = true;
    private bool _isJoining;
    private string? _errorMessage;
    private string _phoneNumber = string.Empty;
    private string? _customerName;

    protected override async Task OnInitializedAsync()
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            _errorMessage = "No queue token provided. Please scan the QR code at the service location.";
            _isLoading = false;
            return;
        }

        try
        {
            _queueInfo = await QueueService.GetQueueInfoAsync(Token);
        }
        catch (HttpRequestException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception)
        {
            _errorMessage = "Unable to load queue information. Please try again.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task HandleJoinQueue()
    {
        if (string.IsNullOrWhiteSpace(_phoneNumber))
        {
            Snackbar.Add("Phone number is required to join the queue.", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Token))
        {
            Snackbar.Add("Invalid queue token.", Severity.Error);
            return;
        }

        _isJoining = true;
        StateHasChanged();

        try
        {
            var request = new JoinQueueRequest
            {
                DailyToken = Token,
                PhoneNumber = _phoneNumber.Trim(),
                CustomerName = string.IsNullOrWhiteSpace(_customerName) ? null : _customerName.Trim()
            };

            _joinResult = await QueueService.JoinQueueAsync(request);
        }
        catch (HttpRequestException ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        catch (Exception)
        {
            Snackbar.Add("Something went wrong. Please try again.", Severity.Error);
        }
        finally
        {
            _isJoining = false;
        }
    }
}
