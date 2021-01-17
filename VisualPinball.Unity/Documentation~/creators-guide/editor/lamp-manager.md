---
description: The lamp manager lets you connect and configure lights, flashers and GIs of the playfield to the gamelogic engine.
---
# Lamp Manager

There are many types of lamps a real pinball machine might use, and there are different ways a gamelogic engine might be addressing them. VPE uses the Unity game engine to accurately simulate lights on the playfield. Those lights have a standardized set of parameters, which you can tweak in the editor. However, lights in a game are dynamic, so the gamelogic engine will toggle them, fade them, or even change their color.

In order to link each of the playfield lights to the gamelogic engine and configure how they react during gameplay, the *Lamp Manager* is used. You can find it under *Visual Pinball -> Lamp Manager*.

[TODO: Screenshot]

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

[TBD how to handle solenoids]

## Setup

[TODO]

## Inserts

[TODO]

## Flashers

[TODO]

## Editor vs Runtime

While editing the table in the Unity editor, you can and probably should disable lights you're not editing. During runtime, VPE first turns all lights off, then turns on the constant lights, and then waits for the gamelogic engine for further instructions.  
