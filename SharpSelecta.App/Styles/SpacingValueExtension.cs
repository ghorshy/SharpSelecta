using System;
using Avalonia.Markup.Xaml;

namespace SharpSelecta.App.Styles;

public sealed class SpacingValueExtension(Spacing step) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => SpacingScale.Resolve(step);
}
