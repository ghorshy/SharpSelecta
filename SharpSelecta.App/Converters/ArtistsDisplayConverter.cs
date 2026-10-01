using System;
using System.Globalization;
using Avalonia.Data.Converters;
using SharpSelecta.App.Formatting;

namespace SharpSelecta.App.Converters;

// Shows an Artist tag value (several artists joined by the tag separator) with the display separator.
// A converter rather than a view-model property so a column's sort path stays on the raw tag value.
public sealed class ArtistsDisplayConverter : IValueConverter
{
    public static readonly ArtistsDisplayConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        TrackFormatting.FormatArtists(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
