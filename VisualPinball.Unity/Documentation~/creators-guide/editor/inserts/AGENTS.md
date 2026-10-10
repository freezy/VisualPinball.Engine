# Recreating the inserts documentation's images

This file is for agents. It is left out of the site (`docfx.json` excludes `**/AGENTS.md`). It says how every image on these pages is made, so that any of them can be shot again after a shader, preset, inspector or geometry change, and how to add images for a new parameter. The scripts it refers to are in `automation/`; the site build ignores them.

## Ground rules

- Never commit or stage images. The user optimizes them (palette PNGs) and commits them. Commit doc text only when asked.
- Never commit in VisualPinball.Unity.Assets. Don't save the T2 or LookDev scene after a run: the runs put back what they change, in memory.
- Reshoot every still after a shader, preset, environment map or geometry change, not only the rows of the changed parameter: they are compared side by side.
- Check your stills before publishing them. Look at the lit and unlit insert, the neighbours (no stray lit insert), and that a trio lines up (same framing). Ask a cheap model (Haiku subagent) to look at contact sheets when there are many.
- After publishing, check that every image a page references exists, and that no image in a folder is unreferenced:
  ```sh
  for f in $(grep -o 'body-material/[^"]*\.png' body-material.md); do [ -f $f ] || echo missing $f; done
  for f in body-material/*.png; do grep -q "\"$f\"" *.md || echo unreferenced $f; done
  ```

## The editors and how to drive them

Two Unity editors are used, both open at the same time:

| Instance (`INSTANCE=`) | Project | Shoots |
|---|---|---|
| `vpe-terminator2` (T2) | `vpe-terminator2/` next to `VisualPinball.Engine/` | all rendered stills on the playfield, the material, cover, Insert Light and preset inspectors, the preset windows, the Lamp Manager |
| `LookDev~` | `VisualPinball.Unity.Hdrp/LookDev~` (scene `Assets/LookDev.unity`) | the index showcase, the Geometry and Pattern inspectors, the wireframes, the top of the Insert component |

They are driven over "MCP for Unity" (see the root `CLAUDE.md`). Without the `mcp__unity-mcp__*` tools, `automation/mcp.sh` speaks raw JSON-RPC to `http://127.0.0.1:8088/mcp` and resolves `INSTANCE` to `Name@hash` on every call:

```sh
INSTANCE='LookDev~' automation/mcp.sh execute_menu_item '{"menu_path":"Pinball/Automation/Geometry/Shoot"}'
automation/mcp.sh read_console '{"types":["error"],"count":10}'
```

- The screenshot code is VPE's editor assembly, menu **Pinball/Automation/** (`VisualPinball.Unity.Editor/Automation/*.cs`, the showcase in `VisualPinball.Unity.Hdrp/Editor/Automation/AutomationShowcase.cs`). Each menu reads its input from and writes its output to `<project>/Library/Automation/<tool>/`, and writes `done.txt` when finished.
- `execute_menu_item` returns at once; the shots run over the next frames. Delete `done.txt` before, poll for it after.
- One run at a time: a second start only logs "Automation: another run is still going" to the console, and the MCP call still says success. If `done.txt` never comes, read the console.
- After editing a `.cs`: `refresh_unity` with `scope=scripts` (`scope=all` for a new file), `compile=request`, `mode=force`, then poll `refresh_unity` with `compile=none, wait_for_ready=true` until `resulting_state` is `idle`, then read the console for `error CS`. Right after a compile a menu may not exist yet: retry.
- `execute_code` doesn't work in these projects. For anything the menus don't do, write a throwaway `[MenuItem]` script into the project's `Assets/Editor`, run it, delete it.
- Python on Windows can't open `/e/...` paths; `automation/paths.py` resolves everything from its own location (`VPE_WORKSPACE` overrides the workspace root). Previews and intermediates go to `%TEMP%/vpe-automation` (`VPE_AUTOMATION_WORK`).
- Inspector shots come out at the display's scale: raw pixels = points × window scale × pixelsPerPoint. T2 was at 1.75 and LookDev at 1.5 on 2026-10-09. Compute it from the raw width before reusing a row range below; if it differs, re-derive the crop with `automation/blocks.py`, or with `automation/match_crop.py` against the published image when only the scale changed.

## Scene facts (T2)

