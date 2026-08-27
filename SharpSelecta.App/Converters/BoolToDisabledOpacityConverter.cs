using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SharpSelecta.App.Converters;

public sealed class BoolToDisabledOpacityConverter : IValueConverter
{
    public static readonly BoolToDisabledOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false ? 0.35 : 1.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
