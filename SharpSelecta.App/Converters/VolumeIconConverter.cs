using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SharpSelecta.App.Converters;

public sealed class VolumeIconConverter : IValueConverter
{
    public static readonly VolumeIconConverter Instance = new();

    private const string BasePath = "avares://SharpSelecta.App/Assets/Icons/";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double volume
            ? volume switch
            {
                <= 0 => $"{BasePath}volume-off.svg",
                < 0.33 => $"{BasePath}volume-4.svg",
                < 0.66 => $"{BasePath}volume-2.svg",
                _ => $"{BasePath}volume.svg",
            }
            : $"{BasePath}volume.svg";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
