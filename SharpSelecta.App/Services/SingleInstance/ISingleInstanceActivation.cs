using Tmds.DBus;

namespace SharpSelecta.App.Services.SingleInstance;

[DBusInterface(AppIdentity.SingleInstanceInterface)]
public interface ISingleInstanceActivation : IDBusObject
{
    Task ActivateAsync(string[] filePaths);
}
