---
uid: insert_customizing
title: Customizing Inserts
description: Use an insert from the asset library, change its color or shape, or create one from scratch.
---

# Customizing Inserts

The [Asset Library](xref:asset_library) already contains most of the shapes and colors you'll need. However, you might have different cut-outs in your playfield that don't exactly fit, or shape/pattern combinations that aren't in the asset library.

This section describes how you to use, customize existing inserts or create them from scratch.

## From the asset library

The easiest way to get an insert is the [Asset Library](xref:asset_library). It contains most common insert shapes and sizes, in several colors each, already baked, with their lamp and spill light set up. Open it with *Asset Browser* in the toolbox, drag the insert you want into your table, and align its rim with the opening in the playfield.

Then connect its lamp in [*Lamp Manager*](../lamp-manager.md), as described under [Mapping](lighting.md#mapping). That's all a library insert needs. Changing its color, look or lamps doesn't need Blender; only changing its shape does.

## Changing the color

Click a color square under *Presets* on the Insert component. A color is a material, so the insert gets a different material for it: VPE looks for one of that color next to the insert's current material, and if there is none, it creates one as a variant of the current material and links it to the [color preset](presets.md#color). New materials go into your project, also when the insert's material comes from a package.

The plastic's molding and how it treats light is the [look preset](presets.md#look), which you choose with the buttons under the colors. On a library insert, those buttons work once its materials are edited, as described below.

### Changing a color

<img src="customizing/readonly-presets.png" width="340" alt="The Presets section of a library insert, with the buttons to edit its materials" class="img-fluid float-end" style="margin-left: 15px"/>

The asset library is installed as a package that can't be changed, and so are its materials and color presets. **Unity shows the inspector of such a material greyed out: nothing in it can be edited.** VPE treats a library that is locked in the *Asset Browser* the same way: it doesn't change what's in the library's folder, nor the materials the library's insert prefabs come with, and greys out their inspectors too. VPE says so at the top of the material's inspector, and in the insert's *Presets* section.

To change the color of a library insert, edit its materials. Under the color buttons, or in the material's inspector, choose for which inserts:

- *Edit for this insert* (or *Edit for N selected inserts*) edits them for the selected inserts only. Other inserts wearing the same materials keep the library's.
- *Edit for all N inserts* edits them for every insert in the open scene that wears the same materials. They keep sharing them, so a change to one changes all of them.

<div style="clear: both"></div>

<img src="customizing/readonly-material.png" width="340" alt="The inspector of a library material, greyed out, with the buttons to edit it" class="img-fluid float-end" style="margin-left: 15px"/>

Editing gives the inserts copies of their body and cover materials, in the folder a bake of the insert would use, described under [Output](baking.md#output). Copies for a single insert are named after it, so clicking *Edit in project* on that insert later uses them instead of making new ones; copies for several inserts are named after the material. Editing again uses the copies made before.

A copy is a variant of the library's material: until you change a value, it follows the library's, and it stays linked to its color preset, so that color's button stays lit. Change the colors in the copies' inspectors, as described under [Body Material](body-material.md) (*Tint*, *Cavity Color*, *Lamp Emission*, *Max Brightness*) and [Cover](cover.md) (*Haze Color*, *Tint*).

<div style="clear: both"></div>

> [!WARNING]
> **A color change in a material linked to a library color is not kept.** The color preset puts its values back when a look is applied to the material, when another color is applied, and when an update of the asset library changes the preset. To keep your colors, set the material's *Color Preset* to none, or link it to a color preset of your own.

### Adding a color

For a color you'll use on several inserts, add a color preset: *Duplicate* the closest color on the *Color* tab of the *Insert Presets* window, as described under [Color](presets.md#color), and set its values there. Its button then appears on every insert. Clicking it on an insert creates that color's body and cover materials in your project, linked to your preset, so changing the preset later changes every insert of that color. This works on library inserts too, without editing their materials first.

To start from an insert you have already tuned, add the preset, then pick it as the *Color Preset* of the insert's body and cover materials and choose *Copy to* when VPE asks whose values to keep.

> [!NOTE]
> **Shared materials.** A look is applied to the body material, so every insert using that material sees the change. The lamp count and positions live in the body material too. Give an insert its own material or variant before changing something that should affect only that insert. Lamp colors and intensities are set per insert, so inserts sharing a material can still differ in those.

## Changing the shape

A library insert's bake belongs to the asset library's package, which can't be changed. Click *Edit in project* in the *Normal Bake* section to copy the bake and its maps into your project first.

Then change its outline and dimensions in the *Geometry* section, as described under [Geometry](geometry.md), and its floor in the same section, as described under [Patterns](patterns.md). Any change to the geometry makes the bake stale, and the insert shows as yellow wireframe until you bake it again with *Bake Again*. Baking needs Blender 5 or newer; see [Baking](baking.md).

## From scratch

<img src="customizing/toolbox-insert.png" width="214" alt="The Insert button in the toolbox" class="img-fluid float-end" style="margin-left: 15px"/>

Click *Insert* in the toolbox (*Pinball → Toolbox*). It creates an insert in the middle of the playfield, under the table's *Inserts*, with an Insert Light component and a spill light. Then:

1. Move it over its opening in the playfield.
2. Set its shape in the *Geometry* section, as described under [Geometry](geometry.md), and its floor, as described under [Patterns](patterns.md).
3. Bake it, as described under [Baking](baking.md). The first bake also gives it the hybrid body material.
4. Choose its color and look under *Presets*, as described under [Presets](presets.md).
5. Add a clear cover if it should have a glossy top, as described under [Cover](cover.md).
6. Connect its lamp in *Lamp Manager*, as described under [Mapping](lighting.md#mapping).

You can also add *Pinball → Insert* and *Pinball → Insert Light* to any GameObject through *Add Component*. Without an Insert Light component, the insert doesn't light up.

<div style="clear: both"></div>

## Troubleshooting

| What you see | What to check |
| --- | --- |
| *Intensity* is one but the insert doesn't light | The body needs the hybrid material and its optical maps: bake with *Bake Hybrid Optics* enabled. See [Baking](baking.md). |
| The insert shows as yellow wireframe after an edit | Its bake no longer fits its geometry. Press *Bake Again*; see [Stale bakes](baking.md#stale-bakes). |
| The material's inspector is greyed out | The material belongs to the asset library. Edit it for the insert first; see [Changing a color](#changing-a-color). |
| A color change is gone after applying a look | The material is linked to a library color, which puts its values back. Unlink it or link it to a color of your own; see [Changing a color](#changing-a-color). |
| Changing one insert changes another | They share a material. Give the insert its own material or variant first. |
| A colored lamp looks pale through the top | Try [*Tint With Lamp*](lighting.md#tint-with-lamp), and check the cover's [*Opacity*](cover.md#opacity) and [*Haze Color*](cover.md#haze-color). |
| The insert glows but the ball stays dark | It has no spill light, or its *Lumens* or *Range* are too low. See [Spill](lighting.md#spill). |
