using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace IndicadoresMovimiento.App.Services;

internal static class QrCodeImage
{
    /// <summary>Genera un código QR en blanco y negro (con su margen blanco) para mostrarlo en pantalla.</summary>
    public static BitmapSource Create(string text, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var matrix = data.ModuleMatrix; // ya incluye la zona de silencio
        var modules = matrix.Count;
        var size = modules * pixelsPerModule;
        var pixels = new byte[size * size];

        for (var y = 0; y < modules; y++)
        {
            var row = matrix[y];
            for (var x = 0; x < modules; x++)
            {
                var value = row[x] ? (byte)0 : (byte)255;
                for (var dy = 0; dy < pixelsPerModule; dy++)
                {
                    var offset = (y * pixelsPerModule + dy) * size + x * pixelsPerModule;
                    Array.Fill(pixels, value, offset, pixelsPerModule);
                }
            }
        }

        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Gray8, null, pixels, size);
        bitmap.Freeze();
        return bitmap;
    }
}
