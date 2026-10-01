namespace SharpSelecta.Core.Conversion;

// Moves a file to the OS trash / recycle bin rather than deleting it, so it stays recoverable.
public interface ITrashService
{
    // Throws when the file can't be trashed (the caller then keeps it).
    void MoveToTrash(string path);
}
