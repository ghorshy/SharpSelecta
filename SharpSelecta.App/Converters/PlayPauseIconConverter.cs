using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SharpSelecta.App.Converters;

public sealed class PlayPauseIconConverter : IValueConverter
{
    public static readonly PlayPauseIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true
            ? IconPaths.Resolve("Icon.PlayerPause", "avares://SharpSelecta.App/Assets/Icons/player-pause.svg")
            : IconPaths.Resolve("Icon.PlayerPlay", "avares://SharpSelecta.App/Assets/Icons/player-play.svg");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
