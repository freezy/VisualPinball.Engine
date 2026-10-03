---
uid: insert_body_material
title: Insert Body Material
description: Every Insert Hybrid shader control for plastic, surface reflections, virtual bulbs, baked glow, and starburst variation.
---

# Insert Body Material

The **Visual Pinball/Insert Hybrid** material renders the inside of an insert. Its **Plastic**, **Lamp**, and **Baked Data** sections separate the unlit body, the lamp shining through it, and the maps that describe its geometry. These controls update immediately and do not require another bake.

This page uses the material inspector's labels. Defaults are the shader's; a library material or a look preset sets different values. Controls are disabled where the shader would ignore them.

## Plastic

Start with every lamp off. Choose how dark the colored body should be, or how much of a bright cavity should show through clear plastic, before setting the lit brightness.

| Parameter | Shader default / range | Effect |
| --- | --- | --- |
| **Tint** | RGB (0.16, 0.13, 0.25) | Plastic color multiplying the unlit body, including its clear share. It does not color the lamp emission. Use Insert Light Color and Lamp Emission for the lit color. |
| **Clarity** | 0; 0–1 | Blends from colored plastic at zero to the clear cavity appearance at one. Values between them blend both looks. |
| **Cavity Color** | RGB (0.9, 0.9, 0.9) | Color of the board or reflector underneath clear plastic, still multiplied by Tint. Available when Clarity is above zero. |
| **Cavity Spread** | 4; 1–8 | Width of the bright cavity around a virtual lamp, measured in lamp radii. Larger values make clear starburst ribs more even. Needs a clear starburst with nonzero Cavity Contrast. |
| **Cavity Contrast** | 0.3; 0–1 | How much darker a clear starburst face becomes when its refracted view lands beyond the bright cavity. Zero removes that variation. |
| **Min Brightness** | 0.04; 0–1 | Amount of the colored share visible under room light when the first lamp is off. It blends toward the full authored Tint as that lamp's intensity rises. Zero removes that diffuse contribution; one leaves it undimmed. It does not keep lamp emission or spill on. |

<!-- Screenshot: material-plastic. Separate Tint, Clarity 0/1, Cavity Color dark/light, Cavity Spread 2/6, Cavity Contrast 0/0.5, and Min Brightness 0.04/0.4 comparisons, with lamps off. -->

> [!NOTE]
> **Clear is an appearance, not transparency.** The hybrid body is opaque. Its clear look approximates the ambient-lit board or reflector viewed through plastic, using the baked response and virtual lamp positions. It does not sample a real object behind the insert or the screen color. The optional cover supplies the transparent top layer.

## Surface

| Parameter | Shader default / range | Effect |
| --- | --- | --- |
| **Smoothness** | 0.93; 0–1 | Sharpness of reflected lights and probes. A high value is glossy; a low value broadens the highlights. With a cover, the top can be glossy while the body stays duller. |
| **Surface Normal Strength** | 0.15; 0–2 | Strength of the baked surface normal map in reflections, including edge rounding and baked grain. Zero removes its contribution. Needs a Surface Normal map and Facet Flatten below one. |
| **Surface Grain** | 0; 0–1 | Fine texture of the molded plastic in its reflections, drawn procedurally in insert space. It stays sharp at any zoom and any map size, so prefer it over the bake's Grain Strength, which it replaces; it fades out where its cells fall below a pixel. Variation Seed picks another pattern. |
| **Surface Grain Size (mm)** | 0.75; 0.1–5 | Size of the grain's cells. The bake's grain measured 1.5 VPX units, about 0.75 mm. Needs nonzero Surface Grain. |
| **Facet Flatten** | 0; 0–1 | Turns the reflection shading normal toward the flat top normal. Zero retains facets; one fully flattens their reflection shading. Values around 0.85 can soften the strong alternating reflection ribs of a clear starburst. It leaves optical refraction intact. |
| **Grain** | 0.25; 0–1 | Fine procedural variation multiplying the body's color and emission. Zero is uniform. Variation Seed selects another distribution. This does not add normal-map roughness. |

For a starburst, begin with a modest Surface Normal Strength because the mesh already has tilted facets. Stipples and engraving have a plain displayed floor and rely on their surface map for reflection relief. Setting Facet Flatten to one also suppresses that mapped relief in the reflection normal.

<!-- Screenshot: material-surface. Smoothness 0.4/0.93, Surface Normal Strength 0/0.3, Facet Flatten 0/0.85, and Grain 0/0.5. Include a recognizable reflected light. -->

> [!NOTE]
> **Two normals, two jobs.** Surface Normal is tangent-space data used by lights and probes. Optical Normals store sharp directions in insert-local space for the virtual bulb. Surface Normal Strength and Facet Flatten do not change optical direction; Optical Facet Strength does not change surface reflections. Baked grain affects the first map only.

