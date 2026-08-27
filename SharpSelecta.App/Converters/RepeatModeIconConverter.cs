using System;
using System.Globalization;
using Avalonia.Data.Converters;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.App.Converters;

public sealed class RepeatModeIconConverter : IValueConverter
{
    public static readonly RepeatModeIconConverter Instance = new();

    private const string BasePath = "avares://SharpSelecta.App/Assets/Icons/";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            RepeatMode.RepeatAll => $"{BasePath}repeat.svg",
            RepeatMode.RepeatOne => $"{BasePath}repeat-once.svg",
            _ => $"{BasePath}repeat-off.svg",
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
