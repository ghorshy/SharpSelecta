# SharpSelecta

Cross-platform, open-source music player built on .NET 10 + Avalonia.

## Status

Early stage — currently a single-track player (pick a file, play/pause). Playlists, a track library, tag editing, auto-DJ/crossfade, and an equalizer are planned next.

## Requirements

- .NET 10 SDK
- `ffmpeg` on `PATH` — used as a fallback for formats OwnAudioSharp can't decode natively (currently: ALAC)

## Build & run

```sh
dotnet build SharpSelecta.slnx
dotnet run --project SharpSelecta.App
```

## Tests

```sh
dotnet test
```

## License

MIT — see [LICENSE](LICENSE).
