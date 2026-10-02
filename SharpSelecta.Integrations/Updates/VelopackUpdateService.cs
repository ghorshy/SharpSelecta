using SharpSelecta.Core.Updates;
using Velopack;
using Velopack.Sources;

namespace SharpSelecta.Integrations.Updates;

// Releases are published to GitHub by the release workflow (vpk pack + vpk upload).
public sealed class VelopackUpdateService : IUpdateService
{
    private const string RepositoryUrl = "https://github.com/ghorshy/SharpSelecta";

    private readonly UpdateManager? _manager;
    private UpdateInfo? _downloaded;

    public VelopackUpdateService()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var manager = new UpdateManager(new GithubSource(RepositoryUrl, null, false));
        if (manager.IsInstalled && !manager.IsPortable)
        {
            _manager = manager;
        }
    }

    public bool IsSupported => _manager is not null;

    public string? CurrentVersion => _manager?.CurrentVersion?.ToString();

    public async Task<string?> DownloadLatestAsync(CancellationToken cancellationToken)
    {
        if (_manager is null)
            return null;

        var update = await _manager.CheckForUpdatesAsync();
        if (update is null)
            return null;

        await _manager.DownloadUpdatesAsync(update, null, cancellationToken);
        _downloaded = update;
        return update.TargetFullRelease.Version.ToString();
    }

    public void RestartToUpdate()
    {
        if (_manager is not null && _downloaded is not null)
        {
            _manager.ApplyUpdatesAndRestart(_downloaded.TargetFullRelease);
        }
    }

    public void UpdateOnExit()
    {
        if (_manager is not null && _downloaded is not null)
        {
            _manager.WaitExitThenApplyUpdates(_downloaded.TargetFullRelease, silent: true, restart: false);
        }
    }
}
