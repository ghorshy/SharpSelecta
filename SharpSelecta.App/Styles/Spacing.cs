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
    public static double Resolve(Spacing step) => step switch
    {
        Spacing.XXS => 2,
        Spacing.XS => 4,
        Spacing.S => 6,
        Spacing.M => 10,
        Spacing.L => 16,
        _ => 0,
    };
}
