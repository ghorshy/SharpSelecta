using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SharpSelecta.App.Converters;

public sealed class PlayPauseIconConverter : IValueConverter
{
    public static readonly PlayPauseIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true
            ? "avares://SharpSelecta.App/Assets/Icons/player-pause.svg"
            : "avares://SharpSelecta.App/Assets/Icons/player-play.svg";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
