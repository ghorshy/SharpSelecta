using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using SharpSelecta.App.Resources;

namespace SharpSelecta.App.Services;

public sealed class ThemeFileException(int line, string message) : Exception(message)
{
    public int Line { get; } = line;
}

// The resources of a theme file; disposing it detaches the brushes that follow another resource.
public sealed class ParsedTheme(ResourceDictionary resources, IReadOnlyList<IDisposable> bindings) : IDisposable
{
    public ResourceDictionary Resources { get; } = resources;

    public void Dispose()
    {
        foreach (var binding in bindings)
        {
            binding.Dispose();
        }
    }
}

// Reads a custom theme (a ResourceDictionary .axaml) without the runtime XAML loader, which NativeAOT can't
// run and which would let a downloaded file create any Avalonia object. Only resource values are allowed.
public static class ThemeFileParser
{
    private const string AvaloniaNamespace = "https://github.com/avaloniaui";
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    // colorSource resolves {DynamicResource Key} brush colors live; existingType gives the type a key already
    // has in the app (null: no such resource), and null checks no keys at all.
    public static ParsedTheme Parse(string text, IResourceHost? colorSource, Func<string, Type?>? existingType)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(text, LoadOptions.SetLineInfo);
        }
        catch (XmlException exception)
        {
            throw new ThemeFileException(exception.LineNumber, Strings.ThemeErrorNotXml(exception.Message));
        }

        var root = document.Root!;
        if (root.Name != XName.Get("ResourceDictionary", AvaloniaNamespace))
            throw new ThemeFileException(Line(root), Strings.ThemeErrorRoot);

        var bindings = new List<IDisposable>();
        var resources = new ResourceDictionary();
        try
        {
            new Reader(colorSource, existingType, bindings).ReadInto(root, resources);
        }
        catch
        {
            new ParsedTheme(resources, bindings).Dispose();
            throw;
        }

        return new ParsedTheme(resources, bindings);
    }

    private static int Line(XObject node) => node is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;

    private sealed class Reader(IResourceHost? colorSource, Func<string, Type?>? existingType, List<IDisposable> bindings)
    {
        public void ReadInto(XElement dictionary, ResourceDictionary target)
        {
            foreach (var element in dictionary.Elements())
            {
                if (element.Name == XName.Get("ResourceDictionary.ThemeDictionaries", AvaloniaNamespace))
                {
                    ReadThemeDictionaries(element, target);
                    continue;
                }

                if (element.Attribute(XName.Get("Key", XamlNamespace))?.Value is not { Length: > 0 } key)
                    throw new ThemeFileException(Line(element), Strings.ThemeErrorMissingKey(element.Name.LocalName));

                if (target.ContainsKey(key))
                    throw new ThemeFileException(Line(element), Strings.ThemeErrorDuplicateKey(key));

                var value = Value(element);
                CheckType(element, key, value);
                target[key] = value;
            }
        }

        private void ReadThemeDictionaries(XElement element, ResourceDictionary target)
        {
            foreach (var dictionary in element.Elements())
            {
                var variant = dictionary.Name == XName.Get("ResourceDictionary", AvaloniaNamespace)
                    ? dictionary.Attribute(XName.Get("Key", XamlNamespace))?.Value switch
                    {
                        "Light" => ThemeVariant.Light,
                        "Dark" => ThemeVariant.Dark,
                        _ => null,
                    }
                    : null;
                if (variant is null)
                    throw new ThemeFileException(Line(dictionary), Strings.ThemeErrorThemeDictionary);

                var resources = new ResourceDictionary();
                ReadInto(dictionary, resources);
                target.ThemeDictionaries[variant] = resources;
            }
        }

        private object Value(XElement element)
        {
            var text = element.Value.Trim();
            var name = element.Name;
            var isSystemType = name.NamespaceName == XamlNamespace || name.NamespaceName.StartsWith("clr-namespace:System", StringComparison.Ordinal);
            object? value = (name.NamespaceName, name.LocalName) switch
            {
                (AvaloniaNamespace, "Color") => Color.TryParse(text, out var color) ? color : null,
                (AvaloniaNamespace, "SolidColorBrush") => Brush(element),
                (AvaloniaNamespace, "Thickness") => Try(() => Thickness.Parse(text)),
                (AvaloniaNamespace, "CornerRadius") => Try(() => CornerRadius.Parse(text)),
                (AvaloniaNamespace, "GridLength") => Try(() => GridLength.Parse(text)),
                (AvaloniaNamespace, "FontFamily") => text.Length > 0 ? Try(() => FontFamily.Parse(text)) : null,
                (AvaloniaNamespace, "FontWeight") => FontWeightOf(text),
                (AvaloniaNamespace, "StreamGeometry") => Try(() => StreamGeometry.Parse(text)),
                (AvaloniaNamespace, "TransformOperations") => Try(() => TransformOperations.Parse(text)),
                (_, "Double") when isSystemType =>
                    double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null,
                (_, "String") when isSystemType => element.Value,
                (_, "TimeSpan") when isSystemType =>
                    TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var time) ? time : null,
                _ => throw new ThemeFileException(Line(element), Strings.ThemeErrorUnsupportedElement(name.LocalName)),
            };

            return value ?? throw new ThemeFileException(Line(element), Strings.ThemeErrorBadValue(text, name.LocalName));
        }

        // A literal color, or {DynamicResource Key} that follows that resource (the accent, say) live.
        private SolidColorBrush? Brush(XElement element)
        {
            var colorText = (element.Attribute("Color")?.Value ?? element.Value).Trim();
            var brush = new SolidColorBrush();
            if (element.Attribute("Opacity")?.Value is { } opacityText)
            {
                if (!double.TryParse(opacityText, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity))
                    throw new ThemeFileException(Line(element), Strings.ThemeErrorBadValue(opacityText, "Opacity"));

                brush.Opacity = opacity;
            }

            if (DynamicResourceKey(colorText) is { } source)
            {
                if (existingType is not null && existingType(source) is null)
                    throw new ThemeFileException(Line(element), Strings.ThemeErrorUnknownKey(source));

                if (colorSource is not null)
                    bindings.Add(colorSource.GetResourceObservable(source, ColorOf).Subscribe(new ColorFollower(brush)));

                return brush;
            }

            if (!Color.TryParse(colorText, out var color))
                return null;

            brush.Color = color;
            return brush;
        }

        private void CheckType(XElement element, string key, object value)
        {
            if (existingType is null)
                return;

            var expected = existingType(key) ?? throw new ThemeFileException(Line(element), Strings.ThemeErrorUnknownKey(key));
            var fits = expected.IsInstanceOfType(value)
                       || (value is IBrush && typeof(IBrush).IsAssignableFrom(expected))
                       || (value is Geometry && typeof(Geometry).IsAssignableFrom(expected));
            if (!fits)
                throw new ThemeFileException(Line(element), Strings.ThemeErrorWrongType(key, expected.Name, element.Name.LocalName));
        }

        private static string? DynamicResourceKey(string text) =>
            text.StartsWith("{DynamicResource ", StringComparison.Ordinal) && text.EndsWith('}')
                ? text["{DynamicResource ".Length..^1].Trim()
                : null;

        private static object? ColorOf(object? resource) => resource switch
        {
            Color color => color,
            ISolidColorBrush brush => brush.Color,
            _ => null,
        };

        // Sets the color as the resource changes, on the thread that changed it (a Bind would post to the UI thread).
        private sealed class ColorFollower(SolidColorBrush brush) : IObserver<object?>
        {
            public void OnNext(object? value)
            {
                if (value is Color color)
                    brush.Color = color;
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }
        }

        private static object? FontWeightOf(string text) =>
            Enum.TryParse<FontWeight>(text, ignoreCase: true, out var weight) ? weight
            : int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? (FontWeight)number
            : null;

        private static object? Try(Func<object> parse)
        {
            try
            {
                return parse();
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException or InvalidOperationException)
            {
                return null;
            }
        }
    }
}