- **The pair.** `Insert, Triangle, 1.5in, Starburst - L35` is lit, the lower insert in the frame; `… - L34` above it stays off. Both wear the library body material `Insert, Triangle, 1.5in, Starburst (Clear)` (color Clear, look Clear Plastic) and the cover `Insert Cover, Clear`, so a material change shows lit and unlit in one still. The Insert Light settings go on L35 only.
- **Every lamp in T2 sits at intensity 1.** Neighbours in view must be listed in a config's `off` to stay dark: `Starburst - L33` (top edge) and `Starburst - L32` for the pair, `Starburst - L71`, `Starburst, L83` and `Starburst - L72` for the red pair. A config written without `off` renders the insert above lit; that happened once.
- **The red pair**, for parameters that only act on tinted plastic: `…, Red, L81` lit (right), `…, Red, L82` off (left), material `Insert, Triangle, 2in, Starburst (Red)`, cover `Insert Cover, Red`.
- **Library materials are read-only package assets.** The stills change them in memory and put them back (a variant that followed its parent is reverted to follow it again). Their inspectors draw greyed out, which is why the material inspector shots use a temporary project copy.
- **The shipped clear cover doesn't refract** (`_RefractionModel` 0, IOR 1). The cover's Tint, Tint Depth and Index of Refraction all work through HDRP's refraction, so their stills put the refracting `Insert Cover, Red` on both inserts for the still (`coverMaterial`), set back to clear values but for the row's own.
- The camera offsets were set by the user in the Scene view by hand and read with *Lighting/Read Scene View* (offsets from the middle between the pair). *Lighting/Frame Scene View* goes back to the first still's view.

## Rendered stills (T2): body material, cover, lighting

All made by **Pinball/Automation/Lighting/Capture** from `vpe-terminator2/Library/Automation/lighting/shots.json`, rendered through the Scene view camera at 900×900 after 12 settling repaints. `automation/stills.sh <config>` copies the config in place, runs it and waits; `automation/publish.py <prefix> <folder>` resizes the stills named `<prefix>*` into the doc folder. Delete old `<prefix>*.png` in the output first, or stale ones get published too.

Config: `insert` and `unlit` (what the names contain; both must wear the same body material), `off` (neighbours kept dark), `ballOffset`, `shots`. Per still: `name`; on L35's light `intensity` (1), `output` (1), `color` ([1,0.64,0.35], incandescent), `tintWithLamp`, `tint`, `lumens` (1.5), `range` (0.1), `extra` (bulbs 2–4 intensities); on the body material `floats` (`"_Prop=v"`), `colors` (`"_Prop=r,g,b"`), `lamps` (four `"x,y,z,radius"` in metres into `_OpticalLamp`…`D`); on the cover `coverMaterial`, `coverFloats`, `coverColors`, `noCover`; `playfieldNormals` (-1 as is, 0, 1); `ball`; `frameBoth` (aim between the pair, default true), `camOffset`, `lookOffset` (metres from that aim point), `width`, `height`.

### Body material (`body-material/`, `m-` prefix)

```sh
cd automation
python build_body.py                    # stills/body.json and stills/body-room.json from the images body-material.md shows
sh stills.sh stills/body.json           # 69 stills, about a minute
sh stills.sh stills/body-room.json      # Room, Surface Normal Strength and Fleck rows, the pair swapped
sh stills.sh stills/min-brightness.json # the red pair
python publish.py m- body-material
```