## Lamp output

| Parameter | Shader default / range | Effect |
| --- | --- | --- |
| **Max Brightness** | 1; unrestricted numeric field | Multiplies emission and the Insert Light's spill output at full lamp intensity. Zero makes both outputs zero. Values such as 10, 20, or 50 can be useful depending on the scene's exposure. |
| **Lamp Emission** | HDR RGB (600, 600, 600) | Nominal emission color and magnitude. It multiplies every lamp's Color. A neutral emission lets RGB lamps supply their own hues; a colored emission filters those hues. |

Max Brightness is shared by the material; each lamp's **Intensity** is a normalized per-insert drive. Increasing the material brightness therefore changes the maximum output of every insert using it. Lamp Emission's magnitude controls the rendered glow, while spill lumens have their own calibration; its hue also colors the spill.

<!-- Screenshot: material-output. Max Brightness 1/20 at fixed exposure; neutral/colored Lamp Emission with the same lamp Color. -->

> [!TIP]
> Keep the camera exposure fixed while tuning or comparing brightness. Auto-exposure can hide an increase by darkening the rest of the table. These emission values are artistic radiance controls, rather than a calibrated conversion from physical bulb wattage.

## Virtual lamp

| Parameter | Shader default / range | Effect |
| --- | --- | --- |
| **Bulbs** | 1; 1–4 | Active bulb count. This is the same setting as Number of lamps on Insert Light. Each active lamp has a position and an independent runtime color/intensity. |
| **Position (mm)** | First lamp: (0, −15, 0), before bake suggestions | First lamp's insert-local X/Y/Z position. X/Z move the image sideways; lowering Y places it deeper and changes parallax. |
| **Bulb 2 / 3 / 4 (mm)** | Inactive until enabled | Positions of the additional lamps, using the same local coordinates. All active lamps share the first lamp's radius. |
| **Radius (mm)** | 8 before bake suggestions; minimum 0.1 | Size of the soft bulb image. A larger radius broadens the moving hotspot and also increases the physical extent represented by Cavity Spread. |
| **Strength** | 1.5; 0–5 | Contribution of the virtual bulb image. Zero removes that image, leaving the baked glow controls available. |
| **Bulb Definition** | 1.5; 0–5 | Strength of a tighter bulb image inside its soft halo. Zero leaves only the halo. Needs nonzero virtual lamp Strength. |
| **Bulb Warmth** | 0; 0–1 | Colors the bulb image as a filament's warm white instead of the Lamp Emission color, at the emission's peak brightness. A red insert's bulb then clips to yellow, as on a photograph, while the glow around it stays red; the cover's tint still colors it. Needs nonzero Bulb Definition. |
| **Index of Refraction** | 1.49; 1.01–2 | Strength of refraction through the plastic. It shapes the virtual lamp image and the clear cavity view. |
| **Optical Facet Strength** | 1; 0–3 | Scales the optical normals' sideways tilt. One follows the baked directions; zero removes the facet bending. It also influences starburst contrast and the clear cavity view. |
| **Lamp Spread (mm)** | 0; 0–100 | Distance at which each lamp's spatial falloff reaches half its output. At twice that distance it reaches one fifth, and at three times one tenth. Zero disables this distance falloff, allowing the bulb to reach the whole body. Also controls the reach of lamp tint. |

Using the hybrid material starts the first lamp from the bake's suggestion: its radius is 40% of the insert's local X extent, with a minimum of 1 mm. Its center is below the lowest point by the larger of 1.5 times that radius or 5 mm. These are starting values; tune the position and radius for the intended bulb afterward.

<!-- Screenshot: material-virtual-lamp. Compare radius, depth, lateral placement, Strength, Bulb Definition, IOR, Optical Facet Strength, and Lamp Spread separately. -->
<!-- Diagram: material-spread. Falloff curve annotated at one, two, and three spread distances; distinguish Lamp Spread from spill Range. -->

> [!NOTE]
> **Refraction and parallax.** The shader refracts the view ray through a flat entrance and the patterned surface, then evaluates a virtual bulb below the insert. Its image shifts with the viewpoint and is evaluated independently for each eye. No screen texture is needed. The source geometry and neutral optical maps stay fixed while position, radius, and refraction are tuned live.

## Glow

| Parameter | Shader default / range | Effect |
| --- | --- | --- |
| **Baked Scatter** | 0.1; 0–2 | Broad internal illumination from the response map's cavity channel. More makes a fuller glow; too much obscures the pattern's contrast. |
| **Edge Glow** | 0.035; 0–3 | Illumination weighted by the baked rounded-edge response, including rims and hole walls. High values can turn the boundaries into a bright outline. |

