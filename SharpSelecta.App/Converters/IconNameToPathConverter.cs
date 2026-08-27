using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SharpSelecta.App.Converters;

public sealed class IconNameToPathConverter : IValueConverter
{
    public static readonly IconNameToPathConverter Instance = new();

    private const string BasePath = "avares://SharpSelecta.App/Assets/Icons/";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        $"{BasePath}{value}.svg";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
