# Lumen Monitoring HW Sidebar

A modern hardware sidebar for Windows 10 and 11: a glass strip on the left or right edge of the
screen – or a bar along the top or bottom – with clock, processor, graphics cards, cooling,
memory, drives, network and more. One portable EXE, .NET included, in six languages
(English, Deutsch, Français, Italiano, Español, Português).

**[Download the latest release](../../releases/latest)** · portable `Lumen-x.y.z.exe`, no installation

<p>
  <img src="docs/images/sidebar.png" width="250" alt="Lumen sidebar, dark">
  <img src="docs/images/two-columns.png" width="420" alt="Two columns with many widgets">
  <img src="docs/images/sidebar-new-styles.png" width="250" alt="Graph, bar and tile styles">
</p>

<img src="docs/images/bar-top.png" width="860" alt="Bar at the top of the screen, two rows">

## Highlights

- **Colour means state.** Every reading moves along one cool-to-hot ramp (blue, mint, amber,
  red), so a glance shows what is working hard. Alternatively the Windows accent colour or a
  preset; hot readings still turn amber and red.
- **Temperature leads.** Wherever a part has a temperature, it is the big number, with two
  minutes of history behind it.
- **Fills the screen exactly.** The automatic density sizes everything to reach from the top of
  the screen to the taskbar, without scrolling. Up to six columns, or the whole screen.
- **Side or bar.** Right or left edge, or a bar at the top or bottom with up to six rows,
  across the whole width or centred; it can reserve its space like the taskbar.
- **Registers.** Up to six pages, with your own names (e.g. Hardware, Gaming), switched by click
  or mouse wheel.
- **Six styles per widget:** the full card, ring, minimal line, bar, graph or tile – rings and
  tiles pair up side by side. Every style shows the values you switched on; one click sets the
  same style for all widgets.
- **Your names:** each widget can have its own title and hardware name, and either can be hidden.
- **Choose what is listed:** single values per widget, which drives and which volumes are shown.
- **External IP on request:** a button in the network widget asks once – never in the background.
- **Colours and backgrounds:** state colours, the Windows accent or 14 presets; nine background
  tones from graphite and OLED black to midnight blue, forest or aubergine.
- **Hardware names** under every title, shortened in tiles (Ryzen 9 5900X, RTX 3080).
- **Missing stays missing:** a sensor that cannot be read shows "—", never a fake zero.

## Widgets

| Hardware | More (opt-in) |
|---|---|
| Processor: temperature, load per thread, clock, power, voltage, fan | Weather (Open-Meteo, no account) |
| Graphics, dedicated and integrated: temperature, hot spot, load, VRAM, power, fan | Now playing (Spotify, browsers, media apps) with controls |
| Cooling and mainboard: every spinning fan, board, VRM and chipset temperatures | Ping and jitter to the internet and the router |
| Memory with segment bar and 2-minute peak | Power and cost per day, week and month |
| Drives: temperature, used space, read and write rates | Data usage per day, week and month |
| Network: download/upload, throughput in Mbit/s, share of the link, IP | Storage space per drive letter |
| Battery: charge, time left, rate, health | System uptime, world clocks, calendar, notes, top apps |
| FPS of the game in the foreground, 1 % low, frame time (PresentMon) | |

<img src="docs/images/bar-bottom.png" width="860" alt="Bar at the bottom with ring, tile and graph styles">

<p>
  <img src="docs/images/sidebar-new-1.png" width="250" alt="Now playing, world clocks, calendar, ping">
  <img src="docs/images/sidebar-new-2.png" width="250" alt="Power, data usage, system, storage, notes">
  <img src="docs/images/sidebar-extras.png" width="250" alt="Weather, battery, cooling, top apps">
</p>

## Sensors

Two libraries work together, so hardware one of them misses is still covered:

- **LibreHardwareMonitor 0.9.6** with the PawnIO driver reads CPU, GPUs, drives, mainboard
  sensors, fan controllers and battery.
- **Open Hardware Monitor, driverless build** (MPL-2.0, in `lib/OpenHardwareMonitor.Driverless`)
  reads GPUs through the NVIDIA and AMD vendor APIs and drives through Windows SMART. It only
  fills readings Libre left empty and adds GPUs or drives only it can see; identical models are
  paired one by one.
- Windows itself supplies memory, network, CPU load as a fallback, battery state, volumes,
  processes, uptime and media.

CPU temperatures and mainboard sensors need **administrator rights** and the **PawnIO** driver.
Without them the sidebar says so and offers a restart as administrator. *Start with Windows*
creates a logon task with highest rights, so there is no UAC prompt at every start.

**Settings → System → Show all sensors** lists every sensor both libraries found and what the
sidebar shows, with Copy and Save, for tracking down a missing reading.

<img src="docs/images/sensors-window.png" width="560" alt="Sensor list">

## Settings

Everything applies immediately, sorted into topics: appearance (language, theme, colours,
background, glass and opacity, high contrast, hardware names, text size 50–200 %), layout
(columns or rows, registers and their names, whole screen, density, width), widgets (visibility,
order, style, column/row, register and individual values), position (edge, screen, always on
top, reserved space, click-through), readings (interval, °C/°F, warning threshold with pop-up),
weather and clocks, power and ping, and system (autostart, sensor list).

Defaults: dark theme, high contrast, language as in Windows.

<p>
  <img src="docs/images/settings-de.png" width="430" alt="Settings: widgets">
  <img src="docs/images/settings-layout.png" width="430" alt="Settings: layout">
</p>

<p>
  <img src="docs/images/backgrounds.png" width="430" alt="Backgrounds and accent colours">
  <img src="docs/images/desktop-warning.png" width="430" alt="Temperature warning">
</p>

Settings are stored in `lumen-settings.json` beside the EXE, or in `%AppData%\Lumen` if that
folder is read-only. `Lumen.exe --demo` shows demo data.

## Build

```
dotnet test tests/Lumen.Tests
dotnet publish src/Lumen.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
dotnet run --project tools/Lumen.Shots -- shots     # renders the screenshots headless
```

The release workflow (Actions → *Build and publish release*) builds on Windows, runs the tests
and publishes the EXE, the source ZIP and the screenshots under the version in
`Directory.Build.props`.

Built with Avalonia 11.3 and the Inter typeface.

## Licence

Lumen is released under the [MIT licence](LICENSE). The Open Hardware Monitor parts in
`lib/OpenHardwareMonitor.Driverless` stay under MPL-2.0; the bundled PresentMon 2.3.1 (Intel) is
under the MIT licence. Both licence texts are in `licenses/`. LibreHardwareMonitor (MPL-2.0) is
used as a NuGet package.
