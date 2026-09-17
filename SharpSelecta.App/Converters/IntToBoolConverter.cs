using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SharpSelecta.App.Converters;

// Binds an int (e.g. a collection Count) to a bool target like IsVisible - true when > 0.
public sealed class IntToBoolConverter : IValueConverter
{
    public static readonly IntToBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
