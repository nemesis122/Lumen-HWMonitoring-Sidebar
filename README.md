# Lumen HW Monitoring Sidebar

A modern hardware sidebar for Windows 10 and 11 – one portable EXE, .NET included, six languages
(English, Deutsch, Français, Italiano, Español, Português). **[Download the latest release](../../releases/latest)** – portable `Lumen-x.y.z.exe`, no
installation. All screenshots use demo data.

<img src="docs/images/desktop-right.png" width="860" alt="Lumen at the right edge of the screen">

## Side, top or bottom

- **Right or left edge**, 1–6 columns, or the whole screen.
- **Bar at the top or bottom** with 1–6 rows, across the whole width or only as wide as its
  widgets, centred. Can reserve its space like the taskbar.
- **Automatic density** fills the height exactly, without scrolling.
- **Registers (pages) 1–6** with your own names, switched by click or mouse wheel.
- **Text size 50–200 %** – above 100 % the sidebar widens by the same factor.

<img src="docs/images/bar-top.png" width="860" alt="Bar at the top, two rows">
<img src="docs/images/bar-bottom.png" width="860" alt="Bar at the bottom with mixed styles">

<p>
  <img src="docs/images/two-columns.png" width="420" alt="Two columns">
  <img src="docs/images/registers.png" width="200" alt="Named registers">
</p>

## Widgets and styles

- Clock, world clocks, calendar, weather, now playing, battery, processor, graphics (dedicated and
  integrated), FPS, cooling and mainboard, power and cost, memory, top apps, system, drives,
  storage space, network, ping, data usage, notes.
- **Six styles per widget:** Standard, Ring, Minimal, Bar, Graph, Tile. **One click sets the style
  of all widgets.** Every style shows the single values you keep switched on.
- **Own title and hardware name per widget**, each can be shown or hidden.
- **Drives and storage space:** choose which drives and volumes appear; each detail switchable.
- **Network:** link speed, link use, 2-minute peak, local address and – only on request in the
  settings – the external IP (one HTTPS request per click, never in the background).

<p>
  <img src="docs/images/sidebar-new-styles.png" width="200" alt="Graph, bar and tile styles">
  <img src="docs/images/sidebar-styles.png" width="200" alt="Ring and minimal styles">
  <img src="docs/images/sidebar-volumes-styles.png" width="200" alt="Storage space and drives">
  <img src="docs/images/sidebar-own-names.png" width="200" alt="Own title and hardware name">
</p>

<img src="docs/images/bar-rows-ring.png" width="860" alt="Bar with three rows, ring style">

## Colours

State colours (blue → mint → amber → red), the Windows accent or 14 presets; nine backgrounds
(Graphite, Black OLED, Midnight blue, Deep sea, Forest, Aubergine, Burgundy, Mocha, Slate); glass
with adjustable opacity; dark or light theme; high contrast. Defaults: dark, high contrast, language
as in Windows.

<p>
  <img src="docs/images/backgrounds.png" width="560" alt="Backgrounds and colours">
  <img src="docs/images/sidebar-light.png" width="200" alt="Light theme">
</p>

## Sensors

- **LibreHardwareMonitor 0.9.6** (PawnIO) and the **driverless Open Hardware Monitor** work
  together; one shared set of rules picks the right readings on Intel (incl. P/E cores), AMD Ryzen,
  NVIDIA, AMD and Intel Arc graphics, laptops and many mainboards.
- Drive letters per physical drive; VPN and virtual network adapters are skipped.
- **Settings → System → Show all sensors** lists everything both libraries found.
- CPU temperatures need administrator rights; *Start with Windows* runs elevated without a prompt.
- Temperature warning with pop-up; errors are written to `lumen-crash.log` beside the EXE.

## Settings

A wide window with topics; everything applies immediately.

<p>
  <img src="docs/images/settings-1-appearance.png" width="420" alt="Appearance">
  <img src="docs/images/settings-2-layout.png" width="420" alt="Layout">
  <img src="docs/images/settings-3-widgets.png" width="420" alt="Widgets">
  <img src="docs/images/settings-4-position.png" width="420" alt="Position">
  <img src="docs/images/settings-5-readings.png" width="420" alt="Readings">
  <img src="docs/images/settings-8-system.png" width="420" alt="System">
</p>

<p>
  <img src="docs/images/sensors-window.png" width="420" alt="All sensors">
  <img src="docs/images/warning.png" width="420" alt="Temperature warning">
</p>

Settings are stored in `lumen-settings.json` beside the EXE, or in `%AppData%\Lumen` if that
folder is read-only. `Lumen.exe --demo` shows demo data. The EXE is not code-signed, so Windows
SmartScreen may ask once ("More info" → "Run anyway").

## Build

```
dotnet test tests/Lumen.Tests
dotnet publish src/Lumen.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true
dotnet run --project tools/Lumen.Shots -c Release -- shots      # renders the screenshots headless (LUMEN_SHOT_LANG=en for English)
```

The release workflow (Actions → *Build and publish release*) builds on Windows, runs the tests and
publishes the EXE, the source ZIP and the screenshots under the version in `Directory.Build.props`.
Built with Avalonia 11.3 and the Inter typeface.

## Licence

Lumen is released under the [MIT licence](LICENSE). The Open Hardware Monitor parts in
`lib/OpenHardwareMonitor.Driverless` stay under MPL-2.0; the bundled PresentMon 2.3.1 (Intel) is
under the MIT licence. Both licence texts are in `licenses/`. LibreHardwareMonitor (MPL-2.0) is
used as a NuGet package.
