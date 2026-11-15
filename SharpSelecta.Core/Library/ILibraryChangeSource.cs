namespace SharpSelecta.Core.Library;

// OldPath is set for a rename. IsDirectory comes from the source: a deleted path can no longer be checked.
public sealed record LibraryChange(string Path, bool IsDirectory, string? OldPath = null);

public interface ILibraryChangeSource
{
    event EventHandler<LibraryChange>? Changed;

    // Events were lost (buffer overflow, watcher error) under this root.
    event EventHandler<string>? Failed;

    // Replaces the set of watched roots; an empty set stops watching.
    void Watch(IReadOnlyCollection<string> roots);

    void Recreate(string root);
}
