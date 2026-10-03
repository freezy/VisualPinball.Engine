---
uid: insert_customizing
title: Customizing Inserts
description: Start with a standard library insert, create a custom insert, and use color and material presets without losing its baked detail.
---

# Customizing Inserts

Start with the closest standard insert in the **Asset Library**. Most common shapes and sizes will be available there, already prepared for use. Choose custom geometry when the outline, molded pattern, or lamp arrangement needs to match something more specific.

## Customize a library insert

1. Place the insert in your table and align its rim with the playfield opening. Keep its scale at one when its authored dimensions already match.
2. Use the **Color** buttons under **Insert → Presets** to choose White, Blue, Green, Red, Yellow, Teal, or Purple.
3. Choose a **Material** preset such as **Clear** or **Colored**, then adjust the [body material](body-material.md) and optional [cover](cover.md) as needed.
4. Set the lamp colors and preview intensities on **Insert Light**, position its lamps, and connect them in [Lamp Manager](../lamp-manager.md).
5. If you change geometry or want a different bake, use **Fork to Project** when the source assets are read-only, then **Bake Again**.

Color and material changes are immediate. Shape and pattern changes alter the bake source and need a new bake.

<!-- Screenshot: customizing-library. A library insert placed on a table, with Color and Material preset buttons visible. -->

> [!NOTE]
> **Package assets and project assets.** A library bake can be referenced by many inserts and may belong to a package. Fork to Project copies the bake into `Assets/Inserts` and derives a writable material variant. That gives the customized insert its own bake while retaining the original material's inherited tuning. If the copied bake is stale, the fork starts a new bake automatically; a fitting bake is simply copied.

## Create an insert from scratch

1. Create a GameObject at the insert's position and add **Visual Pinball → Insert** through **Add Component**. Unity adds the required Mesh Filter and Mesh Renderer.
2. Choose the outline and dimensions in **Geometry**. For an imported outline, choose Custom and assign its Source Mesh. Add a channel, bevel, holes, and pattern as needed. See [Geometry](geometry.md) and [Patterns](patterns.md).
3. Add **Visual Pinball → Insert Light** if the insert should respond to lamps.
4. Keep **Bake Hybrid Optics** enabled in **Normal Bake**, select a Blender 5 executable if it is not found automatically, and bake.
5. With Insert Light and an empty body material slot, the first bake sets up a hybrid body material automatically. If you started with another material, use **Bake Hybrid and Use Material** on Insert Light, or bake the optical maps and click **Use HDRP Hybrid Material** on Insert.
6. Click **Add Intensity Controls and Spill Light** or **Add Spill Light** when you want the insert to illuminate nearby objects. Use **Add Clear Cover** for the optional glossy top.
7. Choose presets and finish the material and lamp settings from the game camera.

> [!TIP]
> A normal bake and a working lamp material are separate requirements. A material without the hybrid shader cannot use the Insert Light's Intensity and Color. The Inspector's **Bake Hybrid and Use Material** button handles both requirements together.

## Color presets

