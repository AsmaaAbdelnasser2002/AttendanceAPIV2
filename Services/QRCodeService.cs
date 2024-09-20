using SkiaSharp;
using SkiaSharp.QrCode;
using SkiaSharp.QrCode.Models;

public class QRCodeService
{
	public string GenerateQRCode(string text)
	{
		// Define QR Code parameters
		var qrCodeGenerator = new QRCodeGenerator();
		var qr = qrCodeGenerator.CreateQrCode(text, ECCLevel.Q);

		// Generate QR Code as a bitmap image
		var info = new SKImageInfo(256, 256);
		using (var surface = SKSurface.Create(info))
		{
			var canvas = surface.Canvas;
			canvas.Render(qr, 256, 256, SKColors.Black, SKColors.White);
			canvas.Flush();

			using (var image = surface.Snapshot())
			using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
			{
				// Return Base64 string of the QR code
				return Convert.ToBase64String(data.ToArray());
			}
		}
	}
}
