---
description: The lamp manager lets you connect and configure lights, flashers and GIs of the playfield to the gamelogic engine.
---
# Lamp Manager

There are many types of lamps a real pinball machine might use, and there are different ways a gamelogic engine might be addressing them. VPE uses the Unity game engine to accurately simulate lights on the playfield. Those lights have a standardized set of parameters, which you can tweak in the editor. However, lights in a game are dynamic, so the gamelogic engine will toggle them, fade them, or even change their color.

In order to link each of the playfield lights to the gamelogic engine and configure how they react during gameplay, the *Lamp Manager* is used. You can find it under *Visual Pinball -> Lamp Manager*.

![Lamp Manager](lamp-manager.png)

> [!note]
> We use the terms *lights* and *lamps* as follows:
> - With **light** we're referring to the render engine's [light](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@10.2/manual/Light-Component.html). It's a simulated light source and doesn't have to be a physical element on the table, but can also refer to the sun, some directional scene light, or other types of lighting used in the simulation.
> - With **lamp** we're referring to a "bulb" that is "screwed" into the table. It's more of a logical component VPE has to deal with during gameplay, decoupled for the rendering aspect.

## About Lamps

Physical machines have a bunch of different concepts when it comes to lighting. The vast majority of solid state machines from the eighties until the early 2010s used a **lamp matrix**, where lamps were addressed by row/column, and they only could be turned on or off. Historically, incandescent light bulbs were used, which resulted in a warm-up period until they reached full illuminosity (and a cool-down period when turned off). For this, VPE adopted the fade-in and fade-out properties from Visual Pinball that can be set on a light.

Later machines used single colored **LEDs** that were each directly connected to the controller board (see also: [Lights vs LEDs](https://docs.missionpinball.org/en/latest/mechs/lights/lights_versus_leds.html)). Contrarily to matrix lamps, the intensity here could be set more fine grained by the game software.

More recently, games started using **RGB-LEDs** that are additionally able to change the color during gameplay. In VPE, these can be handled in two different ways:
- As three single connections from the gamelogic engine (e.g. that's what PinMAME provides)
- With a single RGB connection, where the gamelogic engine always provides the full color (e.g. MPF, or custom table logic)

Additionally, most pinball machines come with **GI strips**, which are a set of bulbs used for global illumination of the playfield. All lights from a strip are addressed at once, so one gamelogic GI strip maps to multiple lamps on the playfield.

Finally, high-powered lamps such as flashers might appear under the gamelogic engine's **coil outputs**, since those lamps operate with the same voltage and have the same properties as coils.  

## Setup

Every row in the lamp manager corresponds to a logical connection between the gamelogic engine and the lamp on the playfield. A lamp can be linked to multiple outputs, and an output can be linked to multiple lamps.

### IDs

The first column, **ID** shows the name that the gamelogic engine exports for each lamp.

> [!note]
> As we cannot be 100% sure that the gamelogic engine has accurate data about the lamp names, you can also add lamp IDs manually, but that should be the exception.

### Description

The **Description** column is optional. If you're setting up a re-creation, you would typically use this for the lamp name from the game manual. It's purely for your own benefit, and you can keep this empty if you want.

### Destination

The **Destination** defines where the lamp is located. Currently, *Playfield* is the only option.

### Element

Under the **Element** column, you choose which lamp among the game items on the playfield should be controlled.

### Type

The **Type** column defines how the signal is interpreted by the lamp. This is important, because the gamelogic engine typically sends integer values to the lamp. There are four types:

- *Single On|Off* - Typically lamps from the lamp matrix. They can only be on or off. Receiving `0` will turn the lamp off, any other value will tell it on.
- *Single Fading* - Individually connected lamps that can be dimmed by the gamelogic engine. Received values can be `0` to `255`, where `0` turns the lamp off, and `255` sets it to full intensity.
- *RGB Multi* - An RGB lamp that can change its color during gameplay. Lamps of this type receive three connections, one from each red, green and blue.
- *RGB* - An RGB lamp that receives its data from a single connection. This is the only mode where the lamp doesn't receive an integer, but an entire color.

## Flashers

When using a gamelogic engine that behaves like real hardware like PinMAME, high-powered lamps such as flashers show usually up as connected driver board. 

VPE allows routing coil outputs to lamps. For that, go to the [Coil Manager](coil-manager.md) and select *Lamp* as **Destination**:

![Coil as lamp](coil-manager-lamp.png)

This will make the coil show up in the lamp manager where you can configure it:

![Lamp from coil](lamp-manager-coil.png)

You note that you cannot change the *ID* of the lamp, because it's still linked to the coil. Also, removing or changing the coil destination will remove the entry from the lamp manager. Changing the ID in the coil manager will also update it in the lamp manager.

## GI Strips

There is currently no special support for GI strips. In Visual Pinball, you can put GI lamps into a collection and address the whole collection at once via script. VPE doesn't have this feature yet. In order to hook up GI lamps, you can can add an entry per lamp and link all of them to the same ID.

We want to make this easier in the future, so we're thinking of integrating this into the editor directly.

## Editor vs Runtime

While editing the table in the Unity editor, you can and probably should disable lights you're not editing. During runtime, VPE first turns all lights off, then turns on the constant lights, and then waits for the gamelogic engine for further instructions.

If you run the game in the editor, the lamp manager shows the lamp statuses in real-time:

![Lamps runtime](lamp-manager-gameplay.gif)