Both need nonzero Max Brightness and lamp drive. They supplement the virtual bulb image; they remain useful when its Strength is zero.

<!-- Screenshot: material-glow. Strength fixed; Baked Scatter 0/0.5 and Edge Glow 0/0.2 compared separately. -->

## Facets

These controls use the baked starburst sector metadata to align variation with the fan. They are disabled when there is no starburst, and need a visible lamp or scatter contribution.

| Parameter | Shader default / range | Effect |
| --- | --- | --- |
| **Roughness** | 0; 0–1 | How rough the structure inside the plastic is: the facets, channels and holes the light comes up through. Zero gives crisp ribs and rays; one diffuses them, softening the ribs and sending the rays and the bulb's image into the glow. The finish of the top is the cover's. |
| **Facet Contrast** | 0.8; 0–1 | Difference in light coupling between opposing starburst slopes. Zero is even; one makes strongly alternating ribs. Around 0.25 can suit clear plastic. |
| **Mottle Amount** | 0.4; 0–1 | Cloudy variation across the ribs and small differences between sectors. Zero removes those variations. |
| **Mottle Size (mm)** | 1.4; 0.2–5 | Spacing of the cloud cells. Smaller is finer. Enabled when Mottle Amount is nonzero. Fine detail fades when it becomes smaller than a pixel. |
| **Variation Seed** | 7; unrestricted numeric field | Selects another mottle, sector variation, and procedural Grain pattern. This is not a brightness setting. It remains useful without a starburst when Grain is nonzero. |

<!-- Screenshot: material-variation. Facet Contrast 0/0.8, Mottle Amount 0/0.7, Mottle Size 0.4/3, Variation Seed 7/23. -->

## Baked Data

**Bake Again** owns these values and keeps them aligned with the body mesh. They are exposed to recover an insert's own maps after accidental material copying, or for a deliberately authored custom material. Copying another insert's maps can break the alignment even if the outlines look similar.

| Parameter | Meaning |
| --- | --- |
| **Surface Normal** | Tangent-space normal texture used for surface reflections. |
| **Optical Normals** | Texture of sharp directions in insert-local space. |
| **Optical Response** | RGB cavity, edge, and transmission data described on [Baking](baking.md#output). |
| **Starburst Sectors** | Sector count, normally twice the generator's Bursts. Below four the starburst-specific variation is inactive. |
| **Starburst Centre (mm)** | X/Z fan center in insert-local millimeters. It must match the generated starburst center. |

Do not retile these maps independently. The body's final UVs, tangents, and all three maps belong to one bake. Generated lamp suggestions also start from that insert's dimensions; they are then free to be tuned as material settings.

<!-- Screenshot: material-baked-data. The three maps and the Baked Data Inspector; use correct/mismatched assets only as an explanatory example. -->

> [!NOTE]
> **Shader properties.** The controls use `_BaseColor` (Tint), `_InsertClarity`, `_InsertCavityColor`, `_InsertCavitySpread`, `_InsertCavityContrast`, `_InsertMinBrightness`, `_Smoothness`, `_NormalStrength`, `_InsertFacetFlatten`, `_OpticalGrain`, `_InsertLuminosity`, `_EmissiveColor`, `_OpticalLampGain`, `_OpticalBulbDefinition`, `_OpticalIor`, `_OpticalWarp`, `_OpticalSpread`, `_OpticalScatter`, `_OpticalEdgeGlow`, `_OpticalContrast`, `_OpticalVariation`, `_OpticalMottleSize`, `_OpticalSeed` and `_InsertRoughness`. The names matter when editing look preset entries; the inspector shows the labels.

> [!NOTE]
> **Generated and runtime properties.** `_NormalMap`, `_OpticalNormalMap`, and `_OpticalResponseMap` hold the textures. `_OpticalLamp`, `_OpticalLampB`, `_OpticalLampC`, and `_OpticalLampD` store local XYZ and radius W in meters; an additional bulb with radius zero is inactive. `_OpticalStarburst` stores center X/Z in X/Y, sector count in Z, and lowest fan height in W, with positions in meters. Insert Light supplies `_InsertLampLevel` and linear `_InsertLampColor` for the first lamp, `_InsertLampColorB/C/D` as linear RGB plus intensity in W for the other lamps, and `_InsertLampTint` for the tint blend. Edit those runtime values through Insert Light rather than through the material asset.

## HDRP surface options

The material retains HDRP's standard Surface Options and Advanced Options. The supplied body uses an opaque surface, while the separate cover uses transparency. The custom Smoothness and surface normal controls shape the body's standard HDRP lighting and reflections; lamp emission is added alongside them. General table lighting, reflection probes, exposure, and post-processing still affect the final appearance.
