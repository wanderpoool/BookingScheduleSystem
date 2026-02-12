namespace BookingScheduleSystem.Web.Services;

public interface IQrCodeService
{
    string GenerateQrCodeBase64(string url, int pixelsPerModule = 10);
}
