---
uid: insert_lighting
title: Insert Lighting
description: Preview and place up to four virtual lamps, map them to the game, tint the plastic, and configure real light spill.
---

# Insert Lighting

**Insert Light** drives the lamp colors and intensities seen through the hybrid body and, optionally, a real light above the insert. Every shape can have one to four lamps; a banana usually wants several along its length.

The body needs a hybrid material with optical maps. If the Inspector reports that they are missing, use **Bake Hybrid and Use Material**. The material sets the full-intensity look; Insert Light previews and animates it.

## Lamp

The **Lamp** dropdown under the intensity sliders says what bulb sits under the insert: it sets the lamps' **Color**, the bulb's **Output** and the **Fade Up** and **Fade Down** times from a [lamp preset](presets.md#lamp), and the light remembers which preset it follows. **Custom** keeps the values as they are, following no preset.

An insert whose lamp is mapped as **RGB** or **RGB Multi** in [Lamp Manager](../lamp-manager.md) is an RGB LED whatever the rest of the table runs: the game drives its color and fading, so no preset is applied to it and its Lamp row says so.

| Parameter | Meaning |
| --- | --- |
| **Output** | How much light the bulb gives, relative to a #44 filament. It scales every lamp's lit look and the spill alike, at every intensity, without touching the material's Max Brightness. An insert that already clips to white does not get whiter with more output; the difference shows in its fall-off, its spill and its bloom. |

A light whose values no longer match its preset says so and offers **Reset to Preset**. **Give All Inserts This Lamp** applies the preset to every insert of the table; the **Incandescent** and **LED** buttons in [Lamp Manager](../lamp-manager.md#lamp-type) do the same and also give the table's other lamps the fades. An insert driven by a parent VPE Light fades with that Light's settings instead. **Edit…** opens the [Insert Presets](presets.md) window.

A blue insert is the dimmest on an incandescent table: a warm filament has little blue to give, and the blue plastic keeps the rest. Where a table needs it brighter all the same, raise the Blue [color preset](customizing.md#color-presets)'s **Max Brightness**.

<!-- Screenshot: lighting-lamp-presets. The same clear and red insert lit under Incandescent and LED. -->

## Lamps

The **Lamps** section places one **Lamp N** intensity slider at the top for each active lamp. Below **Number of lamps**, each row shows its Name, Color, and Position. Preview one lamp at a time before checking how their bright regions overlap.

| Parameter | Meaning |
| --- | --- |
| **Lamp N** / **Intensity** | Preview drive from 0, off, to 1, the material's Max Brightness. Each lamp has its own value. In a multi-object selection, Intensity and Color edit the first lamp. |
| **Number of lamps** | Active virtual lamp count, from 1 to 4. Count is stored in the body material. Lowering it preserves disabled lamps' placement for reuse. |
| **Name** | Name offered in Lamp Manager for that lamp. Empty names fall back to Bulb 1, Bulb 2, and so on. Descriptive names such as Left, Center, and Right make mappings easier. |
| **Color** | That lamp's drive color, independent of the plastic Tint. It multiplies the material's Lamp Emission color. The Lamp preset sets it for every bulb; each lamp can still have its own RGB mapping. |
| **Position (mm)** | Position relative to the insert in Unity-local X/Y/Z, in millimeters. Y is height; negative is below the rim. Changing X/Z moves the bright region, while changing Y affects depth and parallax. |

Drag the labeled **circular handles** in Scene view to move lamps in the insert's local XZ plane. The handles keep their Y height; edit Y in the Inspector to move a lamp vertically. Lamp count and position edits are disabled during Play mode and for read-only materials.

**Bulbs**, **Position**, and **Radius** in the [body material](body-material.md#virtual-lamp) refer to the same virtual lamps. Radius is shared by the active bulbs and is edited in that material Inspector. Lamp placements and radius are material settings, so all inserts sharing the material share that layout.

<!-- Screenshot: lighting-layout. One versus three lamps; independently previewed sliders, per-row colors, and Scene circle handles. -->
<!-- Animation: lighting-parallax. Fixed lamp placement seen from two camera angles, plus a deeper lamp for comparison. -->

> [!NOTE]
> **A virtual lamp is a shader image.** Its position describes where the optical shader looks for a bulb. It is not a Unity Light and moving it does not move the spill light. Colors and intensities are passed as per-renderer material overrides, so different insert objects can animate independently while sharing a material.

## Tint With Lamp

| Parameter | Meaning |
| --- | --- |
| **Tint With Lamp** | Turns the body's and cover haze's hue and saturation toward the local lamp mix when lit. This helps a neutral haze preserve the color of an RGB lamp. Off retains their authored hues. |
| **Tint Amount** | Maximum hue/saturation blend, from 0 to 1. Enabled when Tint With Lamp is on. Zero preserves the authored look; one takes the lamp hue fully where its tint reaches. |

This blend preserves the color's brightness value. It does not make the body emit on its own, and an off lamp leaves the authored tint intact. A white lamp supplies no hue change. With several colored lamps, the local blend follows their positions and **Lamp Spread**, so a long insert can be red at one end and blue at the other.

<!-- Screenshot: lighting-tint. Same gray cover over a saturated lamp, Tint With Lamp off/on; Tint Amount 0.25/1 separately. -->

## Spill

A glowing insert does not by itself supply a controllable direct light to nearby geometry. Assign a light under **Spill → Light**, or use **Add Spill Light** to create one just under the insert, pointing upward through it.

| Parameter / action | Meaning |
| --- | --- |
| **Light** | Unity Light driven by the active lamps. Its transform remains editable, allowing you to aim the spill. |
| **Lumens** | Output per active lamp at Max Brightness = 1 and lamp intensity = 1. The default for a new spot spill is 1.5 lm. |
| **Range (m)** | Reach of the light in meters, independent of the virtual Lamp Spread. The default is 0.1 m. |
| **Configured Spill** | Inspector calculation using Lumens × Max Brightness × the first lamp's preview Intensity. With multiple lamps, actual output uses the sum of their intensities. |
| **Add Spill Light** | Creates a wide spot light and assigns it. |
| **Make It a Spot Light** | Converts an assigned non-spot spill into the wide upward spot setup, replacing old point defaults where still present. |

For several lamps, light output is **Lumens × Max Brightness × sum of active lamp intensities**. Their linear colors are averaged with intensity weights and multiplied by the hue of Lamp Emission. The single spill represents their combined illumination; it is not a separate real light for every virtual bulb.

For example, Lumens 1.5, Max Brightness 20, and one lamp at 0.5 gives **15 lm**. A second lamp at 0.5 brings the output to **30 lm**.

<!-- Screenshot: lighting-spill. Insert and ball with Lumens 0/1.5, and Range 0.04/0.1, keeping the camera and body emission fixed. -->

> [!NOTE]
> **Default spill setup.** The helper creates a wide spot with a 147° cone, a soft inner cone, a small light source, and no shadows, bounce, or volumetric contribution. Aim and range are important because the light is deliberately broad. Each enabled spill also adds a real light's rendering cost; use it where nearby objects need illumination.

## Mapping

Select **Insert Light** as a destination in [Lamp Manager](../lamp-manager.md). A single-lamp insert offers its normal lamp item; an insert with several lamps offers a separately named item for each bulb. Map each game output to the intended item.

Use **Single On/Off** for binary output, **Single Fading** for stepped intensity, and **RGB** for a color-and-intensity output. For separate red, green, and blue game outputs, use **RGB Multi** with the appropriate channel on each mapping; an optional alpha channel drives intensity. Insert Light is also available to [Light Groups](../../manual/mechanisms/light-groups.md) and as a [Wire Manager](../wire-manager.md) destination.

During Play mode, mapped lamp events overwrite preview drive values. An unmapped Insert Light can follow a parent VPE Light component, while direct lamp/wire control takes precedence. Without either driver, the Inspector values remain the manual control.

> [!TIP]
> Test the unlit state, each lamp separately, and the combined state. A material that looks good with every lamp at one can still be too bright where colors overlap or too dark at the far end when only one lamp is on.
