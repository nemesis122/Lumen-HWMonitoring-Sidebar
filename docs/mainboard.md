# Lumen 4.4.0 — Mainboard and design selection

## Choose a Mainboard design

Open Settings → Widgets and scroll to the end of the design list. Mainboard, Mainboard Map, Mainboard Map Desktop and Mainboard Cores are grouped there. Choose a design for all widgets or for a selected register. There is no separate Mainboard Map entry in the tray menu.

Mainboard Cores places CPU and GPU in equal-height panels above a full-width core-load strip, followed by RAM/PSU, drives, fans and network. At 440 px, sixteen cores fit in one row. Narrower widths or larger scaling wrap them into more rows. All selected readings, including inline VRAM usage, remain available. The original Mainboard design is still available.

Mainboard Map Desktop fills the working area of the selected monitor, using one column without floating widgets. The board is capped at 600 logical pixels high and the dashboard at 1400 logical pixels wide. CPU, GPU and network are on the left; RAM, PSU and drives are on the right; cores and fans are below. Choosing a sidebar design after the Desktop design disables whole-screen placement. Monitor and placement options remain under Placement.

For Mainboard Cores, start with a sidebar width of 440 px. Close an older Lumen instance before launching the new executable. Keep your existing settings to retain your preferences.

## Main-widget selections are stored per design

A newly selected ordinary design starts with five HW widgets: CPU, GPU, RAM, drives and network. It also starts with four INFO widgets: clock, world clocks, calendar and weather. A newly selected AIO/Mainboard design adds cooling/mainboard and power, giving seven HW and four INFO widgets.

Your own checks for these eleven main widgets are stored for each design. A new design uses its defaults; returning to a customized design restores its saved selection. The active design and current checks survive restarts. Clicking the same design again does not reset your edits. A register-specific design change leaves other registers unchanged.

This rule only covers these main widgets. Extra widgets such as FPS, ping or battery, individual sensor choices under Customise, widget order and register assignments are not reset.

## Design cleanup

Removed from all design selectors: Mainboard Typography, Mainboard Data Matrix, Mainboard Instruments, Mainboard Strips, AIO Pop Console, AIO Chromatic and Aperture. Old profiles using these designs switch to Standard while keeping their other widget preferences. The old design IDs remain readable so existing IDs do not shift. There are 82 selectable designs.

## Extra widgets in AIO designs

Clock, world clocks, calendar, weather, media, battery, FPS, top apps, system, storage space, ping, data usage, notes, drive health and timer have matching extra cards in every available AIO design. They keep their existing live data, sensor switches and controls. They no longer silently use the Standard frame while the selector displays an AIO name.

Enable an extra widget with its checkbox under Widgets. Notes and Timer, which have no individual style picker, automatically follow a uniform AIO design on their register, including in older profiles. Mixed registers do not override these two widgets. Overview remains the combined hardware card.

## Connections and readings

Mainboard network values stay within three separate download/upload/link cells, with padding and values that fit their available width. PCB traces are interrupted around wide panels such as NETWORK and FANS. They are not drawn behind titles or readings, including when the card background is transparent.

In the Map designs, cyan identifies CPU/RAM and violet identifies GPU/PSU. Card numbers, component outlines and connections match. ATX identifies the power connector, not a separate physical PSU on the board. The board image is a generic illustration, not a detected motherboard model.

The application uses Lumen's existing live sensor sources. Screenshots use demo data. Sensor availability depends on hardware, permissions and the selected source; missing readings appear as “—”.

## Build from source with the .NET 8 SDK

    dotnet test tests/Lumen.Tests -c Release -m:1
    dotnet publish src/Lumen.App -c Release -r win-x64 --self-contained -m:1 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish

Render native previews:

    dotnet build tools/Lumen.Preview -c Release -m:1
    dotnet tools/Lumen.Preview/bin/Release/net8.0/Lumen.Preview.dll preview --mainboard --en

Repository: https://github.com/nemesis122/Lumen-HWMonitoring-Sidebar

Licence notices are in LICENSE and licenses/.
