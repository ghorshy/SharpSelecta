using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace SharpSelecta.App.Converters;

public sealed class ArtworkConverter : IValueConverter
{
    public static readonly ArtworkConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is byte[] bytes ? new Bitmap(new MemoryStream(bytes)) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
