---
uid: insert_cover
title: Insert Cover
description: Add a transparent top, tune haze and reflections, and project the playfield's normal detail continuously across an insert.
---

# Insert Cover

The body supplies the molded interior and its optical appearance. A **clear cover** closes the opening at the rim with a flat top, adding its own reflections, haze, and surface detail. It is optional and independent of the body bake, so you can add or tune it without baking again.

## Add the top surface

In **Insert → Clear Cover**, assign **Material** or click **Add Clear Cover**. The helper creates a **Visual Pinball/Insert Cover** material with a subtle haze and glossy finish. Clearing Material removes the top surface.

| Component parameter | Meaning |
| --- | --- |
| **Material** | Material assigned to the generated top surface. It occupies the renderer's second material slot. Any material can be assigned, but the dedicated cover shader supplies the haze, lamp tint, and playfield-normal features described here. |
| **Roughness** | How worn this insert's top is, from 0 to 1. Zero is the cover material's finish; one a sanded, scuffed top whose reflections of the lights are a blur and which veils the pattern a little, a tenth of haze on top of the material's Opacity. It is this insert's own, written per renderer, so inserts sharing one cover material can be worn differently: on a real playfield the inserts in the ball's path are the rough ones, and the bottom of an insert, just above the bulb, stays sharp whatever the top looks like. |
| **Use Playfield Normals** | Projects the detected playfield's normal detail across the cover. Available for the dedicated Insert Cover material. Disabled uses the cover material's own map and local UVs. |
| **Playfield Override** | Optional Mesh Renderer supplying the normal map and UV projection. Leave empty for automatic playfield detection. |
| **Detected Surface** | Readout identifying the resolved source renderer. |
| **Source Material** | Readout of that source renderer's first material. |
| **Source Normal Map** | Readout of the normal map supplied by that material. |

<!-- Screenshot: cover-component. Cover absent/present, then Use Playfield Normals disabled/enabled with source readouts. -->

> [!NOTE]
> **Separate material slot.** The cover is added after the body's bake has been applied. Its triangles, UVs, and per-slot material overrides do not change the body's bake hash or atlas. A custom cover material determines its own transparency and shadows; the dedicated cover keeps reflections visible at low opacity and does not cast a solid shadow.

## Haze

Open the cover material's **Haze** section to tune the veil seen over the interior. It can be nearly invisible, lightly worn, or milky.

| Parameter | Shader default / range | Effect |
| --- | --- | --- |
| **Opacity** | 0.01; 0–0.5 | Amount of haze blended over the body. The default gives a clean top with very little veil. Values around 0.05–0.1 soften the interior like worn or slightly frosted plastic. Specular reflection strength is preserved as opacity changes. |
| **Haze Color** | RGB (0.8, 0.8, 0.8) | Color of the veil. White brightens it, a darker gray dulls it, and a colored haze filters its appearance. Disabled at zero Opacity. |

With **Tint With Lamp** enabled on Insert Light, the haze can take the local lamp hue while preserving its brightness value. Without that tint, a gray veil can make a saturated lamp look pale. See [Lighting](lighting.md#tint-the-body-and-cover-with-the-lamp).

<!-- Screenshot: cover-haze. Opacity 0.01/0.1, then Haze Color light/dark with opacity fixed. -->

## Surface reflections and normal detail

| Parameter | Shader default / range | Effect |
| --- | --- | --- |
| **Smoothness** | 0.96; 0–1 | Sharpness of lights and probes reflected in the top. Match the surrounding playfield for a highlight that continues across the insert. |
| **Normal Map** | Unassigned | Tangent-space scratches or fine texture on the cover. Without playfield projection, it uses local 0–1 UVs spanning the top. |
| **Tiling** | (1, 1) | Repeat scale of the material's own normal texture. Disabled when no map is assigned. |
| **Offset** | (0, 0) | Shift of that texture within its local cover UVs. |
| **Normal Strength** | 1; 0–2 | Strength of the cover detail. Zero is flat. With playfield projection, it multiplies the source material's normal strength; one matches the source. |

The cover's material Inspector reports when inserts in the open scene use playfield normals instead of its own map. In that case the source supplies the effective texture and projection; the cover's Smoothness and Normal Strength still apply.

<!-- Screenshot: cover-surface. Smoothness 0.4/0.96, map absent/present, Tiling 1/4, Offset 0/0.25, and Normal Strength 0/1. -->

## Follow the playfield surface

Enable **Use Playfield Normals** when scratches, wood grain, or other top-surface normal detail should continue across the insert boundary. VPE detects a surface under the enclosing Playfield, or under the single Playfield in the same Table, and can choose the largest suitable imported surface. Use **Playfield Override** when the intended surface is not the detected one.

The source must have a readable mesh with a planar top and usable UV0 in its first submesh. Enable **Read/Write** on an imported mesh if needed. Its first material needs a supported normal map; the projection supports HDRP Lit tangent-space normal maps and follows the source base-map tiling, offset, and normal strength.

> [!NOTE]
> **Projection rather than matching tiling by eye.** VPE derives the source mesh's actual XZ-to-UV mapping, including rotations, flips, and margins. The cover receives matching UVs and tangent frames, while a per-renderer override supplies the source normal texture and strength. Neither shared material is rewritten. Moving either object updates the projection in the authoring scene.

<!-- Diagram: cover-projection. One scratch crossing playfield and cover, with the source UV transform and cover's matching projection highlighted. -->

The source texture needs detail over the insert area for anything to appear there. Matching normal detail does not also match smoothness: tune the cover's Smoothness to the playfield's effective mask-map smoothness for a continuous highlight.

Disabling projection restores the local cover UVs and its own material settings. If detection or the source becomes invalid, the Inspector reports the problem and falls back to those material settings. Check Detected Surface, Source Material, and Source Normal Map before trying to compensate with cover tiling.

## Shader data reference

> [!NOTE]
> The authored cover properties are `_CoverOpacity`, `_HazeColor`, `_Smoothness`, `_CoverNormalMap` (including its texture tiling and offset), and `_CoverNormalStrength`. `_PlayfieldNormalStrength` is supplied by the component for a detected source and defaults to one for a local map. Insert Light also supplies `_InsertLampLevel`, `_InsertLampColor`, `_InsertLampTint`, `_OpticalLamp/B/C/D`, `_InsertLampColorB/C/D`, and `_OpticalSpread` to this material slot. Their encodings match the [body's runtime data](body-material.md#baked-data). They let the haze follow the same spatial lamp mix; the cover itself emits no lamp light.
