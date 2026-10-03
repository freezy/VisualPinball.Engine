---
uid: insert_presets
title: Insert Presets
description: Which of the three insert presets owns which value, where each is applied, and how brightness is composed.
---

# Insert Presets

An insert's appearance is set in three places, each answering one question, and no value is owned by two of them.

| Preset | Answers | Scope | Owns | Applied on |
| --- | --- | --- | --- | --- |
| **Lamp** | What bulb is under it? | The table | The lamp's **Color**, **Output**, **Fade Up** and **Fade Down** | The Insert Light's Lamp row; the whole table from the **Incandescent** / **LED** buttons in [Lamp Manager](../lamp-manager.md#lamp-type) |
| **Color** | What plastic is it? | A colour | Body **Tint**, **Cavity Color**, **Lamp Emission** and **Max Brightness**; cover **Haze Color** and **Cover Tint** | The colour squares under **Presets → Color** on the insert; the body and cover materials follow the preset |
| **Look** | How is it moulded, and how does it behave optically? | A style: **Clear Plastic**, **Colored Plastic** | Every other numeric value of the body material: clarity, surface, the virtual lamp's strength, spread and definition, glow, facets, mottle, grain | The buttons under **Presets → Look** on the insert; the body material follows the preset |

Three things are deliberately not presets. **Roughness**, in the insert's **Wear** section, is this insert's own: inserts sharing a material can be worn differently. **Intensity** on the Insert Light is the game's. The virtual **lamp positions** and radius are the shape's, stored in the body material.

## Where a value comes from

A material follows the presets it is linked to: edit the preset, and every material linked to it changes. The link is shown at the top of the body material's Plastic section as **Color Preset** and **Look Preset**, and the Insert Light's Lamp row names its preset.

In the body material's inspector, every value a preset owns says so in its tooltip: *From the Look preset Clear Plastic* or *From the Color preset Red*. A value edited in the material after the preset was applied is marked **•** on its label, its tooltip gives the preset's value, and the Look row counts them: such a value goes back to the preset's the next time the preset changes. **Copy to <preset>** keeps it by putting it into the preset, and so into every material linked to it; **Reset to <preset>** discards it. The Insert Light's Lamp row behaves the same way for the lamp's values.

Each preset asset's own inspector lists the materials, or lights, that follow it, and how many values each has edited.

## Brightness

A lit insert's brightness is one chain, and nothing else scales it:

**Lamp Output × Color Max Brightness × Intensity**

- **Output** is the bulb: an LED kit gives about three times a filament's light. The Lamp preset.
- **Max Brightness** is the plastic: colours differ in how much of the light they let through, so each colour has its own. The Color preset.
- **Intensity** is the game: the lamp's level, zero to one, from Lamp Manager or the preview slider.

Under the tonemapper an insert that already clips to white does not get whiter with more output; the difference shows in its fall-off, its spill onto the ball and playfield, and its bloom.

## Editing a preset

- **Lamp**: in Lamp Manager, press **Settings** next to the lit lamp button; the four values are edited in place, written to the preset and put under every insert of the table as you change them, during a game in the editor as well. **Asset** highlights the preset in the Project window.
- **Color**: Ctrl/Cmd-click a colour square to highlight its asset, and edit it in the Inspector; the linked materials follow. **Copy to <colour>** in a body or cover material takes that material's colours into the preset.
- **Look**: Ctrl/Cmd-click a look button to highlight its asset, or use **Copy to <look>** in a body material. The **+** button saves the selected insert's current look as a new preset and links the material to it.

The shipped presets are in the HDRP package's `Assets/Art/Materials/Insert Presets` folder; a project's own go anywhere under `Assets/` and join the same rows and buttons.
