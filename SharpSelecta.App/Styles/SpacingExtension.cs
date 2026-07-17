using System;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;

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
        Resolve(Left, Horizontal, serviceProvider),
        Resolve(Top, Vertical, serviceProvider),
        Resolve(Right, Horizontal, serviceProvider),
        Resolve(Bottom, Vertical, serviceProvider));

    private double Resolve(Spacing side, Spacing axis, IServiceProvider serviceProvider)
    {
        var step = side != Spacing.None ? side : axis != Spacing.None ? axis : Uniform;
        return (double)new StaticResourceExtension(SpacingScale.KeyFor(step)).ProvideValue(serviceProvider)!;
    }
}
