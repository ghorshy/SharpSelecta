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
                (AvaloniaNamespace, "LinearGradientBrush" or "RadialGradientBrush" or "ConicGradientBrush") => Gradient(element),
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

        private SolidColorBrush Brush(XElement element)
        {
            var brush = new SolidColorBrush();
            if (element.Attribute("Opacity")?.Value is { } opacityText)
                brush.Opacity = Number(element, opacityText, "Opacity");

            var colorText = (element.Attribute("Color")?.Value ?? element.Value).Trim();
            return SetColor(element, colorText, color => brush.Color = color)
                ? brush
                : throw new ThemeFileException(Line(element), Strings.ThemeErrorBadValue(colorText, "Color"));
        }

        // Linear, radial or conic, with stops directly inside or under <X.GradientStops>.
        private GradientBrush Gradient(XElement element)
        {
            var kind = element.Name.LocalName;
            GradientBrush brush = kind switch
            {
                "LinearGradientBrush" => new LinearGradientBrush(),
                "RadialGradientBrush" => new RadialGradientBrush(),
                _ => new ConicGradientBrush(),
            };

            foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.Namespace != XamlNamespace))
            {
                var text = attribute.Value;
                switch (brush, attribute.Name.LocalName)
                {
                    case (_, "Opacity"):
                        brush.Opacity = Number(element, text, "Opacity");
                        break;
                    case (_, "SpreadMethod"):
                        brush.SpreadMethod = Enum.TryParse<GradientSpreadMethod>(text, ignoreCase: true, out var spread) && Enum.IsDefined(spread)
                            ? spread
                            : throw new ThemeFileException(Line(element), Strings.ThemeErrorBadValue(text, "SpreadMethod"));
                        break;
                    case (LinearGradientBrush linear, "StartPoint"):
                        linear.StartPoint = Point(element, text);
                        break;
                    case (LinearGradientBrush linear, "EndPoint"):
                        linear.EndPoint = Point(element, text);
                        break;
                    case (RadialGradientBrush radial, "Center"):
                        radial.Center = Point(element, text);
                        break;
                    case (RadialGradientBrush radial, "GradientOrigin"):
                        radial.GradientOrigin = Point(element, text);
                        break;
                    case (RadialGradientBrush radial, "RadiusX"):
                        radial.RadiusX = Scalar(element, text);
                        break;
                    case (RadialGradientBrush radial, "RadiusY"):
                        radial.RadiusY = Scalar(element, text);
                        break;
                    case (ConicGradientBrush conic, "Center"):
                        conic.Center = Point(element, text);
                        break;
                    case (ConicGradientBrush conic, "Angle"):
                        conic.Angle = Number(element, text, "Angle");
                        break;
                    default:
                        throw new ThemeFileException(Line(element), Strings.ThemeErrorUnsupportedAttribute(attribute.Name.LocalName, kind));
                }
            }

            foreach (var stop in GradientStops(element, kind))
            {
                brush.GradientStops.Add(stop);
            }

            return brush;
        }

        private IEnumerable<GradientStop> GradientStops(XElement brush, string kind)
        {
            foreach (var child in brush.Elements())
            {
                var stops = child.Name.LocalName == $"{kind}.GradientStops"
                    ? child.Elements().SelectMany(e => e.Name.LocalName == "GradientStops" ? e.Elements() : [e])
                    : [child];
                foreach (var element in stops)
                {
                    yield return element.Name == XName.Get("GradientStop", AvaloniaNamespace)
                        ? Stop(element)
                        : throw new ThemeFileException(Line(element), Strings.ThemeErrorUnsupportedElement(element.Name.LocalName));
                }
            }
        }

        private GradientStop Stop(XElement element)
        {
            var stop = new GradientStop();
            if (element.Attribute("Offset")?.Value is { } offsetText)
                stop.Offset = Number(element, offsetText, "Offset");

            var colorText = (element.Attribute("Color")?.Value ?? "").Trim();
            return SetColor(element, colorText, color => stop.Color = color)
                ? stop
                : throw new ThemeFileException(Line(element), Strings.ThemeErrorBadValue(colorText, "Color"));
        }

        // A literal color, or {DynamicResource Key} that follows that resource (the accent, say) live.
        private bool SetColor(XElement element, string text, Action<Color> set)
        {
            if (DynamicResourceKey(text) is { } source)
            {
                if (existingType is not null && existingType(source) is null)
                    throw new ThemeFileException(Line(element), Strings.ThemeErrorUnknownKey(source));

                if (colorSource is not null)
                    bindings.Add(colorSource.GetResourceObservable(source, ColorOf).Subscribe(new ColorFollower(set)));

                return true;
            }

            if (!Color.TryParse(text, out var color))
                return false;

            set(color);
            return true;
        }

        private static double Number(XElement element, string text, string what) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw new ThemeFileException(Line(element), Strings.ThemeErrorBadValue(text, what));

        private static RelativePoint Point(XElement element, string text) =>
            Try(() => RelativePoint.Parse(text)) as RelativePoint?
            ?? throw new ThemeFileException(Line(element), Strings.ThemeErrorBadValue(text, "RelativePoint"));

        private static RelativeScalar Scalar(XElement element, string text) =>
            Try(() => RelativeScalar.Parse(text)) as RelativeScalar?
            ?? throw new ThemeFileException(Line(element), Strings.ThemeErrorBadValue(text, "RelativeScalar"));

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
        private sealed class ColorFollower(Action<Color> set) : IObserver<object?>
        {
            public void OnNext(object? value)
            {
                if (value is Color color)
                    set(color);
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
