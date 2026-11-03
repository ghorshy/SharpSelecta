namespace SharpSelecta.App.ViewModels;

public sealed record ShortcutSectionViewModel(string Title, IReadOnlyList<ShortcutRowViewModel> Rows);