- `build_body.py` reads the `src="body-material/<name>.png"` list of the page and maps each name prefix to its shader property (`clarity-0.5` → `_InsertClarity=0.5`). A new parameter needs its prefix in the `floats` or `colors` table there, nothing else; a value that needs another to show gets it in `extra` (Surface Grain Size needs `_SurfaceGrain=1`, the material has 0; Clarity is shot at `_InsertMinBrightness=0.1`, because at the material's 0.485 the tinted and the clear body come out alike; Cavity Contrast at `_InsertCavitySpread=1`, because at 2.5 nearly every face sees the cavity; Roughness and Facet Contrast at `_OpticalScatter=0.4`, because the baked glow at 1.2 drowns the light the facets pass; Mottle Amount and Variation Seed at `_OpticalMottleSize=1`, because at 0.2 mm the mottle is a fine grain (mottle shows on the unlit insert); Smoothness without the cover and at `_InsertFacetFlatten=0.95`, values 0.65 / 0.9 / 1, because the faceted body throws the only sharp light's highlight off its ribs and the highlight only sharpens above 0.8), and the page says what it was shown at.
- **Values.** Each row's values are the ones in the page's table headers; the middle one is the material's own, unless it sits too close to an end to show anything (Cavity Contrast: 0 / 0.5 / 1, the text gives Clear Plastic's 0.9) (check with a dump of the material when presets change). Lamp Emission: the neutral is the material's own `_EmissiveColor` (1367.12), quarter a fourth of it (341.78), warm keeps a warm white's proportions at its brightness (1367.12, 884.6, 482.5). Radius moves the first virtual bulb, 21.53 mm under the insert's origin.
- **No haze.** Every body still sets `_CoverOpacity=0` on the cover: the body's look doesn't depend on it, and the clear cover's haze (0.31) washed out the tint.
- **Views.** All rows from `camOffset [0.00299, 0.06004, -0.00228]`, `lookOffset [0.00299, 0.00004, 0.00072]` (the pair, L34 on top, L35 below). Index of Refraction from `camOffset [0.02, 0.045, -0.02]`, `frameBoth: false`: steep and diagonal on L35 alone, the bend only shows at an angle. Room Return, Room Blur, Room Return Exposure, Surface Normal Strength, Flecks and Fleck Size are in `stills/body-room.json`, with the pair swapped (L34 lit, L35 off: a lit insert drowns the returned room), from `camOffset [0.02023, 0.03726, 0.00189]`, `lookOffset [0.00334, 0, -0.01311]`: down onto L35 from 59° up and from the right, the user's Scene view brought in to 0.65 of its distance. Shallow facets only return the room at an angle; straight down there is almost nothing. Flecks and Fleck Size come along that line a little closer, at 0.55 of the distance (`camOffset [0.01763, 0.03152, -0.00043]`; at a quarter the angle looked contrived), Flecks at 0 / 0.5 / 1 (Clear Plastic's 0.841 looks like 1).
- **Lit-only crops.** Parameters that only act on the lit insert lose the top third, the unlit insert: the Lamp Output and Virtual Lamp sections (Max Brightness, Lamp Emission, Radius, Strength, Lamp Spread), Roughness and Facet Contrast, but not Index of Refraction. `publish.py` crops those to `(0, h/3, w, h)` at 480×320; the rest are 480×480. A new lit-only parameter goes into `LIT_ONLY` in `publish.py`.
- **Min Brightness** shows on the tinted red insert while it is off: `stills/min-brightness.json` frames the red pair from 10 cm above, values 0 / 0.485 (Colored Plastic's) / 1. Not cropped.
- The page shows every still at `width="240"` in a three-column table.

### Cover (`cover/`, `c-` prefix)

```sh
sh stills.sh stills/cover.json && python publish.py c- cover
```

- The pair and view as the body. *Cover* none/clear: `noCover`. Opacity 0.01 / 0.1 / 0.5. Haze Color at `_CoverOpacity=0.3`, where the color reads (at 0.1 black and white looked alike). Smoothness 0.5 / 0.75 / 0.96.
- Tint, Tint Depth (with the red tint) and Index of Refraction wear `Insert Cover, Red` made clear (`_HazeColor` 0.8 grey, `_CoverOpacity` 0, IOR 1.49, depth 3) but for the row's value; see the scene facts. With the incandescent lamp a blue tint stays pale on the lit insert: the lamp has little blue to let through.
- Index of Refraction from `camOffset [0, 0.03, 0.045]`, `frameBoth: false`: low, from behind, along both inserts; the shift shows on the near insert's floor and holes against its frame.

### Lighting (`lighting/`, `l-` prefix)

```sh
sh stills.sh stills/lighting.json && sh stills.sh stills/spill.json && python publish.py l- lighting
```

- Intensity 0 / 0.5 / 1; Output 0.25 / 1 (incandescent) / 3 (LED); Color incandescent / white / red; Lamps 1–3 (bulbs at z −12, 0, +12 mm, radius 5.89 mm); Position Z −12 / 0 / +12 mm; Tint With Lamp with a blue lamp, off / 0.5 / 1. Lamp count and position live in the shared material, so they move the unlit insert's bulbs too; the page says so.
- Spill (Lumens, Range) shows on a ball beside L35: `ball: true`, `ballOffset [0, 0, 0.025]`, `frameBoth: false`, `camOffset [0, 0.016, -0.042]`, `lookOffset [0, 0.012, 0.022]`. The ball is "Metal - Steel Scratched"; the default mirror ball only reflected the insert.

## Preset example stills (T2, `presets/*-on.png`, `*-off.png`)

Not in Pinball/Automation yet: they apply lamp and color presets for good. `automation/unity/PresetStills.cs` does it; copy it into `vpe-terminator2/Assets/Editor`, compile (`scope=all`), then:

```sh
cd automation
sh shoot_presets.sh                                                  # 20 d_*.png in vpe-terminator2/Library/Automation/presets
python publish_presets.py                                            # crops them into presets/
```

and delete `PresetStills.cs` (and its `.meta`) again. Each preset is shown off and on from 5 cm above L35: lamp presets Incandescent and LED, color presets Clear, Red, Green and Blue (L35 then wears that color's library materials; the script puts back Incandescent and Clear, check `color-done.txt`), looks Clear Plastic and Colored Plastic on the clear L35 and on the red L81 (6.5 cm above, a 2 in triangle). There is no `off` list here: L34 above shows lit, as it always has. Crops: `(150,150,750,750)` for L35, `(120,120,780,780)` for L81, both to 320×320.

## Index showcase (LookDev, `inserts-showcase-lit.png`, `inserts-showcase-unlit.png`)

Copy `automation/unity/LookDevStills.cs` into `LookDev~/Assets/Editor`, then with `INSTANCE='LookDev~'`:

1. *Automation Stills/LookDev/Showcase On*: activates the "Insert Showcase" root, which the scene keeps inactive, and hides "Playfield - Inserts" (a board over the middle six inserts) and "Insert Grid, Off".
2. *Pinball/Automation/Showcase/List* (check every insert is Red, Incandescent, intensity 1; *Showcase/Align Colors* makes them so), *Showcase/Shoot Lit*, *Showcase/Shoot Gizmo*. Wait for `lit-done.txt` and `gizmo-done.txt` in `Library/Automation/showcase`. Each renders straight down, orthographic, 8192 wide, once on black and once on white.
3. *Automation Stills/LookDev/Showcase Off*.
4. `python automation/matte_showcase.py`: alpha from the black and white renders. The white comes out tonemapped (about 206) and the bloom leaves a red veil over the black, so the matte normalises by the white background and lifts the alpha floor; the background must end up fully transparent, the glow kept. The outlines are 50% grey (RGB 128) with the rendered alpha, so they read on light and dark themes. Check the previews it writes.

## Inspector shots

Window sizes are in points; "scale" is how much larger than the Inspector the window draws. The row ranges are for the display scale given; re-derive them with `blocks.py` otherwise (start about 8 px above a section's header block, end about 6 px under its last field).

### Body material and cover inspectors (T2)

*Pinball/Automation/Lighting/Shoot Material Copy* shoots a temporary project variant of L35's material (so it isn't greyed out; fields its color preset owns stay greyed out, as intended) in 8 slices of 340×560 at scale 1, 500 points apart, and deletes the copy (check no `Assets/*.mat` is left). The insert comes from the `insert` of `lighting/shots.json` (put `stills/body.json` there, not the red config); `material-slot.txt` 0 is the body, 1 the cover (set it back to 0).

```sh
python automation/stitch.py material "$TEMP/material-tall.png" 340   # cover: stitch.py cover …
python automation/crop.py "$TEMP/material-tall.png" body-material/inspector-plastic.png 31 750
```

At 1.75: body `inspector-plastic` 31–750, `inspector-surface` 753–1072, `inspector-lamp-output` 1081–1186, `inspector-virtual-lamp` 1191–1450, `inspector-glow` 1453–1562, `inspector-facets` 1566–1779, `inspector-baked-data` 1787–2124; cover (`cover-*` slices) `inspector-plastic` 31–285, `inspector-haze` 293–601, `inspector-surface` 611–831. Full width, no resize; shown at `width="340"`. A wider window doesn't help "Room Return Exposure (EV)", which stays truncated: HDRP's label column is fixed.

`cover/inspector-component.png`: *Lighting/Shoot Cover Section* (an unpacked copy of L35, only Clear Cover unfolded, 340×360 at scale 2), rows 172–834 at 1.75, resized to 680 wide.

### Insert Light (T2, `lighting/inspector-*.png`)

*Lighting/Shoot Inspector*: an unpacked copy of L35 at intensity 1, one shot per section (Lamp, State, Spill), 340×520 at scale 2. At 1.75 the rows were lamps 0–740, state 76–696, spill 160–618, resized to 680 wide; `match_crop.py <raw> <published> publish` reproduces them while only the scale changed.

### Presets page inspectors (T2, `presets/inspector-*.png`)

*Pinball/Automation/Presets/Shoot*, `presets/target.txt` = `Starburst - L35` (a contains match).
- `inspector-insert`: the Insert component at 280×360, scale 1 (half width, so the color buttons spread evenly), crop `(0,128,490,598)` at 1.75, no resize.
- `inspector-light`: not the presets menu's Light shot, but the Lamp section from `lighting/inspector-lamps.png`'s raw shot, rows 0–362, resized to 680 wide.
- `inspector-material`: the body material at 560×415, scale 1, with Smoothness nudged so the edited mark shows; published as is.
- `inspector-preset-window-{lamp,color,look}` (720×420, scale 2) and `inspector-lamp-manager` (1050×150, scale 2, settings row open, every light on Incandescent for the shot): `match_crop.py` against the published ones.

### Geometry (LookDev, `geometry/inspector-*.png`)

Copy `automation/geometry-picks.txt` to `LookDev~/Library/Automation/geometry/picks.txt` and run *Pinball/Automation/Geometry/Shoot* (340×640, scale 2, only Geometry unfolded, plain copies without prefab marks, hole values rounded so they fit). The names lie: *Geometry/List* writes the real shapes (Red_001 of the 1.1875in circle is the Arrow, Red_002 the Diamond, Red_003 the Flash); "temp banana" is a generated default banana. Then `python automation/geometry_crop.py` cuts each shape from the top through its last outline field (a field's widget is anything right of 0.45 W: Flash's Mirror checkbox starts left of the middle) and resizes to 680. Body, Holes and Hole Placement: `match_crop.py` of `section-360.png` against `inspector-body.png` and `inspector-holes.png`, of `section-720.png` against `inspector-hole-placement.png`.

### Patterns (LookDev, `patterns/`)

- *Automation Stills/LookDev/Create Patterns Source* (the inactive `Insert, Patterns`, a 47 mm square with a channel), `500` into `Library/Automation/patterns/offset.txt`, *Pinball/Automation/Patterns/Shoot* (one copy per pattern, only its pattern on; the engraved line switches to Flash and scrolls 100 points further), then `match_crop.py section-<pattern>.png inspector-<pattern>.png publish`, and *Delete Patterns Source*. Without the source the menu writes `done.txt` and no image; the console has the exception.
- Wireframes (`wireframe-angled-<pattern>.png`): set the LookDev Scene view to the angle of the published ones (camera at (-0.03, 0.02, -0.03), euler (32.1, 51.1, 0), fov 60), run *Patterns/Export Wireframes From Scene View*, then `python automation/render_wireframes.py`. Only needed when the pattern geometry changes.

### Basics (`basics/`)

- `edit-in-project.png`: LookDev, `Library/Automation/inspector/target.txt` = `Insert, Triangle, 1.1875in, Starburst` (an exact name: a library insert nobody edited, with 3 more alike in the scene, so both buttons show), *Pinball/Automation/Inspector/Shoot Top* (340×230, scale 2, with the title bar, all sections folded, the library locked for the shot). Cut just below the Geometry header. Shown at `width="340"`.
- `toolbox-insert.png`: the Insert button of the toolbox, cut by hand from a screenshot to 136×136. *Pinball/Automation/Toolbox/Shoot* gives the toolbox, but not at this scale.
- `inserts-assetlib.png`, `inserts-unbaked-material.png`, and `inserts-geometry.svg` on the Geometry page, are the user's own.

## Adding images for a new parameter

1. The page's table names the images (`<param>-<value>.png`) and its header the values; the middle one should be the material's own.
2. Body material: add the prefix to `build_body.py` (and to `LIT_ONLY` in `publish.py` if it only acts on the lit insert), rebuild `stills/body.json`, shoot, publish. Cover and lighting: add stills to `stills/cover.json` or `stills/lighting.json` with the same view as their neighbours.
3. If the parameter doesn't show in the stills, say so to the user rather than publishing three identical images; a parameter that needs another one to show gets that one set too, and the page says what it was shown at.
4. Reshoot the material inspector slices and re-cut the sections whose rows moved.
