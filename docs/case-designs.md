# Full-case sensor overlays

## Cyan Glas refinement preview

The one- and two-column Cyan Glas scene has a revised presentation: nearly opaque dark cards, compact label/value pairs, smaller core bars and short connectors ending in dots. Network, voltages and other aggregate values form an unconnected bottom dock. Default device names are available on the card-title tooltip; custom hardware names and names needed to distinguish multiple GPUs stay visible. Drive cards are numbered independently and their pointers use separate M.2 locations. Selected sensor identities remain unchanged.

Two columns use a separate 4:5 background (`01-Cyan-Glas-two-column.png`) with measured component coordinates. The picture covers the full scene using uniform scaling and cropping, so no artificial metal side gutters or distorted hardware are needed. Card widths are capped at 240 logical pixels. This is a focused refinement of the one- and two-column Cyan Glas layout; the wider Cyan Glas layouts retain their previous presentation while the new direction is reviewed.

The new clean background was generated with the built-in image-generation tool from this specification: photorealistic black open dual-chamber chassis filling a 4:5 portrait canvas, central motherboard, visible CPU cooler, four RAM sticks, unbranded GPU, two M.2 heatsinks, radiator fans and PSU; restrained cyan illumination, no typography, branding, UI or arrows.

## Available designs

Cyan Glas remains available in the existing design picker. The persisted IDs are retained in `WidgetStyle`; existing settings and IDs are retained.

| Style | Look |
| --- | --- |
| Case · Cyan Glas | Compact dark cards and short cyan connections |

## Live interface

The clean backgrounds are bitmap assets. Sensor values, model names, labels, optional titles and graphs are separate native controls, not baked into the image. The existing selected AIO readings supply the data. Each selected sensor has one native binding, including additional GPUs, drives, core temperatures and core-load bars. Devices without a reported sensor do not acquire invented readings. Each GPU and drive has its own model-specific card. GPU fan readings join that GPU's card, CPU fan readings join the CPU card, and mainboard temperatures have a separate mainboard card.

Arrowheads connect cards to measured component positions in each background. The layout reserves visible points on the hardware and routes orthogonal connectors around sensor panels. It recomputes coordinates after arranging the panel, so resizing and automatic fitting retain the association. Wide sidebars extend the dark metal chassis around the central hardware picture rather than stretching the hardware. Multiple GPUs/drives are listed separately; the picture remains a representative case view.

Width, column count, registers, register names, full-screen mode and density are preserved when selecting any design. Automatic fits the scene to the available work area above the taskbar; Comfortable and Compact retain scrolling. Extra sensor selections and enlarged fonts affect the resulting natural height. Two to six configured columns use that many widget rails; a one-column case uses overlay rails inside that column (Blue Modules uses its single rail). Global title/label/value fonts and colours remain effective.

## Assets

The original approved preview images were edited with the built-in image-generation tool to remove widgets, text, arrows, graphs, headers and taskbars while preserving the hardware positions, colours and portrait framing. The retained assets are in `src/Lumen.App/Assets/Aio/Cases`. The legacy Case Plan image also uses the clean amber case, removing the baked GeForce RTX branding. No illustration disclaimer is drawn.

Shared edit prompt: "Produce only the clean photorealistic PC interior background. Remove all UI overlays, panels, labels, values, icons, charts, arrows, lines, headers and Windows taskbars; reconstruct the hidden hardware. Preserve the original full portrait framing, component positions, camera and lighting. Unbranded hardware: no GeForce RTX, logos, text or watermark."

## Verification

Case-specific tests check ID persistence, unchanged placement, one binding per selected reading, live changes, sensor switches, different GPU models, natural scrolling, nonoverlapping widgets, visible correctly mapped arrow endpoints, and height changes at one through four columns. The existing colour, font, AIO, profile and all-design column-height regression suites also include the new styles. The supplied program screenshots use demo telemetry and are captures of the implemented controls.

## Removed designs

Technik Amber, Minimal Weiß, Grün Terminal, Blaue Module, Rot Performance, Violett Orbit, Industrie Stahl, Kompakt Sensoren and Große Werte are no longer selectable; their unused case assets are removed. Together with Sensor Table, Two Halves, Summary + Details and Case Plan, exactly thirteen styles were removed. Saved profiles migrate these IDs to Cyan Glas while retaining sensor selections, fonts and placement. Enum IDs are not renumbered.
