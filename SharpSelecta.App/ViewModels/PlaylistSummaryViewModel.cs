namespace SharpSelecta.App.ViewModels;

public sealed class PlaylistSummaryViewModel(string id, string name)
{
    public string Id { get; } = id;

    public string Name { get; set; } = name;
}
