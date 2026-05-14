# ChuChartManager

[简体中文](README.md) | [English](README.en.md)

A chart and resource management tool for CHUNITHM.

## Features

### Chart Management

- Browse local chart list, sort by ID / name, filter by genre / difficulty
- Edit chart metadata (title, artist, genre, level, notes designer)
- Import / export charts (C2S / UGC format conversion)
- Import jacket art, export BGM as MP3
- Batch operations (edit properties, export jackets / audio)

### Resource Management

- Create custom resources (trophies, name plates, characters, map icons, costumes, system voices)
- Resource browser (trophies, name plates, frames, characters, costumes, system voices, stages)
- Resource ID conflict detection

### Events & Maps

- Browse and edit events / maps, create custom events and maps
- Import / replace map background textures and event info images

### Other

- Course editor (Dan-i Nintei)
- Login bonus editor
- DDS texture extraction (from AFB / SVO files)
- E-mote model WebGL preview
- Option directory management (create, import, delete, custom chart marking)
- Multi-language (中文 / English / 日本語)
- Remote mode (LAN access)

## Build

Requirements:
- .NET 10 SDK
- Node.js 18+, pnpm
- .NET Framework 4.8.1 Targeting Pack (required by SonicAudioTools)

```bash
git submodule update --init --recursive

cd ChuChartManager/Front
pnpm install
pnpm build

cd ../..
dotnet build ChuChartManager.slnx

# Build FreeMote toolchain
dotnet build FreeMote/FreeMote.Tools.PsbDecompile -c Release
dotnet build FreeMote/FreeMote.Tools.PsBuild -c Release
```

## Credits

- [MuNET-UI](https://github.com/MuNET-OSS/MuNET-UI) — UI component library
- [FreeMote](https://github.com/UlyssesWu/FreeMote) — E-mote PSB toolchain
- [DDSExtractor](https://github.com/XNTech/DDSExtractor) — DDS texture extraction library
