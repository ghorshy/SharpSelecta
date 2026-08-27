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
                <= 0 => IconPaths.Resolve("Icon.VolumeOff", $"{BasePath}volume-off.svg"),
                < 0.33 => IconPaths.Resolve("Icon.Volume4", $"{BasePath}volume-4.svg"),
                < 0.66 => IconPaths.Resolve("Icon.Volume2", $"{BasePath}volume-2.svg"),
                _ => IconPaths.Resolve("Icon.Volume", $"{BasePath}volume.svg"),
            }
            : IconPaths.Resolve("Icon.Volume", $"{BasePath}volume.svg");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
