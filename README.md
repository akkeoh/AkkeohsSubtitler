# Akkeoh's Subtitler for Vegas Pro

Automatic speech-to-text subtitles for **VEGAS Pro** using [whisper.cpp](https://github.com/ggml-org/whisper.cpp). Select clips on the timeline, run the plugin, and subtitle blocks are placed on a new video track as **Titles & Text** events.

## What you get

| Piece | Role |
|-------|------|
| `AkkeohsSubtitlesSetup.exe` | Single-file installer (embeds plugin DLLs; run as admin) |
| Extension + Core | Installed into VEGAS Application Extensions |
| whisper.cpp + FFmpeg + models | Downloaded during setup into the product install folder |

## Workflow

1. Select one or more events on the timeline.
2. Open **View → Extensions → Akkeoh's Subtitler**.
3. Choose model / language → **Generate subtitles**.
4. The plugin extracts audio with ffmpeg, runs whisper.cpp, and places timed Titles & Text blocks.

## Build

Requirements:

- Windows x64
- Visual Studio 2019/2022 (or Build Tools) with **.NET Framework 4.7.2** targeting pack
- Optional but recommended: VEGAS Pro installed (provides the real `ScriptPortal.Vegas.dll`)

```powershell
# From repo root
.\scripts\build.ps1
```

Output: `dist\AkkeohsSubtitlesSetup.exe` (single file to ship).

Without VEGAS installed, the build uses a **compile stub** for `ScriptPortal.Vegas.dll`. Before shipping, rebuild with the real DLL from your VEGAS install (for example `C:\Program Files\VEGAS\VEGAS Pro 15.0\ScriptPortal.Vegas.dll`).

## Install

1. Run `AkkeohsSubtitlesSetup.exe` as administrator.
2. Pick your VEGAS version and models (Fast / Fast English are required).
3. Accept terms and install (whisper.cpp, FFmpeg, and models download automatically).
4. Restart VEGAS → **View → Extensions → Akkeoh's Subtitler**.

Files are installed to:

- `%ProgramFiles%\AkkeohsSubtitlesForVegas\` (or Program Files (x86)) — binaries + models
- `%ProgramData%\Vegas Pro\<version>\Application Extensions\` — plugin DLLs
- `%LocalAppData%\AkkeohsSubtitlesForVegas\settings.json` — settings

Uninstall via Apps & Features or `akkeohs_subtitler_uninstall.exe`.

## Project layout

```
src\AkkeohsVegas.Core        Transcription pipeline (no Vegas dependency)
src\AkkeohsVegas.Extension   Application Extension + dock UI + timeline placement
src\AkkeohsVegas.Installer   AkkeohsSubtitlesSetup.exe (embeds Core + Extension)
scripts\                     build.ps1, ensure-vegas-reference.ps1
lib\stubs\                   ScriptPortal.Vegas compile stub
third_party\                 Optional offline whisper/ffmpeg/models cache
```

## Notes / limits

- VEGAS Pro scripting is Pro-only (not Movie Studio).
- Generated Titles & Text media (no file path) cannot be transcribed — use file-based clips.
- Timing respects `Take.Offset` and event length.
- `ScriptPortal.Vegas.dll` is not redistributed; it comes with VEGAS Pro.

## License

Plugin code in this repository: use freely for your projects.  
whisper.cpp, FFmpeg, and model weights retain their own licenses.
