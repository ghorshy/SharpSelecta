# SharpSelecta

Cross-platform, open-source music player built on .NET 10 + Avalonia.

## Status

Early stage, but already usable day to day:

- Point it at one or more folders and it scans your library (MP3, FLAC, WAV, AIFF, Ogg Vorbis, M4A — including AAC and ALAC), then remembers it in a local index so later launches start instantly and only re-check what actually changed on disk
- Browse as a sortable, column-configurable table, or as an album grid with cover art (zoomable, sortable by title/artist/year)
- Click an album tile to expand its tracklist in place; double-click to queue and play the whole album
- A Recently Added section showing the newest additions to your library first
- Fuzzy search across track title, artist, and album, filtering both views live as you type
- Double-click any cover art for a full-resolution preview
- Playlists you can create, rename, reorder by dragging, and import from or export to M3U/M3U8
- Queue with drag-to-reorder, play-next, and repeat modes (off/all/one)
- Auto DJ: a toggle on the queue that keeps it topped up with random tracks from the playlist you started playing from (or the whole library), without repeating anything until the pool is used up
- Play/pause, seek (with an optional waveform seek bar), volume (linear or logarithmic), and a toggle between elapsed and remaining time
- 10-band graphic equalizer, with named presets or manual per-band gain
- Playback device selection, and the queue/current track/volume all persist across restarts
- Rebindable keyboard shortcuts, plus light, dark, or custom themes
- Edit tags (title, artist, album artist, album, genre, year, track number, comments) and cover art from the Properties window (Alt+Enter) for one track, a selection of tracks, or a whole album, with the cover stored embedded in the files or as a separate cover.jpg. Tracks can have several artists, plus remixer, composer, conductor and lyricist credits
- Find a cover online for an album from Apple Music, Deezer, Cover Art Archive or fanart.tv (fanart.tv needs your own free API key), compare them side by side, and use the one you like
- Jump from any track or album straight to its file in your file manager
- On Linux, integrates with playerctl and desktop media-key bindings (MPRIS)
- Optional Discord Rich Presence: shows the playing track as a "Listening to" status with a progress bar (off by default, enable it in Settings → Integrations)
- Convert tracks to MP3, FLAC, M4A (ALAC or AAC), Ogg Vorbis, WAV or AIFF from the Properties window's Convert tab, singly or in batches. Tags and embedded cover art carry over (WAV keeps only title, artist and album), a lossy file is never converted to a lossless format or to a higher bitrate, and the original is kept unless you untick "Keep original file" - then it goes to the trash

## Requirements

- .NET 10 SDK
- Optional: [FFmpeg](https://ffmpeg.org/) on your PATH, needed only for the Convert tab

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
- [Microsoft.Data.Sqlite](https://github.com/dotnet/efcore) — local library index
- [Serilog](https://github.com/serilog/serilog) — logging
- [Tmds.DBus](https://github.com/tmds/Tmds.DBus) — MPRIS/playerctl integration on Linux
- [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia) — SVG icon rendering
- [Tabler Icons](https://tabler.io/icons) — toolbar icon set
- [TUnit](https://github.com/thomhurst/TUnit) + [NSubstitute](https://github.com/nsubstitute/NSubstitute) — testing

## TODO

- Crossfade

## License

MIT — see [LICENSE](LICENSE).
