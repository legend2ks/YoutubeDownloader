<div align="center">
<img src="https://user-images.githubusercontent.com/16824470/226195878-47e26931-87a4-4145-b208-6f336eca55f2.png"/>

[![Chat](https://img.shields.io/badge/Chat-on%20Telegram-blue)](https://t.me/+5Kma9lxB0z40Y2M0)
[![Release](https://img.shields.io/github/v/release/legend2ks/YoutubeDownloader?label=Release&color=2ea043)](https://github.com/legend2ks/YoutubeDownloader/releases)
[![Downloads](https://img.shields.io/github/downloads/legend2ks/YoutubeDownloader/total?label=Downloads&color=2ea043)](https://github.com/legend2ks/YoutubeDownloader/releases)
[![Donate](https://img.shields.io/badge/_-Donate-red.svg?logo=undertale&logoColor=ff3333&labelColor=ffcccc&color=ff3333)](DONATE.md)
</div>

An open-source YouTube video downloader that allows you to easily download videos from YouTube in all available original qualities. It uses yt-dlp, ffmpeg and aria2 under the hood.

### Features

* Easy selection of video/audio/container formats
* Download videos from playlists
* Listing and downloading channel videos
* Embedding chapter markers
* Fast, multithreaded downloading

### Screenshots

<img src="https://github.com/legend2ks/YoutubeDownloader/assets/16824470/bc5afcad-1d5a-48fb-a727-49c95f068de5" />
<div align="center">
  <img src="https://github.com/legend2ks/YoutubeDownloader/assets/16824470/a464935d-3b5e-47cc-bf83-37195f2c8e9c" height="170" />
  <img src="https://github.com/legend2ks/YoutubeDownloader/assets/16824470/52958fb7-09ce-4921-a571-4291aee4aa47" height="170" />
  <img src="https://github.com/legend2ks/YoutubeDownloader/assets/16824470/62379871-0d96-49cd-b61f-54058befa7d2" height="170" />
</div>

## Installing

#### Requirements:

- Microsoft Windows 7+ (x64)
- [.NET 8.0 Desktop Runtime](https://aka.ms/dotnet-core-applaunch?framework=Microsoft.NETCore.App&framework_version=8.0.0&arch=x64&rid=win-x64&gui=true)

[Releases](https://github.com/legend2ks/YoutubeDownloader/releases)

## Building from source code

Clone the repository (including submodules):

```
git clone --recursive https://github.com/legend2ks/YoutubeDownloader
```

Build the project using the build script, or:

```
cd YoutubeDownloader
dotnet publish "YoutubeApp/YoutubeApp.csproj" -c "Release" -o "Publish/app" -p:DebugType=None -p:PublishSingleFile=true --self-contained false
```

Download the project dependencies and put them in `Publish/app/utils`:

- "yt-dlp.exe" from https://github.com/yt-dlp/yt-dlp/releases
- "aria2c.exe" from https://github.com/aria2/aria2/releases (`aria2-1.37.0-win-64bit-build1.zip`).
- "ffmpeg.exe" from https://github.com/BtbN/FFmpeg-Builds/releases (`ffmpeg-master-latest-win64-lgpl.zip`)

## Roadmap

* [x] Channels section
* [ ] Audio-only formats
* [ ] Scheduler
* [ ] Subtitle support
* [ ] Multi-platform support
* [ ] Browser Integration

## ❤ Support

This project is free and open source, if you like my work, please consider:
* Star this project on GitHub
* [Donate](DONATE.md)

Your support helps keep the project going.

## Tech Stack

<table>
  <tr>
    <td>
      <img src="https://github.com/legend2ks/YoutubeDownloader/assets/16824470/1634a771-5000-48d2-b078-b443243cba6c" height="35" />
    </td>
    <td>
      Avalonia UI
    </td>
  </tr>
  <tr>
    <td>
      <img src="https://github.com/legend2ks/YoutubeDownloader/assets/16824470/6862f28d-8547-49a1-b631-32157c0d17e4" height="35" />
    </td>
    <td>
      C# / .NET
    </td>
  </tr>
</table>
