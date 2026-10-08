namespace SharpSelecta.Core.Conversion;

public interface ITrashService
{
    // Throws when the file can't be trashed (the caller then keeps it).
    void MoveToTrash(string path);
}
