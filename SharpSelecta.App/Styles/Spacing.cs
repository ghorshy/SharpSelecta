namespace SharpSelecta.App.Styles;

public enum Spacing
{
    None,
    XXS,
    XS,
    S,
    M,
    L,
}

public static class SpacingScale
{
    public static string KeyFor(Spacing step) => $"Spacing.{step}";
}
