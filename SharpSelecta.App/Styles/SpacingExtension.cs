using System;
using System.Linq;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
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

    public override object ProvideValue(IServiceProvider serviceProvider) => new MultiBinding
    {
        Converter = new FuncMultiValueConverter<double, Thickness>(values =>
        {
            var v = values.ToArray();
            return new Thickness(v[0], v[1], v[2], v[3]);
        }),
        Bindings =
        {
            ResourceBinding(Resolve(Left, Horizontal), serviceProvider),
            ResourceBinding(Resolve(Top, Vertical), serviceProvider),
            ResourceBinding(Resolve(Right, Horizontal), serviceProvider),
            ResourceBinding(Resolve(Bottom, Vertical), serviceProvider),
        },
    };

    private Spacing Resolve(Spacing side, Spacing axis) =>
        side != Spacing.None ? side : axis != Spacing.None ? axis : Uniform;

    private static BindingBase ResourceBinding(Spacing step, IServiceProvider serviceProvider) =>
        new DynamicResourceExtension(SpacingScale.KeyFor(step)).ProvideValue(serviceProvider);
}
