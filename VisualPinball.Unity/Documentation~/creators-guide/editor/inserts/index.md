---
uid: inserts
title: Inserts
description: Choose, light, and customize pinball inserts, with generated shapes, optical materials, surface patterns, and editable lamps.
---

# Inserts

An insert is the small plastic window in the playfield that lights up to show a shot, award, or game state. Its appearance comes from several things working together: the shape of the plastic, the molded detail underneath, the finish on top, and the lamps shining through it. VPE lets you tune each of those parts without modeling the whole insert by hand.

Most standard inserts will be available in the **Asset Library**. Start there when a familiar shape and size will do: place the insert, choose its color, and connect its lamps. The generator is there for unusual shapes, dimensions, and surface details, or for adjusting a library insert to match a particular table. [Customizing Inserts](customizing.md) walks through both workflows.

<!-- Visual: overview-family. A lineup of the eight shapes, followed by the same insert unlit and lit. See visuals.md. -->

## What you can make

The generator supports **Triangle, Rectangle, Circle, Banana, Diamond, Flash, Bullet, and Arrow** outlines, plus **Custom** outlines supplied by a mesh. Rounded corners, a recessed body, a surrounding channel, a bottom bevel, and holes give them the structure of molded inserts. The top-view preview helps you fit that structure before baking it.

Choose a plain surface, a radial **Starburst**, or a grid of **Stipples**. Flash and Custom shapes can also have an **Engraved Line** following the floor outline. Patterns share the same body settings; they are described together on the [Patterns](patterns.md) page so their differences are easy to compare.

The **Insert Hybrid** material combines ordinary surface reflections with a baked internal glow and a virtual image of the lamp underneath. It supports colored plastic and clear plastic, independently adjustable reflection and refraction, and up to four lamps with their own colors and intensities. An optional **Insert Cover** material adds a smooth top, haze, and scratches. Its normal map can follow the playfield for continuous surface detail across the edge of the insert.

An **Insert Light** component connects those lamps to the game. You can preview each lamp in the Inspector, drag its circular handle in the Scene view, and map it in Lamp Manager. An optional real spill light illuminates the ball and nearby objects above the insert.

> [!NOTE]
> **Rendering support.** The mesh generator belongs to VPE's main Unity package. The Insert Hybrid and Insert Cover shaders described here are supplied by the HDRP package. These optical materials currently require HDRP; Shader Graph alone does not make them available in URP.

## The four parts of an insert

| Part | What it controls | Where to edit it |
| --- | --- | --- |
| **Geometry** | Outline, dimensions, body, channel, bevel, holes, and the source pattern | Insert component → Geometry |
| **Bake** | UV layout, rounded surface detail, grain, and the maps used for internal lighting | Insert component → Normal Bake |
| **Materials** | Plastic tint, clarity, reflections, lamp appearance, and the cover's finish | Body and cover material Inspectors; Insert component → Presets |
| **Lamp drive** | Each lamp's intensity and color, mapping names, lamp placement, tinting, and real light output | Insert Light component |

<!-- Diagram: overview-layers. Cutaway with the cover, inward-facing body, channel, patterned floor, virtual lamps, and spill light labeled. -->

> [!NOTE]
> **Why there is a bake.** Blender generates texture detail from the insert's source geometry. Stipples bake from the detailed pyramids onto the ordinary no-pattern mesh, and an engraved line is replaced by a plain middle after baking. Starburst facets remain in the finished mesh. The bake supplies surface normals for reflections and separate optical maps for the lamp. See [Baking](baking.md) for the complete workflow.

## Choose the right page

| You want to… | Start here |
| --- | --- |
| Place a library insert, change its look, or create one from scratch | [Customizing Inserts](customizing.md) |
| Know which preset owns which value, and how brightness is composed | [Presets](presets.md) |
| Change a shape, round a corner, or move a hole | [Geometry](geometry.md) |
| Choose and tune molded surface detail | [Patterns](patterns.md) |
| Generate maps, understand a stale bake, or fork package assets | [Baking](baking.md) |
| Preview lamps, move them, or connect them to the game | [Lighting](lighting.md) |
| Tune plastic, reflections, refraction, glow, and optical variation | [Body Material](body-material.md) |
| Add haze, top reflections, or playfield scratches | [Cover Material](cover.md) |

## Which changes need baking?

| Change | Bake again? |
| --- | --- |
| Shape, dimensions, corners, body depth, channel, bottom bevel, holes, or pattern geometry | **Yes**, when the change alters the generated source mesh |
| Texture size, padding, edge bevel, baked grain, or unwrap settings | **Yes** to see the new settings; changing them alone does not mark the current bake stale |
| Body material settings or material/color presets | **No** |
| Lamp count, placement, radius, color, intensity, or spill settings | **No** |
| Cover material, haze, smoothness, normal strength, or playfield-normal mapping | **No** |

> [!TIP]
> Fit the shape and pattern first, then bake once and tune the material and lamps while looking through the game camera. Material settings update immediately, so that part of the process is quick.

## Saved tables and the player

Inserts are supported by VPE's `.vpe` packaging. The finished mesh, materials, textures, and lamp state travel with the table. Re-importing into the Editor restores the generator settings, custom source meshes, and bake data, so the insert remains editable.

> [!NOTE]
> **Editor data and runtime data.** The player uses the finished insert meshes stored in `table.glb`; it does not regenerate them or run Blender. The generation parameters and editor bake data live in `meta/inserts.json`, a sidecar read when the package is imported for editing. The hybrid and cover material properties are described by VPE's material serialization rather than being reduced to a generic glTF material.
