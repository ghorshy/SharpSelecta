using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SharpSelecta.Tests;

public sealed record ThemeTokenViolation(int Line, string Description);

public static partial class ThemeTokenScanner
{
    private sealed record Assignment(string Property, string Value, int Line);

    // Vector artwork coordinates (not style values): the shapes of the placeholder disc, same as
    // the path data inside an icon SVG. Its colors and stroke thickness are still tokens.
    private static readonly Dictionary<string, string[]> GeometryExemptions = new()
    {
        ["AlbumCoverPlaceholderIcon.axaml"] = ["Width", "Height"],
    };

    private static readonly string[] ColorAttributes =
        ["Foreground", "Background", "BorderBrush", "Fill", "Stroke", "Color", "CurrentColor", "SelectionBrush", "CaretBrush"];

    private static readonly string[] MetricAttributes =
    [
        "Opacity", "FontSize", "FontWeight", "FontStyle", "FontFamily", "CornerRadius", "BorderThickness", "StrokeThickness",
        "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight", "Padding", "Margin", "Spacing", "RowSpacing", "ColumnSpacing",
    ];

    private static readonly string[] StructuralValues = ["0", "Auto", "*", "NaN", "Transparent"];

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex("""(?<=["'>])#[0-9A-Fa-f]{6,8}\b""")]
    private static partial Regex HexColorRegex();

    public static List<ThemeTokenViolation> LiteralColors(string axaml, string fileName)
    {
        var text = WithoutComments(axaml);
        var violations = HexColorRegex().Matches(text).Select(m => new ThemeTokenViolation(Line(text, m.Index), $"hex color {m.Value}")).ToList();
        violations.AddRange(Literals(axaml, fileName, ColorAttributes));
        return violations;
    }

    public static List<ThemeTokenViolation> LiteralMetrics(string axaml, string fileName)
    {
        var violations = Literals(axaml, fileName, MetricAttributes).ToList();
        foreach (var assignment in Assignments(axaml).Where(a => a.Property is "ColumnDefinitions" or "RowDefinitions"))
        {
            foreach (var part in assignment.Value.Split(',').Select(p => p.Trim()))
            {
                if (!Regex.IsMatch(part, @"^(Auto|\d*\*)$"))
                    violations.Add(new ThemeTokenViolation(assignment.Line, $"grid definition \"{part}\""));
            }
        }

        return violations;
    }

    private static IEnumerable<ThemeTokenViolation> Literals(string axaml, string fileName, string[] properties) =>
        Assignments(axaml)
            .Where(a => properties.Contains(a.Property) && !IsAllowed(a.Property, a.Value, fileName))
            .Select(a => new ThemeTokenViolation(a.Line, $"{a.Property} = \"{a.Value}\""));

    private static IEnumerable<Assignment> Assignments(string axaml)
    {
        foreach (var element in XDocument.Parse(axaml, LoadOptions.SetLineInfo).Descendants())
        {
            var line = ((IXmlLineInfo)element).LineNumber;
            if (element.Name.LocalName == "Setter")
            {
                // Value is Setter's content property, so it can be an attribute, a <Setter.Value> element or bare content.
                var content = element.Elements().FirstOrDefault(e => e.Name.LocalName == "Setter.Value") ?? element;
                if (element.Attribute("Property") is { } property && (element.Attribute("Value")?.Value ?? LiteralContent(content)) is { } value)
                    yield return new Assignment(LastSegment(property.Value), value, line);
                continue;
            }

            if (element.Name.LocalName.Contains('.') && LiteralContent(element) is { } elementValue)
                yield return new Assignment(LastSegment(element.Name.LocalName), elementValue, line);

            foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
                yield return new Assignment(LastSegment(attribute.Name.LocalName), attribute.Value, ((IXmlLineInfo)attribute).LineNumber);
        }
    }

    // Bare text, or one attribute-less primitive like <x:Double>12</x:Double>. Object values aren't literals; their attributes get walked.
    private static string? LiteralContent(XElement element)
    {
        var children = element.Elements().ToList();
        var holder = children is [{ HasAttributes: false, HasElements: false } primitive] ? primitive : element;
        return !holder.HasElements && holder.Value.Trim() is { Length: > 0 } text ? text : null;
    }

    private static bool IsAllowed(string property, string value, string fileName) =>
        value.StartsWith('{') || StructuralValues.Contains(value)
        || (GeometryExemptions.TryGetValue(fileName, out var exempt) && exempt.Contains(property));

    private static string LastSegment(string name) => name[(name.LastIndexOfAny(['.', ':']) + 1)..];

    public static string WithoutComments(string axaml) =>
        CommentRegex().Replace(axaml, match => new string(match.Value.Select(c => c == '\n' ? '\n' : ' ').ToArray()));

    public static int Line(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;
}
