# Vidnelo 1.0

A Windows desktop app for downloading video and audio, powered by [yt-dlp](https://github.com/yt-dlp/yt-dlp).

## Getting started

Run **Vidnelo.exe**, paste a link, choose a format and quality, select a destination folder, and start downloading. Keep the `tools/` folder alongside the executable.

The interface supports 15 languages, including English and Polish. Additional instructions in Polish are available in `INSTRUKCJA.txt`.

## Project layout

- `Vidnelo.exe` — the compiled application; distributed separately from the source code.
- `tools/` — yt-dlp, FFmpeg, FFprobe, and Deno, with source and license information.
- `assets/` — icons, original logo artwork, and artwork descriptions. The desktop shortcut uses an icon from this folder.
- `src/` — application code, WPF interface, and translations.
- `tests/` — checks and small local media fixtures.
- `scripts/` — tools for dependency setup, icon preparation, and desktop shortcut creation.
- `docs/` — interface verification report.

## Building

Run `./build.ps1` in PowerShell. Building requires Windows and the .NET Framework compiler.

The resulting executable is placed alongside `tools/`. Close Vidnelo before replacing its executable, or build a separate copy with:

```powershell
./build.ps1 -OutputName Vidnelo.dev.exe
```

Intermediate build files are stored in `.build/` and can be removed after compilation.

## Verification

- `python tests/check-translations.py` checks the translation catalog.
- `Vidnelo.exe --render-preview test-artifacts/ui` checks the interface and saves previews.
- `Vidnelo.exe --localization-preview test-artifacts/languages` checks localized layouts.
- `python tests/test-server.py` serves local test media at `127.0.0.1:18769`. With the server running, use `Vidnelo.exe --ui-test test-artifacts/flow` to check the download flow.

Compile the download engine checks in `tests/` together with `src/DownloadEngine.cs` using the .NET Framework `csc.exe` compiler. The progress model checks use `src/ProgressModel.cs`. Place engine test executables in the application folder so they can locate `tools/`.

Update checks use `src/AppUpdates.cs`, `src/Localization.cs`, the embedded translation catalog, and the WPF, System.Xaml, and System.Web.Extensions references.

Tests use local media fixtures and the `test-artifacts/` directory. Generated `.build/`, `.downloads/`, and `test-artifacts/` folders, along with temporary test executables, can be removed after testing.

User settings are stored separately in `%LOCALAPPDATA%/LinkDownloader`.

## Updates

Releases are published on [GitHub Releases](https://github.com/cavxvac/Vidnelo/releases).

Vidnelo checks for the latest stable release in the background at startup. When a newer version is available, it highlights the update button without interrupting downloads. The Updates window displays release notes and lets users open the release page, postpone the update, or skip that version. Manual checks also show skipped versions.

Connection failures at startup are silent. Manual checks display a message if the update check fails.

Application updates currently require manual installation: download the new version, close Vidnelo, and replace the application executable. User settings remain in `%LOCALAPPDATA%/LinkDownloader`. A separate **Update yt-dlp** button updates the download engine.

### Publishing a release

Use version tags such as `v1.0`, `v1.1`, or `v1.2.1`. The version in `src/AssemblyInfo.cs` must match the release tag. Attach the compiled application or distribution package to the release; GitHub's automatically generated source archives are not ready-to-run packages.

Drafts and prereleases are excluded from update checks. Until the first release is published, the app displays **No published releases yet.**

## Credits

Created by [cavxvac](https://github.com/cavxvac) · [gerardbinder.com](https://gerardbinder.com)

Vidnelo is an independent graphical client for [yt-dlp](https://github.com/yt-dlp/yt-dlp). Downloads are handled locally by yt-dlp and its supporting tools. See `tools/` for information about bundled dependencies and their licenses.
