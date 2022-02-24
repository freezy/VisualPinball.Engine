---
uid: uvs_lamps
title: Lamps
description: How lamps are driven.
---

# Lamps

Lamps a bit more complex than coils and switches, because besides of simply being turned on or off, they have an intensity and a color. Additionally, they have a *blinking* state. This means that all our lamp nodes include a dropdown indicating how it should be driven, with the input (or output) types changing accordingly:

- **Status** corresponds to a `enum`, one of  *On*, *Off* and *Blinking*.
- **On/Off** is a `boolean`, where `true` corresponds to the *On* status, and `false` to the *Off* status.
- **Intensity** corresponds to a `float`, and is the brightness of the lamp.
- **Color** has its own `Color` type.

These four modes allow you to completely control a lamp (with *On/Off* being sugar for setting the status using a `boolean`). However, there is a second factor that defines how the lamp will actually react, and that is its [mapping type](xref:lamp_manager#type) in the Lamp Manager.

See, VPE supports a wide range of gamelogic engines, and they often don't have an internal API as rich as our visual scripting package. For example, when PinMAME sets a light to the value of 255, it doesn't know whether it just "turned it on" from 0 or whether it was "faded in" from a previous non-0 value. That's information we have to manually set in the Lamp Manager (in this example, the mapping type would be *Single On|Off* or *Single Fading* respectively).

That said, the only mode that might leads to confusion is *Intensity*, mainly because it's the only value that PinMAME emits. So if you choose *Intensity*, here is how the value is treated depending each mapping type:

- *Single On|Off* sets the **status** of the lamp to *On* if the value is greater than 0, and to *Off* otherwise.
- *Single Fading* sets the **intensity** to the value *divided by [maximal intensity](xref:lamp_manager#max-intensity)*. We recommend setting the maximal intensity to 100 in the Lamp Manager and use values from 0 to 100 in the visual scripting nodes.
- *RGB* sets the **intensity**, where the value is between 0 and 1.
- *RGB Multi* you probably won't use. It sets the [channel](xref:lamp_manager#channel) defined in the mapping to the value divided by 255 (yes, it's very PinMAME specific).