The colored squares select materials for the insert's body and, when assigned, its cover. They use an **Insert Color Preset** asset containing the plastic, cavity, HDR emission, and haze colors. These are material colors and describe the plastic under a white lamp; the bulb behind it is the Insert Light's [Lamp preset](lighting.md#choose-the-kind-of-bulb), whose colour multiplies the emission, so the same red material is right over an incandescent bulb and over an LED.

Clicking a color first searches for a matching material in the current material's folder. It matches the shader, requested color word, and related material name, so another insert's unrelated color material is not chosen merely because it is green. If a matching material exists, it is assigned with its existing settings intact.

When there is no match, VPE derives a new material from the current one. A recognized color word is replaced with the selected color; without a color word, the new name receives `, <Color>` and the material is made a variant of its source. The body receives the preset's **Tint**, **Cavity Color**, **Lamp Emission** and **Max Brightness**; the cover receives **Haze Color** and **Cover Tint**. The textures stay inherited or copied from the source material.

For package materials, VPE checks both the package folder and its equivalent project folder. Newly created materials go into the project. For example, `Packages/<package>/Assets/Art/Materials/Inserts` maps to `Assets/Art/Materials/Inserts`. A material without an asset path uses `Assets/Inserts`.

<!-- Screenshot: customizing-colors. Same insert, same exposure and lamp drive, with each of the seven color buttons applied. -->

> [!NOTE]
> **Presets create a starting point.** Selecting an existing color material does not overwrite it with the current preset's colors. Editing the preset changes the colors used for future material creation; existing materials can keep their individual tuning.

### Edit or add a color preset

Ctrl-click a color button, or Cmd-click on macOS, to highlight its preset asset in the **Project** window. The shipped presets are in the HDRP package's `Assets/Art/Materials/Insert Presets` folder. For a project-specific preset, create an asset with **Create → Visual Pinball → Insert Color Preset**.

| Parameter | Meaning |
| --- | --- |
| **Color Name** | Color word used in the button tooltip, material naming, and lookup. Use a distinct, stable name. |
| **Order** | Button ordering; lower values appear first, with names used to break ties. |
| **Swatch** | The button's display color. It does not change the rendered material. |
| **Tint** | The hybrid body's Tint when a new color material is created. |
| **Cavity Color** | Color underneath the clear share of the plastic. Its effect depends on Clarity. |
| **Lamp Emission** | HDR emission color and magnitude on the newly created body material: the colour of the plastic lit from below by a white lamp. The lamps' own colors, from the Insert Light's Lamp preset, multiply it. |
| **Max Brightness** | The body's Max Brightness. Colors differ in how bright they look through the plastic, so each has its own. |
| **Haze Color** | Haze Color on a newly created cover color material. It needs a cover and nonzero Opacity to show. |
| **Cover Tint** | The cover's Tint: the color the lamp light and the body underneath take on their way up through the plastic slab. Keep a little of every channel in it, and in Lamp Emission, so a bright lamp fades to white instead of shifting hue under the tonemapper. |

## Material presets

Material presets such as **Clear** and **Colored** apply the body's numeric look settings: clarity, brightness, reflection strength, optical behavior, and variation. They preserve all color values, texture maps, virtual lamp positions and radii, and the cover material. They also leave hidden bake metadata and runtime lamp drive alone.

The preset buttons apply directly; there is no dropdown or extra Apply step. The **+** button on the same line saves the current numeric body settings as a new **Insert Material Preset** asset. Ctrl/Cmd-click a preset button to highlight its asset in the Project window.

<!-- Screenshot: customizing-material-presets. Clear and Colored applied to the same shape and color, with the same lamps. -->

> [!NOTE]
> **Shared material edits.** A material preset edits the assigned body material. Every insert using that same material sees the change. Lamp count and positions also live in the body material. Give an insert its own writable material or variant before making a change that should affect only that insert. Lamp colors and intensities use per-renderer overrides and can differ between inserts sharing a material.

A material preset asset stores **Body Shader**, the shader name it was captured from, and a **Body** list of numeric properties. Each entry's **Name** is the shader property reference, **Kind** identifies its value type, and **Float** or **Vector** supplies the corresponding value. Use the **+** button to capture a preset, then edit the stored numbers when refining it. Color entries are not applied; colors belong to color presets.

## A practical tuning order

Fit [Geometry](geometry.md), choose the [Pattern](patterns.md), and finish the [Bake](baking.md) first. Then tune the unlit body with all lamps off. Bring one lamp up and adjust Max Brightness, its position, spread, and radius. Add the other lamps and test their combinations. Finish the cover's reflections and haze, then check the result in the game's camera and exposure.

For a colored insert, the **Colored** preset is a useful start; keep Clarity near zero and use Min Brightness to set the off appearance. For clear plastic, start with **Clear**, use Cavity Color for the board or reflector beneath it, and soften the reflection ribs with Facet Flatten. Both looks can use the same geometry and maps.

## Common surprises

| What you see | What to check |
| --- | --- |
| Intensity is one but the insert does not light | The body needs the hybrid material and optical maps. Check Max Brightness, Lamp Emission, and the lamp's Color as well. |
| The insert becomes yellow geometry in Scene view after an edit | Its previous bake no longer fits. Bake Again; the yellow preview shows the current source geometry. |
| A colored lamp looks pale through the top | Try Tint With Lamp and Tint Amount; check the cover's Opacity and Haze Color. |
| Changing one insert changes another | They share a material. Give the insert a separate material or variant before editing material settings or lamp layout. |
| The lamp glows but the ball stays dark | Add or assign a real spill light and check its Lumens, Range, and orientation. |
| A bake or lamp-position field is locked | Its material or bake belongs to a read-only source. Fork to Project before editing. |
