using Tmds.DBus;

namespace SharpSelecta.App.Services.SingleInstance;

[DBusInterface("io.github.sharpselecta.SingleInstance")]
public interface ISingleInstanceActivation : IDBusObject
{
    Task ActivateAsync(string[] filePaths);
}
