namespace SharpSelecta.Core.Updates;

// Self-updating, for installs that support it (the Windows installer). Elsewhere - a portable zip, a Linux
// package - updates come from wherever the app was installed from, and IsSupported is false.
public interface IUpdateService
{
    bool IsSupported { get; }

    string? CurrentVersion { get; }

    // Downloads the newest release if there is a newer one. Returns its version, or null when up to date.
    Task<string?> DownloadLatestAsync(CancellationToken cancellationToken);

    // Exits, installs the downloaded update and starts the new version.
    void RestartToUpdate();

    // Installs the downloaded update once the app has exited, without starting it again.
    void UpdateOnExit();
}
