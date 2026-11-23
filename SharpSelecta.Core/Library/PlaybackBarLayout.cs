using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharpSelecta.Core.Library;

public enum PlaybackBarComponent
{
    Previous,
    PlayPause,
    Next,
    Elapsed,
    SeekBar,
    Length,
    Volume,
    Repeat,
    Equalizer,
    Settings,
}

public enum SeekBarWidth
{
    Fill,
    Fixed,
}

public enum PlaybackBarAlignment
{
    Left,
    Center,
    Right,
}

public enum TitleAlignment
{
    Left,
    Center,
}

// The bottom bar's components in their order on the bar; the ones not listed are hidden.
public sealed record PlaybackBarLayout
{
    public const double MinSeekBarPercent = 20;
    public const double MaxSeekBarPercent = 80;
    public const double DefaultSeekBarPercent = 50;

    public static IReadOnlyList<PlaybackBarComponent> AllComponents { get; } = Enum.GetValues<PlaybackBarComponent>();

    public static PlaybackBarLayout Default { get; } = new();

    // Saved as names, so an id from a newer version is skipped instead of failing the whole settings file.
    [JsonConverter(typeof(ComponentNamesConverter))]
    public IReadOnlyList<PlaybackBarComponent> Components { get; init; } = AllComponents;

    public SeekBarWidth SeekBarWidth { get; init; } = SeekBarWidth.Fill;

    // The seek bar's share of the bar's width when SeekBarWidth is Fixed.
    public double SeekBarPercent { get; init; } = DefaultSeekBarPercent;

    // Where the row sits when the seek bar doesn't fill the free width.
    public PlaybackBarAlignment Alignment { get; init; } = PlaybackBarAlignment.Center;

    public bool ShowTitle { get; init; } = true;

    public TitleAlignment TitleAlignment { get; init; } = TitleAlignment.Left;

    public static bool CanHide(PlaybackBarComponent component) =>
        component is not (PlaybackBarComponent.SeekBar or PlaybackBarComponent.Settings);

    public IEnumerable<PlaybackBarComponent> HiddenComponents => AllComponents.Except(Components);

    // Duplicates dropped, the seek bar and Settings put back if missing, the percent within the slider's range.
    public PlaybackBarLayout Normalize()
    {
        var components = Components.Where(c => Enum.IsDefined(c)).Distinct().ToList();
        if (!components.Contains(PlaybackBarComponent.SeekBar))
        {
            var before = AllComponents.TakeWhile(c => c != PlaybackBarComponent.SeekBar).ToHashSet();
            components.Insert(components.FindLastIndex(before.Contains) + 1, PlaybackBarComponent.SeekBar);
        }

        if (!components.Contains(PlaybackBarComponent.Settings))
            components.Add(PlaybackBarComponent.Settings);

        return this with
        {
            Components = components,
            SeekBarPercent = double.IsNaN(SeekBarPercent)
                ? DefaultSeekBarPercent
                : Math.Clamp(SeekBarPercent, MinSeekBarPercent, MaxSeekBarPercent),
            TitleAlignment = Enum.IsDefined(TitleAlignment) ? TitleAlignment : TitleAlignment.Left,
        };
    }

    public bool Equals(PlaybackBarLayout? other) =>
        other is not null && Components.SequenceEqual(other.Components) && SeekBarWidth == other.SeekBarWidth
        && SeekBarPercent == other.SeekBarPercent && Alignment == other.Alignment && ShowTitle == other.ShowTitle
        && TitleAlignment == other.TitleAlignment;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in Components)
        {
            hash.Add(component);
        }

        hash.Add(SeekBarWidth);
        hash.Add(SeekBarPercent);
        hash.Add(Alignment);
        hash.Add(ShowTitle);
        hash.Add(TitleAlignment);
        return hash.ToHashCode();
    }

    internal sealed class ComponentNamesConverter : JsonConverter<IReadOnlyList<PlaybackBarComponent>>
    {
        public override IReadOnlyList<PlaybackBarComponent> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected an array of component names.");

            var components = new List<PlaybackBarComponent>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String
                    && Enum.TryParse<PlaybackBarComponent>(reader.GetString(), out var component)
                    && Enum.IsDefined(component))
                {
                    components.Add(component);
                }
                else
                {
                    reader.Skip();
                }
            }

            return components;
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyList<PlaybackBarComponent> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var component in value)
            {
                writer.WriteStringValue(component.ToString());
            }

            writer.WriteEndArray();
        }
    }
}
