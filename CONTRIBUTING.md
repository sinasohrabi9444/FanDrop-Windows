# Contributing to FanDrop for Windows

Thanks for helping out! Issues and pull requests are welcome in **English or Persian (فارسی)**.

## Reporting a bug

[Open a bug report](https://github.com/sinasohrabi9444/FanDrop-Windows/issues/new?template=bug_report.yml) and include:

- Windows version (`winver`)
- What you dragged (file type / where from), which key, which blade
- The relevant part of `%TEMP%\fandrop.log` — run with `FANDROP_DEBUG=1` for detail

## Development setup

No IDE or SDK needed — just Windows 10/11.

```bat
git clone https://github.com/sinasohrabi9444/FanDrop-Windows.git
cd FanDrop-Windows
build.cmd
```

Handy while iterating:

```bat
FanDrop.exe --run Tools "Resize 1280" photo.jpg      :: run an action with no UI
FanDrop.exe --snapshot fan.png Tools photo.jpg 2     :: render the fan to a PNG
```

The architecture tour and a step-by-step "add a new blade" walkthrough are in
[docs/fa/07-build-and-develop.md](docs/fa/07-build-and-develop.md).

## Guidelines

- **Stay dependency-free.** The project builds with the in-box `csc.exe` against .NET Framework 4.8. Don't add NuGet packages, newer C# language features the in-box compiler (C# 5) can't handle, or an installer.
- **Never touch the original file.** Every output goes to the output folder with a readable suffix (`name (compressed).jpg`).
- **External tools are optional.** Anything that needs ffmpeg / Word / exiftool must fail with a clear notification, never a crash.
- **Keep the hook thread fast.** `DragWatcher`'s low-level hook callback must never block.
- Match the existing code style (4 spaces, braces on new lines, CRLF).
- If you change what a blade does, update the reference table in the README and `docs/fa/03-reference.md`.
- If you change the fan's look, regenerate the images in `docs/images` with `--snapshot`.

## Pull requests

1. Fork and branch from `main`.
2. `build.cmd` must succeed — CI runs it on every PR.
3. Describe what you changed and how you tested it (screenshots for UI changes).
