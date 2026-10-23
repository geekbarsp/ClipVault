using ClipVault.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ClipVault_App.Converters;

public sealed class ContentTypeGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is ClipboardContentType type ? type switch
    {
        ClipboardContentType.Image => "\uEB9F",
        ClipboardContentType.Link => "\uE71B",
        ClipboardContentType.Files => "\uE8B7",
        ClipboardContentType.Html => "\uE943",
        ClipboardContentType.RichText => "\uE8D2",
        _ => "\uE8A5"
    } : "\uE8A5";
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public sealed class DateTimeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is DateTimeOffset instant ? instant.ToLocalTime().ToString("M/d/yyyy h:mm:ss tt") : "";
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public sealed class BooleanGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is true ? "\uE735" : "\uE734";
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public sealed class BooleanOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is true ? 1d : 0.45d;
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public sealed class BooleanVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public sealed class MonitoringLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is true ? "Pause monitoring" : "Resume monitoring";
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is null ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public sealed class LocalImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string path || !File.Exists(path)) return null;
        try { return new BitmapImage(new Uri(path)) { DecodePixelWidth = parameter is string width && int.TryParse(width, out var pixels) ? pixels : 800 }; }
        catch { return null; }
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public sealed class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string path || !File.Exists(path)) return "Stored locally";
        var bytes = new FileInfo(path).Length;
        return bytes >= 1_048_576 ? $"{bytes / 1_048_576d:0.##} MB" : $"{bytes / 1024d:0.##} KB";
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
