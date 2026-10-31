# Open Hardware Monitor — driverless subset for Sidebar Remastered by NemeSIS

This is a modified, reduced build of the maintained HardwareMonitor/OpenHardwareMonitor fork. It is **not** an upstream Open Hardware Monitor PawnIO implementation.

Upstream: https://github.com/HardwareMonitor/OpenHardwareMonitor/tree/3f6b9b14cf4780281b4771ffc793ea0660138f91

Upstream library version: 1.0.9513. Local assembly: `NemeSIS.OpenHardwareMonitor.Driverless`, version 1.0.9513.1.

## Changes made on 2026-10-29

- Retained AMD ADL, NVIDIA NVAPI/NVML, Windows SMART and their sensor model code.
- Removed all embedded driver resources and their extraction, installation and loading code. No WinRing0 or InpOut driver is included or loaded.
- Removed the upstream CPU, motherboard, memory/SPD, controller, network, PSU and battery backends, together with raw register and physical memory access implementations.
- Removed Intel GPU implementations that relied on CPU/raw-register access. The host uses LibreHardwareMonitor for unsupported GPUs, including Intel devices.
- Replaced the upstream `Computer` entry point with `DriverlessComputer`, which only opens vendor-API GPU and Windows SMART groups.
- Replaced the external common settings/OS helper dependency with a small in-memory settings store and Windows platform helper.
- Added a build check rejecting embedded resources. The host's backend tests inspect the built assembly for driver resources, installation imports and removed raw-access types before hardware tests.

## How the Sidebar option works

`Open Hardware Monitor + PawnIO` is an explicitly labelled **hybrid**:

- GPU and SMART readings: this modified Open Hardware Monitor subset, where available.
- CPU, motherboard and controllers: the existing LibreHardwareMonitor 0.9.6 engine with its PawnIO modules.
- Unsupported GPU/storage devices: LibreHardwareMonitor fallback. Repeated identical models are matched individually.
- Memory totals, volumes and network: Windows APIs, as in the existing Sidebar.

The host never substitutes Libre values while claiming they were read by upstream OHM. Raw sensor rows retain a provider name, and Settings describes the bridge. Neither option requires a separate monitoring application. Both use the existing explicit PawnIO setup flow for privileged sensors; no driver is installed automatically.

This is not a complete native port of every upstream OHM backend to PawnIO. Hardware coverage depends on the selected readers, installed vendor drivers and permissions. Missing readings stay unavailable.

## Additional local changes on 2026-10-30

- Added an optional storage-only mode to DriverlessComputer. The Libre option uses it to supplement physical drives absent from Libre, with explicitly labelled OHM fallback providers. It opens no GPU groups in this mode.
- The hybrid merge now supplements missing sensor values and extra sensors from the matching Libre device while retaining available OHM readings. Added sensors have stable IDs under the original device and preference aliases.

## License and source

Original source files retain their upstream notices. Open Hardware Monitor and the modifications are distributed under MPL-2.0; see `LICENSE.txt`. Corresponding modified source is included in the Sidebar source archive in this folder. Generated `bin/` and `obj/` files are excluded.
