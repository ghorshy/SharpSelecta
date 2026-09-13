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
}

public static class SpacingScale
{
    public static string KeyFor(Spacing step) => $"Spacing.{step}";
}
