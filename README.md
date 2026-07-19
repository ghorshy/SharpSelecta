# SharpSelecta

Cross-platform, open-source music player built on .NET 10 + Avalonia.

## Status

Early stage. You can point it at a folder, browse your library in a sortable table, queue up tracks and reorder them, and see what's playing with its artwork.

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
- [ATL.NET](https://github.com/Zeugma440/atldotnet) — reading audio tags and properties
- [Serilog](https://github.com/serilog/serilog) — logging
- [TUnit](https://github.com/thomhurst/TUnit) + [NSubstitute](https://github.com/nsubstitute/NSubstitute) — testing

## TODO

- Playlists
- Tag editing
- Auto-DJ / crossfade
- Equalizer
- Discord Rich Presence
- Grid/tile Library view with cover art
- File extension converter (WAV->FLAC, etc...)

## License

MIT — see [LICENSE](LICENSE).
