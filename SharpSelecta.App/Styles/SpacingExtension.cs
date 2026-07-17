using System;
using Avalonia;
using Avalonia.Markup.Xaml;

namespace SharpSelecta.App.Styles;

public sealed class SpacingExtension : MarkupExtension
{
    public Spacing Uniform { get; set; }
    public Spacing Horizontal { get; set; }
    public Spacing Vertical { get; set; }
    public Spacing Left { get; set; }
    public Spacing Top { get; set; }
    public Spacing Right { get; set; }
    public Spacing Bottom { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => new Thickness(
        SpacingScale.Resolve(Resolve(Left, Horizontal)),
        SpacingScale.Resolve(Resolve(Top, Vertical)),
        SpacingScale.Resolve(Resolve(Right, Horizontal)),
        SpacingScale.Resolve(Resolve(Bottom, Vertical)));

    private Spacing Resolve(Spacing side, Spacing axis) =>
        side != Spacing.None ? side : axis != Spacing.None ? axis : Uniform;
}
