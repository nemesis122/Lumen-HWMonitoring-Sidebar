# Widget and sensor colours

Colours inherit in this order: **sensor → widget → global → design**. Each colour
inherits independently, so a custom temperature value does not change its label
or graph. Existing profiles keep their appearance until you choose an override.

## Global defaults

Open **Settings → Appearance → Colours**. Choose separate colours for titles,
labels, values and graphs. Pick a swatch or enter a HEX colour such as `#22D3EE`.
Choose **As the design draws it** to remove an override.

The existing global value and label settings retain their behaviour: they recolour
neutral text while preserving a design's accents. Widget and sensor overrides
also recolour accented text.

## One widget

Open **Settings → Widgets → Customise → Colours** and turn off **Use global
colours**. Choose title, label, value and graph/bar colours independently.
**Like global** inherits one colour; **Restore global colours** resets the widget's
colour settings. Sensor overrides remain independent and can be reset separately.

In an AIO design, CPU, GPU, memory and other hardware sections use the corresponding
widget's colours. The Overview widget supplies their shared defaults.

## One sensor

Under **Sensor values**, click the small colour button next to a reading. The main
palette changes its value colour. Expand **Labels, graphs and warnings** for its
label colour, graph colour and warning behaviour. **Like widget** removes that
individual override; **Reset sensor colours** clears all overrides for that sensor.
The colour button does not change which readings are selected.

Where a graph contains two bound readings, each series can have its own colour,
including network download and upload. Colours follow sensor identities, rather
than displayed values or GPU model names. A combined chart without a separately
bound series uses its widget's graph colour.

## Warning colours and saved settings

Automatic warning colours are enabled by default. Hot temperature readings retain
amber/red warnings. Turn **Automatic warning colours** off globally, per widget
or per sensor to keep your chosen colour. **Like global / Like widget** inherits
the warning setting too.

Overrides are included in saved profiles and exported themes. Importing an older
theme without these fields keeps your colour overrides. Layout, columns, registers,
scrolling and sensor selections are independent of colour settings.

## Colour assignment fix

Panel and Black bars, core cells and gauges now use colour overrides even when
their original appearance came from a theme object or a fixed grey fill. Native
graphs, additional sensor rows and individual volume bars retain their sensor
identities. Repeated AIO values (including RAM amount and load) share the same
sensor override. Notes also apply the value colour while editing.

The colour button beside a history/bar option sets its graph colour; an override
on the actual reading takes precedence. Removing an override restores the widget
or design colour, including the original theme and transparency.
