---
uid: insert_traced_mode
title: Insert Traced Mode
description: The Insert Hybrid shader's opt-in Traced mode, which lights the body from an offline path-traced render of the insert.
---

# Insert Traced Mode

*Traced Mode* is an opt-in mode of the *Pinball/Insert Hybrid* body material. With it on, the body's glow comes from an offline, path-traced render of the insert instead of the optical response map. The bulb image and the facets' glints are still drawn live, through a facet map from the same render. With the lamp off, a second render of the insert under the room replaces the live unlit body. Off, the material is the live one, unchanged.

Everything in the [body material](body-material.md)'s *Lamp* section stays in charge: color, brightness, the lamp drive and the Insert Light component work as without Traced Mode. Traced Mode only changes where the body's light comes from.

Both pipelines draw it from the same material. HDRP shows a *Traced Mode* section after *Baked Data*, and URP shows a *Traced Mode* foldout after the graph's controls. Its controls only show while the mode is on.

## What it needs

The maps come from rendering one insert on its own, outside Unity. VPE doesn't make them yet: the [insert baker](baking.md) bakes the live mode's maps only. Import all three as linear (no sRGB), *Traced Light* and *Lamp-Off Env* as HDR (EXR), all with mipmaps so a small insert doesn't shimmer.

- *Traced Light* (`_TracedLight`): what the eye sees through each point of the top with the lamp lit, rendered from four view elevations, one per channel. A linear HDR texture.
- *Traced Facets* (`_TracedFacets`): the facet normals of the same render, X and Z in red and green, the faceted floor in alpha. It needs mipmaps.
- *Lamp-Off Env* (`_TracedEnv`): the same insert with the lamp off under the room, the four elevations in a 2×2 atlas (15°, 45° on top, 60°, 70° below), multiplied by the body mask. Optional: without it, leave *Lamp-Off Look* at zero and the unlit body stays live.

The body is shaded as a flat card at the top of the insert: the cover's refraction is undone, so the traced light doesn't move with the mesh's facets.

## Bake

- *Frame* places the render's square in insert space: its center X and Z in meters, its turn about Y in degrees, its width in meters.
- *Elevations (deg)* are the four view elevations the channels of *Traced Light* were rendered from, off vertical.
- *Depths (mm)*: X is the cover's slab thickness, whose refraction the card undoes; Y is how deep the facets sit under the top; Z is how deep the glow's parallax reaches; W is how far in from the rim the traced light fades out.

*Alignment View* (in *Bulb*) shows the render's square on the insert as a 2 mm grid, red outside it and the traced light in green. Use it to set *Frame*.

## Look

- *Glow* is the traced glow's strength, in units of *Baked Scatter*: at one, it is as bright as the live glow would be.
- *Glow Spread* at one is the render as baked. Higher values lift the dim reaches toward the bright core, so the glow fills the insert instead of pooling over the bulb; lower values tighten it.
- *Live Glow Mix* is how much of the live soft and edge glow comes back in place of the traced one.
- *Glints* is the strength of the facets' highlights. A wide highlight is a dim one, so this sets how bright they are whatever their shape.
- *Glint Shape*: X is the facet tilt in degrees; Y is the angle sensitivity, how small a turn of the eye moves a highlight across a facet; Z is the number of highlights; W is the color fringe in degrees.
- *Glint Turn (deg)* turns the highlights about the insert.
- *Glint Organic*: X is how many degrees the molding's facets waver, so glints slide instead of switching whole facets; Y is how far each insert's highlights turn either way, seeded from where it sits, so a row of the same insert doesn't flash in step; Z is the waviness' wavelength in millimeters.

## Lamp Off

- *Lamp-Off Look* is how far the lamp-off render takes the place of the live unlit body: zero keeps the live body, one is the render as baked. The lamp's glow adds over it.
- *Lamp-Off Vary* lets each insert read the render a little differently, seeded from where it sits: X degrees of elevation, Y brightness as a fraction either way, Z a refraction wobble in texels. Zero is the render exactly, the same on every insert.
- *Lamp-Off Glints* is the room glinting in the facets with the lamp off. It is as strong as the room light the render shows there, with the glints' own shape and variation.

> [!NOTE]
> **The lamp-off brightness is a global.** `_InsertTracedOffEv`, in stops, scales every traced insert's lamp-off render to the table's room. Unset, it is 0, which shows the render at the brightness of the room it was rendered in. Set it when the table loads with `Shader.SetGlobalFloat("_InsertTracedOffEv", stops)`.

## Bulb

- *Through Traced Facets* at one draws the bulb through the traced facets, from the card; zero keeps the live material's own bulb image.
- *Gain* is the bulb's strength through the facets, on top of *Strength*.
- *Halo* is how much of the bulb's broad halo, the part that moves with the eye, comes through with its tight image.
- *Depth* scales how deep the bulbs sit for their image: more depth, more parallax.
- *Mesh Facet Normals* at one lights the body with the live mesh's facet normals. Zero gives it a flat polished top, as a real insert has, and leaves the facets to the traced maps.

> [!NOTE]
> **Shader properties.** The mode is the `_INSERT_TRACED` keyword. The controls use `_TracedLight`, `_TracedFacets`, `_TracedFrame`, `_TracedAngles`, `_TracedGeometry`, `_TracedGlow`, `_TracedGlowSpread`, `_TracedLiveMix`, `_TracedGlint`, `_TracedGlintParams`, `_TracedGlintRot`, `_TracedGlintOrganic`, `_TracedEnv`, `_TracedEnvGain`, `_TracedEnvVary`, `_TracedOffGlint`, `_TracedBulbSource`, `_TracedBulbGain`, `_TracedBulbHalo`, `_TracedBulbDepth`, `_TracedMeshNormals` and `_TracedDebug` (*Alignment View*). The shader code is `InsertTraced.hlsl`, next to the graph.
