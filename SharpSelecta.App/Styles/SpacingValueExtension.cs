using System;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace SharpSelecta.App.Styles;

public sealed class SpacingValueExtension(Spacing step) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new DynamicResourceExtension(SpacingScale.KeyFor(step)).ProvideValue(serviceProvider);
}
