using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace URLShortener.API.Services;

/// <summary>
/// Service for generating QR codes for shortened URLs
/// </summary>
public interface IQRCodeService
{
    /// <summary>
    /// Generates a QR code image for the given URL
    /// </summary>
    /// <param name="url">The URL to encode</param>
    /// <param name="size">Size of the QR code in pixels</param>
    /// <param name="darkColor">Dark color for the QR code (default: black)</param>
    /// <param name="lightColor">Light color for the QR code (default: white)</param>
    /// <returns>QR code as byte array (PNG format)</returns>
    byte[] GenerateQRCode(string url, int size = 300, string? darkColor = null, string? lightColor = null);
}

public class QRCodeService : IQRCodeService
{
    private readonly ILogger<QRCodeService> _logger;

    public QRCodeService(ILogger<QRCodeService> logger)
    {
        _logger = logger;
    }

    public byte[] GenerateQRCode(string url, int size = 300, string? darkColor = null, string? lightColor = null)
    {
        try
        {
            // Validate size
            if (size < 50 || size > 1000)
            {
                size = 300; // Default to 300 if invalid
            }

            // Generate QR code
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeImage = qrCode.GetGraphic(20, 
                ParseColor(darkColor, Color.Black),
                ParseColor(lightColor, Color.White));

            _logger.LogDebug("Generated QR code for URL: {Url}, Size: {Size}", url, size);
            
            return qrCodeImage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate QR code for URL: {Url}", url);
            throw new InvalidOperationException("Failed to generate QR code", ex);
        }
    }

    private byte[] ParseColor(string? colorString, Color defaultColor)
    {
        if (string.IsNullOrWhiteSpace(colorString))
        {
            return new[] { defaultColor.R, defaultColor.G, defaultColor.B, defaultColor.A };
        }

        try
        {
            // Support hex colors (e.g., #FF0000 or FF0000)
            if (colorString.StartsWith("#"))
            {
                colorString = colorString.Substring(1);
            }

            if (colorString.Length == 6)
            {
                var r = Convert.ToByte(colorString.Substring(0, 2), 16);
                var g = Convert.ToByte(colorString.Substring(2, 2), 16);
                var b = Convert.ToByte(colorString.Substring(4, 2), 16);
                return new byte[] { r, g, b, 255 };
            }
            else if (colorString.Length == 8)
            {
                var r = Convert.ToByte(colorString.Substring(0, 2), 16);
                var g = Convert.ToByte(colorString.Substring(2, 2), 16);
                var b = Convert.ToByte(colorString.Substring(4, 2), 16);
                var a = Convert.ToByte(colorString.Substring(6, 2), 16);
                return new byte[] { r, g, b, a };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse color: {Color}, using default", colorString);
        }

        return new[] { defaultColor.R, defaultColor.G, defaultColor.B, defaultColor.A };
    }
}