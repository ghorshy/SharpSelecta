# SharpSelecta

Cross-platform, open-source music player built on .NET 10 + Avalonia.

## Status

Early stage, but already usable day to day:

- Point it at one or more folders and it scans your library (MP3, FLAC, WAV, M4A — including AAC and ALAC)
- Browse as a sortable, column-configurable table, or as an album grid with cover art (zoomable, sortable by title/artist/year)
- Click an album tile to expand its tracklist in place; double-click to queue and play the whole album
- Double-click any cover art for a full-resolution preview
- Queue with drag-to-reorder, play-next, and repeat modes (off/all/one)
- Play/pause, seek, volume (linear or logarithmic), and a toggle between elapsed and remaining time
- Playback device selection, and the queue/current track/volume all persist across restarts
- On Linux, integrates with playerctl and desktop media-key bindings (MPRIS)

## Requirements

- .NET 10 SDK

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
- [ATL.NET](https://github.com/Zeugma440/atldotnet) — reading audio tags and properties
- [Serilog](https://github.com/serilog/serilog) — logging
- [Tmds.DBus](https://github.com/tmds/Tmds.DBus) — MPRIS/playerctl integration on Linux
- [TUnit](https://github.com/thomhurst/TUnit) + [NSubstitute](https://github.com/nsubstitute/NSubstitute) — testing

## TODO

- Playlists
- Tag editing
- Auto-DJ / crossfade
- Equalizer
- Discord Rich Presence
- File extension converter (WAV->FLAC, etc...)

## License

MIT — see [LICENSE](LICENSE).
