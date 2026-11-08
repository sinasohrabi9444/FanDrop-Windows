# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [1.0.0] — 2026-11-08

First public release.

### Added
- Radial drop fan that opens mid-drag on **Shift** (Convert), **Ctrl** (Tools) and **Alt** (Actions).
- Image: PNG / JPG / WEBP / HEIC / TIFF / PDF conversion; resize, compress, strip metadata, rotate, grayscale.
- Video: MP4 / MOV / GIF / WEBM / MP3; compress, strip metadata, mute, first 10 s, grab frame (ffmpeg).
- Audio: MP3 / M4A / WAV / FLAC / OGG; strip metadata, normalize, first 30 s, mono, compress (ffmpeg).
- Documents: TXT / RTF / HTML / DOCX via Word, LibreOffice or a built-in converter; copy text, word count.
- PDF: every page to PNG / JPG.
- ZIP and SHA-256 for any file or folder; multi-file drops make one `Archive.zip`.
- Actions: copy path, reveal, open, Quick Look-style preview window, move to Recycle Bin.
- Virtual-file drops from browsers and Outlook.
- Tray icon with output folder, log and Start-with-Windows toggle.
- `--run` and `--snapshot` developer hooks; `FANDROP_OUT`, `FANDROP_DEBUG`, `FANDROP_NO_OFFICE`.

[1.0.0]: https://github.com/sinasohrabi9444/FanDrop-Windows/releases/tag/v1.0.0
