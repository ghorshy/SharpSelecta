using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

internal sealed class FakeLibraryChangeSource : ILibraryChangeSource
{
    public event EventHandler<LibraryChange>? Changed;

    public event EventHandler<string>? Failed;

    public IReadOnlyCollection<string> Roots { get; private set; } = [];

    public List<string> Recreated { get; } = [];

    public void Watch(IReadOnlyCollection<string> roots) => Roots = roots;

    public void Recreate(string root) => Recreated.Add(root);

    public void Raise(LibraryChange change) => Changed?.Invoke(this, change);

    public void Fail(string root) => Failed?.Invoke(this, root);
}
