using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SharpSelecta.App.Converters;

public sealed class PlayPauseIconConverter : IValueConverter
{
    public static readonly PlayPauseIconConverter Instance = new();

    private const string BasePath = "avares://SharpSelecta.App/Assets/Icons/";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? $"{BasePath}player-pause.svg" : $"{BasePath}player-play.svg";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
