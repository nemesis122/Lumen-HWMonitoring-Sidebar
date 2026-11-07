# Lumen 4.0.0 – Every Widget, Every Design

A portable hardware-monitoring sidebar for Windows 10 and 11 with **36 widget designs**. Every widget – hardware and info alike – takes the look of the design you choose. Use it vertically at either side of your desktop or horizontally at the top or bottom, in dark or light.

**[Download Lumen 4.0.0](https://github.com/nemesis122/Lumen-HWMonitoring-Sidebar/releases/tag/v4.0.0)** · [All designs](docs/style-gallery.md) · [Release notes](docs/release-notes/v4.0.0.md)

![Sci-Fi, Thermostat, Neon HUD and Evolution](docs/images/v4.0.0/hero-designs.png)

## What's new in 4.0.0

- **26 new designs**: Sci-Fi, Thermostat, Neon HUD, Neon tiles, Smart Panel, Purple dashboard and list, Evolution, Dial, History graph, Double ring, Level columns, Duplex, Signal rows, Open panel, the Clarity family, Control deck and six graph variants.
- **Every widget in every design**: clock, calendar, weather, now playing, top apps, data usage, uptime, overview and drive health follow the chosen design.
- **Design per register**: one design for HW, another for INFO.
- **Size controls**: widget, graphic, main value, value and label size – per widget or for all at once.
- **Every drive and both network directions** are shown in every design.
- **Designs follow your background and colour scheme.**
- **No jumping**: columns end flush at the bottom, widgets keep their place when values change.
- **Translated everywhere**, no word broken in the middle.

![Info widgets in four designs](docs/images/v4.0.0/info-widgets.png)

![Horizontal bar](docs/images/v4.0.0/horizontal-bar.png)

![Style per register](docs/images/v4.0.0/settings-style-per-register.png)

**[Browse images of all 36 designs, in dark and light](docs/style-gallery.md).** All pictures use demo data.

## Sensor support

| Source | Integration |
| --- | --- |
| **LibreHardwareMonitor** | Built-in sensor library for supported CPU, GPU, storage, mainboard, fan and battery readings. |
| **Open Hardware Monitor** | Built-in driverless GPU/SMART fallback, combined with LibreHardwareMonitor readings. |
| **AIDA64** | Optional external source through shared memory. Enable shared memory in AIDA64's External Applications settings, then select AIDA64 in **Settings → Sensors**. |
| **HWiNFO** | Optional external shared-memory source, also available in **Settings → Sensors**. |

Automatic selection and fallback are supported. Hardware, permissions and available drivers determine which readings can be obtained. AIDA64 and HWiNFO are separate applications; they are not bundled with Lumen.

## Layout and widgets

- Left/right sidebar or top/bottom bar, with multiple rows/columns and up to six pages.
- Automatic density, adjustable text size, custom titles and hardware names.
- CPU, discrete/integrated GPU, memory, drives, storage space, network, cooling, power, FPS, ping, battery, system, weather, clocks, calendar, media, usage and notes.
- Colour presets, state colours, Windows accent, theme files, glass and several typefaces.
- English, German, French, Italian, Spanish and Portuguese.

## Diagnostics

Error reports include the executable path, active layout, recent activity, available error logs, session state and restart history. Log read failures are distinguished from missing logs. Settings → System also shows the executable location and provides **Open folder**.

Logs and reports stay local. Nothing is uploaded automatically.

## Download and run

Download **Lumen-4.0.0.exe** for the portable application, or the portable ZIP with licence files. No separate .NET installation is required. Close an older running instance before starting the new EXE. Settings are stored beside the EXE, with an AppData fallback when that folder is read-only.

The release also includes the complete source archive, all gallery images and SHA-256 checksums.

## Build and verify

```powershell
dotnet test tests/Lumen.Tests -c Release -m:1
dotnet run --project tools/Lumen.Regression -c Release -- --expect-fixed
dotnet publish src/Lumen.App -c Release -r win-x64 --self-contained -m:1 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

The release workflow builds the EXE from the tagged source, runs the tests and packages the source, screenshots and checksums. UI image baselines are OS-specific.

## Licence

Lumen is distributed under the [MIT licence](LICENSE). The Open Hardware Monitor source in `lib/OpenHardwareMonitor.Driverless` remains MPL-2.0. LibreHardwareMonitor is used as a NuGet package under its licence. PresentMon is MIT-licensed. Included notices are in `licenses/` and the source archive.

