using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace SharpSelecta.App.Converters;

public sealed class ArtworkConverter : IValueConverter
{
    public static readonly ArtworkConverter Instance = new();

    private readonly ConditionalWeakTable<byte[], Bitmap> _decodedCache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is byte[] bytes ? _decodedCache.GetValue(bytes, static b => new Bitmap(new MemoryStream(b))) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
