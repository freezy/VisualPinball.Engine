---
uid: insert_baking
title: Baking Inserts
description: Configure Blender, optimize UVs, bake surface and optical maps, and manage project-owned insert assets.
---

# Baking Inserts

Baking turns the insert's generated detail into maps that the material can read quickly: Blender turns the source geometry into textures. Stipples bake from the detailed pyramids onto a plain mesh, an engraved line is replaced by a plain middle after baking, and starburst facets stay in the mesh. The bake writes the surface normals for the reflections and separate optical maps for the lamp, optimizes the UV layout and supplies the tangent frames needed to read the surface normal map. Work through the *Normal Bake* section of the Insert component after fitting the geometry and pattern.

> [!NOTE]
> **Baking needs Blender 5.0 or newer.** VPE runs [Blender](https://www.blender.org/download/) in the background to bake, and **there is no bake without it**: *Bake* stays disabled, and an insert you created or changed keeps its unbaked mesh and material. Install it from blender.org; VPE ships its own bake script, so no add-on or Blender setup is needed.
>
> Blender is only needed where inserts are baked. The bake is saved as assets in your project and packed into the .vpe file, so the table renders in the editor, in builds and in the player without Blender. Someone who opens your table needs Blender only to change an insert's geometry or bake settings, and the baked inserts of the asset library can be placed and tuned without it.

## First bake

Check the *Blender* field. VPE looks for the newest Blender 5 under `Program Files\Blender Foundation` on Windows, in `/Applications/Blender.app` on macOS, and as `blender` on the `PATH` on Linux. Anywhere else, choose the executable with the `…` button. The path is an editor setting of this machine, shared by all inserts and projects, rather than something stored in each table. VPE starts Blender once to check its version and whether UVPackMaster is installed, and the *Normal Bake* section reports what it found, or why Blender can't be used. Blender then runs in the background, one insert at a time.

Keep *Bake Hybrid Optics* enabled for the hybrid lamp material. Click *Bake*, then use *Use HDRP Hybrid Material* if the current body material needs replacing. *Bake Hybrid and Use Material* on the Insert Light component combines those actions. A new insert with an Insert Light component and no body material receives the hybrid material automatically on its first bake.

<!-- Screenshot: baking-inspector. Normal Bake before the first bake and after a successful bake, including settings, progress, and the UV fill readout. -->

## Bake settings

| Parameter | Default / choices | Effect |
| --- | --- | --- |
| *Texture Size* | 1024; 256 to 4096 | Width and height of the baked surface normal map in pixels. The creases between facets are seams in the bake, so they stay as sharp as the mesh at any size; size the map for the rounded edges and walls instead: 512 suits most inserts and 256 the small ones. Doubling each dimension uses four times as many texels. |
| *Optical Normals* | Same size; same size to an eighth | Size of the optical normal map as a fraction of *Texture Size*, shown in pixels. Every facet is an island of its own, so a small map runs out of texels for the islands before it runs out o f detail; halve it only on inserts with few facets. |
| *Light Response* | Half; same size to an eighth | Size of the light response map as a fraction of *Texture Size*, shown in pixels. It holds a soft glow and smooth masks, which need fewer texels than the creases; its blur scales with it, never under a texel and a half, so the glow stays smooth. A quarter leaves the facet islands too few texels on a 512 map. |
| *Padding (px)* | 8; 1–32 | Extension around each UV island in the normal map. Islands are packed at least twice this distance apart so their padding does not meet; the smaller maps keep the same distance in their own pixels. More padding helps filtering at a distance but leaves less space for the surfaces themselves: every facet is an island of its own, so the UV fill readout drops with many facets at a small size, which costs nothing while a facet's texels all hold the one direction. |
| *Edge Bevel* | 1 | Width of rounded sharp edges in the surface normal bake, in VPX units. Zero keeps them sharp. This affects reflection detail rather than adding bevel triangles. |
| *Grain Strength* | 0; 0–1 | Amount of fine texture in the body's baked surface normals. Zero is smooth and disables *Grain Size*. The hybrid material's *Surface Grain* draws the same grain at any zoom without a bake and without the texels a baked grain needs; keep this at zero with it. |
| *Grain Size* | 1.5; minimum 0.1 | Size of that baked texture in VPX units. Smaller makes finer grain. It affects surface reflections, rather than the optical lamp or cover. |
| *Unwrap in Blender* | Enabled | Unwraps along the generator's charts before packing. Disabled keeps the generator's UV shapes and repacks them. |
| *Prefer UVPackMaster* | Enabled | Uses UVPackMaster when installed in Blender. Otherwise Blender's own packer is used; the add-on is optional. The Inspector reports the available packer. |
| *Bake on GPU* | Disabled | Uses the GPU configured in Blender's preferences instead of the CPU. Configure Blender's Cycles device before enabling it. |
| *Bake Hybrid Optics* | Enabled | Produces the optical normal and response maps in addition to the surface normal map. Required for the Insert Hybrid material's internal light and cavity appearance. |

Once a baked hybrid material is in use, rebaking also updates its optical maps so they stay aligned with the current atlas. Changing bake settings does not automatically mark the geometry bake stale. Press *Bake Again* to apply them even if the current status still says *Baked*.

<!-- Screenshot: baking-settings. Separate two-value comparisons for resolution, padding, edge bevel, grain strength/size, and unwrap. Compare the UV atlases for packer settings; show CPU/GPU as configuration states. -->

> [!NOTE]
> **Three kinds of fine detail.** *Bottom Bevel* is body geometry. *Edge Bevel* and *Grain Strength*/*Size* are baked reflection normals. The material's *Grain* adds procedural color and emission variation at rendering time, and its *Surface Grain* the procedural reflection grain that replaces the baked one. The cover's *Normal Map* is another independent surface layer. Each has its own purpose and should be tuned in the place that owns it.

## Output

VPE stores generated assets in an insert-specific folder under `Assets/Inserts`, named with the insert and an identifier. Subsequent bakes update that insert's owned assets and preserve its material tuning.

| Output | What it stores | How the shader uses it |
| --- | --- | --- |
| **Normal Map** | Tangent-space surface normals, including rounded edges and fine grain | Reflections, controlled by *Surface Normal Strength* |
| **Optical Normals** | Sharp surface directions in insert-local space, encoded into RGB | Virtual lamp refraction, independently of UV chart rotation and reflection strength |
| **Optical Response** | R: soft cavity illumination; G: rounded-edge response; B: transmission mask | *Baked Scatter*, *Edge Glow*, and the lamp's transmission weighting |
| **Bake asset** | Source hash, final per-corner UVs and tangents, texture references, packer information, and material | Applies the fitting bake to the displayed body and restores editable data on import |
| **Body material** | The chosen shader and its map references | Renders the finished insert |

<!-- Diagram: baking-maps. Surface normal, optical normal, and response channel thumbnails beside the final rendered result. -->

> [!NOTE]
> **Optical background.** Blender lights a neutral cavity proxy and bakes broad illumination, sharp directions, and edge response. Lamp color is applied later in Unity, so runtime RGB changes need no new bake. The runtime shader uses analytic virtual bulbs and refracted rays; this is an artistic approximation of light passing through plastic, rather than a full simulation of transmission, thickness-dependent absorption, and caustics.
>
> **Texture encoding.** The surface normal map is an 8-bit PNG. Optical Normals and Optical Response are 16-bit linear data PNGs, imported uncompressed and without sRGB. The baker supplies the import settings; replacing or reimporting those maps should preserve their encoding.

## UVs and pattern geometry

The generator supplies UV charts for the floors and walls. The baker can unwrap them and packs walls at lower density than the floors. The status shows the packer and the percentage of texture occupied by charts. A higher fill is useful, but enough resolution and separation matter more than filling every pixel.

An engraved line keeps a projection from above so a plain middle can carry its baked detail. A stippled insert sends Blender a separate no-pattern target, which owns the final atlas while the pyramids provide the high-detail source. Starburst facets remain in the displayed mesh. See [Patterns](patterns.md) for the resulting geometry.

If an unwrap folds triangles, the baker falls back to the generator's UV layout. If the packed layout overlaps, the bake stops with an error so two surfaces cannot write into the same texel.

## What needs a new bake

| Change | Bake again? |
| --- | --- |
| Shape, dimensions, corners, body depth, channel, bottom bevel, holes, or pattern geometry | **Yes**, when it changes the generated source mesh |
| Texture size, padding, edge bevel, baked grain, or unwrap settings | **Yes**, to see the new settings; changing them alone does not mark the current bake stale |
| Body material settings, presets | **No** |
| Lamp count, placement, radius, color, intensity, or spill | **No** |
| Cover material, haze, smoothness, normal strength, playfield normals | **No** |

> [!TIP]
> Fit the shape and pattern first, bake once, then tune the material and lamps through the game camera. Material changes show immediately.

## Stale bakes

A fitting bake matches the generated source mesh. Changing geometry invalidates that match; VPE does not apply stale per-corner UVs or tangents to a different mesh.

With an existing stale bake, the normal renderer is hidden in edit mode and the Scene view shows the current source as yellow translucent geometry with contour and wall wires. This preview makes it possible to tune the source before another bake. Unity's global *Wireframe* drawing mode can still show every triangulation edge; switch back to shaded mode to judge the insert's own preview.

The renderer returns when a new bake fits. Play mode and builds render the available mesh even when the authoring bake is stale, so finish the bake before evaluating or exporting the final appearance.

<!-- Screenshot: baking-stale. Fitting shaded insert versus the yellow source preview after a geometry edit. -->

## Actions

| Action / readout | Meaning |
| --- | --- |
| *Bake* / *Bake Again* | Generates UVs, tangents, the normal map, and requested optical maps. Available outside Play mode when the insert and Blender are valid and its bake is writable. |
| *Fork to Project* | Copies a read-only bake, one from a package or a locked asset library, and its maps to this project's Assets/Inserts and derives writable materials. If the copied bake is stale, it starts baking the changed geometry. |
| *Clear Bake* | Removes the bake assignment and restores the original unbaked body material. It leaves the baked assets on disk. |
| *Use HDRP Hybrid Material* | Creates and assigns a hybrid material from a fitting bake with optical maps. The previous material asset remains available. |
| *Copy Blender's Log* | Copies output after a bake failure for diagnosing Blender or device configuration problems. |
| *Packer* / *UV fill* | Shows how the last successful bake laid out the atlas and how much of it is occupied. |

> [!NOTE]
> **Duplicated inserts.** A duplicate can initially reference the original's fitting bake. Each insert has its own identifier, so baking the duplicate creates or uses its own assets rather than overwriting another insert's bake. Shared material settings still affect all users of that material; material ownership and bake ownership are separate concerns.

## Troubleshooting

Correct any geometry error first. For Blender failures, check the executable and reported version, then copy the log. If GPU baking fails, verify the Cycles device in Blender or try the CPU. If UVPackMaster is unavailable, turn off its preference and use Blender's packer. A failed bake keeps the previous bake, allowing you to correct the cause and try again.
