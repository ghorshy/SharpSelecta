namespace SharpSelecta.App.Styles;

public enum Spacing
{
    None,
    // ReSharper disable once InconsistentNaming
    XXS,
    // ReSharper disable once InconsistentNaming
    XS,
    S,
    M,
    L,
    // ReSharper disable once InconsistentNaming
    XL,
}

public static class SpacingScale
{
    public static string KeyFor(Spacing step) => $"Spacing.{step}";
}
