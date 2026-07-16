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

## Built with

- [Avalonia](https://github.com/AvaloniaUI/Avalonia) — cross-platform UI
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM (observable properties, commands)
- [OwnAudioSharp](https://github.com/ModernMube/OwnAudioSharp) — audio engine (decode, mix, output)
- [FFmpeg](https://git.ffmpeg.org/ffmpeg.git) — fallback decoder for formats OwnAudioSharp can't handle natively
- [TagLibSharp](https://github.com/mono/taglib-sharp) — reading/writing audio tags
- [Serilog](https://github.com/serilog/serilog) — logging
- [TUnit](https://github.com/thomhurst/TUnit) + [NSubstitute](https://github.com/nsubstitute/NSubstitute) — testing

## License

MIT — see [LICENSE](LICENSE).
