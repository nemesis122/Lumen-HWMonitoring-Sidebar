# Global typefaces

Settings → Appearance → Text now offers a base typeface and independent global overrides for Titles, Labels, Values, and Graphs and scales. Selecting **Base typeface** in a group removes its override. Existing profiles retain their previous appearance until an override is chosen.

Every picker previews its fonts. The sample below the pickers shows titles, labels, measurement values and a scale separately. **Reset typefaces** restores Inter and clears all four overrides. Per-row reset indicators also remain available. Settings and exported theme files preserve all five choices.

The picker text explicitly uses the active palette's ink colour, including the selected item and the inherited base-font entry. This fixes black text on the dark selection field.

The 22 additional families are embedded in the application; no font installation or runtime download is needed:

- Clear: Roboto, Open Sans, Lato, Noto Sans, Source Sans 3.
- Technical: Rajdhani, Exo 2, Oxanium, Chakra Petch.
- Numbers: Roboto Mono, JetBrains Mono, IBM Plex Mono, Share Tech Mono.
- Bold titles: Bebas Neue, Anton, Oswald, Barlow Condensed, Black Ops One.
- Graffiti / handwriting: Rubik Spray Paint, Rubik Wet Paint, Permanent Marker, Rock Salt.

Two design faces came with 5.2.0: Doto (dot matrix, LED Dot Matrix design) and Playfair Display (serif, Black & Gold and Sumi-e Ink); they are used by those designs, not offered in the font list. 5.3.0 added DSEG7 Classic (seven-segment digits, OFL) for the Segments gallery designs. 5.4.0 added Press Start 2P (8-bit pixel, OFL) for 8-Bit Pixel and Gochi Hand (handwriting, OFL) for Chalkboard. Inter and Orbitron were already bundled. Existing Windows font options remain available. Decorative fonts can be assigned to any role; narrow widgets may shrink or trim long text according to their existing layout rules.

## Font provenance and licensing

Fonts are unmodified upstream files downloaded on 2026-11-16 from [Google Fonts](https://github.com/google/fonts) and the official Google Fonts CSS/font service (`fonts.googleapis.com`, `fonts.gstatic.com`). Regular, Medium, SemiBold and Bold cuts are included where available. Static cuts are used instead of variable fonts so the requested weight resolves consistently in Avalonia.

OFL-1.1 licenses and copyright notices accompany their font files in `src/Lumen.App/Assets/Fonts`. Permanent Marker and Rock Salt use Apache-2.0, with their upstream LICENSE files in that directory. Font license files are also copied into the portable package's `licenses/fonts` directory.

## Implementation

`FontCatalog` defines stable keys and family names. `TitleFont`, `LabelFont`, `ValueFont`, and `GraphFont` are optional global settings; an empty key inherits existing base/design typography. Text roles are explicit for titles and decorative labels; measured values use sensor bindings or number features rather than guessing from digits in a model name. The shared appearance pass reapplies overrides after sensor updates and dynamically created rows. Custom drawn core bars and gauge captions use the graph font when set.

## Validation

Tests cover profile and theme round trips, invalid/legacy settings, actual embedded font family and normal-weight resolution, digit-containing hardware labels, independent picker changes, reset behavior, and live-update rendering in all AIO designs plus representative native designs.
