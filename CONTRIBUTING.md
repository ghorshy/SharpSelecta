# Contributing

Thanks for wanting to help. These rules hold for everyone, people and AI assistants alike.

## Report a bug or ask for something

- Search the open issues first. If one already covers it, add to that one: one problem is one issue.
- For a bug, say what you did, what you expected and what happened instead, plus your OS and the SharpSelecta version.
- Attach the log from around the time it happened. It's in `%LocalAppData%\SharpSelecta\logs` on Windows and `~/.local/share/SharpSelecta/logs` on Linux, one file per day.
- Leave out anything private: your music paths, API keys (such as a fanart.tv key) and personal names.

## Before you start

- For anything bigger than a small fix, open an issue first and say what you plan to do. That way nobody spends a weekend on something that won't be merged.
- Read the code your change touches before changing it.

## Make a change

- The projects depend one way only: `SharpSelecta.App` uses `SharpSelecta.Audio`, `SharpSelecta.Integrations` and `SharpSelecta.Core`, and those two use `SharpSelecta.Core`. Never the reverse.
- Only `SharpSelecta.Audio` talks to OwnAudioSharp. The rest of the app goes through `IAudioEngine`.
- Only `SharpSelecta.Integrations` talks to outside services (cover art sites, Discord, FFmpeg, updates). Their interfaces live in Core.
- `SharpSelecta.App` is the Avalonia UI, written as MVVM with CommunityToolkit.Mvvm. Logic that can live in a view model or in Core doesn't go into code-behind.
- Every piece of UI text goes into `SharpSelecta.App/Resources/Strings.resx`, in English. No text is written straight into XAML or code.
- Every color, size, spacing, font size and opacity comes from the theme tokens in `SharpSelecta.App/Styles`. Tests fail on a literal value in a view.
- Calls that block (loading audio, switching the output device, reading a whole folder) run off the UI thread.
- Log through `ILogger<T>`, never `Console.WriteLine`.
- Never hide an error. Catch only what you can handle, and log the rest.

## Test it

A pull request is merged only once it has been tested. That means all of the following:

1. New behavior and every bug fix come with tests. A fix starts with a test that fails without it.
2. Tests use TUnit and NSubstitute. Mock only what is slow, random or needs hardware (the audio engine, file dialogs, the network). Use the real thing where it's fast and predictable, such as SQLite and the tag reader on the fixture files.
3. `dotnet build SharpSelecta.slnx` builds without warnings, and `dotnet test` passes on your machine.
4. CI passes on Linux and on Windows. A red `main` is fixed before anything else.
5. You ran the app and tried the change by hand. Unit tests don't show how the UI looks or feels, so a change the user can see or hear has to be tried in the running app.
6. The pull request says how you tested it: which tests you added, and what you clicked through by hand, on which OS.

Tests that need FFmpeg skip themselves when it isn't installed. Install it if your change touches conversion.

## Commit

- Start the subject with the kind of change, the way the history does: `feat:`, `fix:`, `refactor:`, `perf:`, `test:`, `docs:`, `build:` or `ci:`. Then say in plain words what changes for the user, for example `fix: closing an expanded album also deselects its tile`.
- The body says why the change was made. A fix names the cause it removes.
- Keep each commit to one change, and keep it building with its tests passing.

## Open a pull request

- One pull request is one change. Unrelated fixes go in their own pull requests.
- Link the issue it solves.
- For a change to the UI, add a screenshot or a short recording.
- Keep the branch up to date with `main`, and answer review comments with a new commit rather than a force-push, so the review can follow what changed.
