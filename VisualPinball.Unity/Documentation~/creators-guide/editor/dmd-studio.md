# DMD Studio

DMD Studio is available from **Pinball > DMD Studio**. Create or select a `DmdProjectAsset`, add cues and layers,
then use the sample-state selector to preview live bindings without entering play mode.

## Authoring

- Drag layer bodies and end handles in the timeline to move or resize their spans. Drag the shaded enter and exit
  boundaries in the ruler to set transition durations.
- Select an animatable property and use **Add Key** to add a key at the playhead. Existing key diamonds can be
  dragged. Sprite-frame durations are editable in the Pixel / Glyph Editor.
- `NumberLayer.CountUpSeconds` enables deterministic count-up animation. Set a text layer's overflow to `Marquee`
  and give it a positive speed to scroll clipped text.
- The Pixel / Glyph Editor supports pencil, eraser, fill, rectangle, ramp or palette shades, and transparent pixels.
  Select a font to touch up glyph pixels and metrics. All bitmap edits participate in Unity undo.
- Validation is continuously refreshed after authoring operations. The Cue Simulator accepts lines such as
  `t=0 Play(jackpot, value=100000)` and plots the base, active, held, and queued scheduler lanes.

## Fonts

V1 imports single-page BMFont text descriptors and their PNG atlas. DMD Studio explicitly converts PNG rows to its
top-origin coordinate convention. TTF/OTF baking is intentionally external because Unity 6000.5 does not expose the
required glyph rasterization API publicly.

**Add Starter Fonts** adds four package assets with tabular digits and ASCII `0x20`–`0x7E` plus `©®™•×`:

| Asset | Approximate pixel size | Source |
|---|---:|---|
| VpeMicro5 | 5 px | Tiny5 |
| VpeMicro7 | 7 px | Tiny5 |
| VpeArcade9 | 9 px | Press Start 2P |
| VpeArcade15 | 15 px | Press Start 2P |

The atlases were baked with Pillow 11.3.0 from Google Fonts commit
`03781cf7a714af8431d14b6f337f923c774429d7`, using `ofl/tiny5/Tiny5-Regular.ttf` and
`ofl/pressstart2p/PressStart2P-Regular.ttf`. Glyph masks use a 128/255 threshold and are stored in
top-origin atlases. Both sources are SIL Open Font License 1.1. The exact copyright and license text is shipped beside
the generated assets in `DmdStudio/StarterFonts`; every `DmdFontAsset.Notes` field repeats its source and attribution.
