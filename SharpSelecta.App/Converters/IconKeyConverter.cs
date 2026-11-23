using System.Globalization;
using Avalonia.Data.Converters;

namespace SharpSelecta.App.Converters;

// An Icon.* key to the theme's path for it.
public sealed class IconKeyConverter : IValueConverter
{
    public static readonly IconKeyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key ? IconPaths.Resolve(key, string.Empty) is { Length: > 0 } path ? path : null : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
