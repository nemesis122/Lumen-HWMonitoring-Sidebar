# Sidebar dashboards

These native designs extend the existing Lumen 4.5.0 application and include the separate global typeface options and the corrected light/dark font picker.

| Original concept | Name in Settings → Widgets | Initial layout |
| --- | --- | --- |
| 08 | AIO · Mainboard Card | Two columns, CPU/GPU/RAM-labelled board illustration and live panels |
| 12 | AIO · Double Table | Two columns, paired CPU/GPU, RAM/drives and cooling/network |
| 17 | AIO · Quadrants | Two columns, four major groups |
| 27 | AIO · Network Center | One 440 px column, network history and full hardware details |

Selecting one of these designs preserves every existing Placement choice: column count, width, density, full-screen mode, registers and register names. The table above describes the original concept arrangements, not settings imposed by the design. At 440 px per column a two-column sidebar has 880 logical px plus the existing inter-column spacing at 100% text scale. Dashboards span the sidebar width, with any companion widgets below. The internal layout follows the configured column count: sensor panels are balanced across three or four columns too, instead of staying in two fixed columns. At insufficient width the layout reflows. In a narrow sensor table the limits reflow below the current value.

These dashboards respect the existing Placement → Density setting: Automatic fills the available height without scrolling; Compact and Comfortable retain the existing scrolling behavior. Selecting a design does not change this setting. Sensor count, additional GPUs/drives, text scaling and font choices affect the resulting height and automatic scale.

Automatic distributes surplus height into the dashboard's sensor panels and metric rows. It does not simulate a full-height layout by adding a large empty padding below the dashboard. Each internal column ends at the common bottom edge of the available work area, above the taskbar. Natural scrolling layouts remain compact.

## Readings

All labels and readings are native controls, not text baked into screenshots. They reuse the existing AIO sensor identities, per-widget sensor switches, device switches, model naming, opacity, colours and global title/label/value/graph fonts. CPU core loads and individually selected core temperatures remain available.

The new dashboards additionally expose RAM used/free/total, available mainboard temperatures, available drive health/activity/operating-hours readings and available ping/jitter/loss readings. Additional values can also be explicitly enabled in other AIO designs. A missing provider reading is not estimated. Integrated/further GPUs have distinct labels and sensor identities.

In Settings, every per-GPU AIO sensor option includes its GPU number and the model name from the detected device. Names are generated from the sensor snapshot for all vendors and models, including identical model names. The stable sensor keys and checked/unchecked choices are unchanged.

Min/max accumulates finite observed readings for the lifetime of the dashboard widget and seeds existing history when available. It includes real zero values and ignores missing values. It is not an all-time persisted statistic. Temperature extrema are stored in Celsius and formatted in the selected unit. Network extrema keep the displayed bit-rate units. Network charts use a shared scale for selected download and upload series and obey both the history and individual sensor switches.

Hardware pictures are representative illustrations, not photographs of the user's detected hardware model. The case image is a fixed illustration from the approved concept; values are separately rendered live. Board markers are drawn independently of the board bitmap. These builds do not add animated fans or freely rotating 3D models.

## Compatibility and verification

The dashboard enum IDs are appended after the existing IDs; saved designs retain their values. The four retained layouts are registered in the normal style pickers and saved profiles. Switching away preserves individual widget preferences through the existing design-selection behavior.

Automated checks cover profile round trips, selector registration, every selected sensor appearing once, live updates, hiding groups and individual sensors, extrema including zero, temperature-unit changes, graph visibility, default placement, native rendering at 440/880 px, reflow and horizontal text bounds, and the full-width sidebar in both automatic-fit and scrolling modes. Existing AIO, core, font and colour regression checks are also run. The included screenshots are captures of the implemented controls with demo readings.

The column/height audit additionally renders every selectable design with 2, 3 and 4 columns at 900, 1040 and 1400 px work-area heights, with live updates between sizes. The four retained dashboards are also checked for internal column count, panel bottom alignment, absence of excessive root padding and layout cycles. GPU option labels are tested with NVIDIA/AMD, AMD/Intel, Intel/AMD and identical NVIDIA models.

The Min–Max layout has been removed from selection; saved uses migrate to Cyan Glas. Design names no longer carry concept numbers. The image disclaimer is removed, and component labels replace numbered board markers. The ten full-case overlays are documented in [case-designs.md](case-designs.md).

Sensor Table, Two Halves, Summary + Details and Case Plan have also been removed from selection. Their saved IDs migrate to Cyan Glas. Mainboard Card, Double Table, Quadrants and Network Center remain available.
