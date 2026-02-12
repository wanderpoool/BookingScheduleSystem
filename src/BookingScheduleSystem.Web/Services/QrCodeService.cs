using QRCoder;

namespace BookingScheduleSystem.Web.Services;

public class QrCodeService : IQrCodeService
{
    public string GenerateQrCodeBase64(string url, int pixelsPerModule = 10)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var pngBytes = qrCode.GetGraphic(pixelsPerModule);
        return Convert.ToBase64String(pngBytes);
    }
}
