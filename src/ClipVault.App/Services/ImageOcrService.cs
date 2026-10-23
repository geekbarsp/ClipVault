using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace ClipVault_App.Services;

public sealed class ImageOcrService
{
    public async Task<string> ExtractAsync(string imagePath)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages() ?? throw new InvalidOperationException("No Windows OCR language is installed.");
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        return (await engine.RecognizeAsync(bitmap)).Text.Trim();
    }
}
