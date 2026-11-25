---
uid: plunger_input
title: Analog Plunger
description: How VPE reads a real plunger in a cabinet, and what table authors need to set up for it.
---

# Analog Plunger

Many cabinets have a real spring plunger connected to a sensor. VPE reads that sensor, filters it, and moves the table's plunger along with it. When the player lets go, VPE launches the virtual plunger at the speed the real one had, so the ball goes as far as the pull was strong.

## For table authors

You only need one setting: enable *Mech Plunger* on the shooter lane plunger (see [Plungers](xref:plungers)). Everything else is the player's cabinet setup.

- A manual plunger follows the real one while it's pulled and fires when it's released.
- An *Auto Plunger* with *Mech Plunger* turns a pull-and-release into a 100 ms *Launch Ball* press, like Visual Pinball does. Wire the launch button as usual.
- Only enable *Mech Plunger* on the plunger the player shoots with. Kickbacks and other plungers should leave it off.

As long as no analog plunger moves, a mech plunger behaves exactly like a normal one, including the keyboard pull-back.

## Supported devices

| Device | What VPE uses |
| --- | --- |
| Open Pinball Device (e.g. Pinscape Pico) | Plunger position, and the plunger speed the board measures. |
| Pinscape (KL25Z) joystick | The Z axis for the position. The RZ axis for the speed, only if the firmware is set up to report it. |
| Gamepad | The right stick, pulled down. |
| Any other joystick | Any axis, picked by hand. |

These work on Windows, Linux and macOS. On Linux, an Open Pinball Device needs the udev rule from the NativeInput read-me, otherwise it doesn't show up.

## Setting it up in the player

Open **Settings → Input → Plunger**.

1. Turn on the card of your plunger's device. VPE picks the axes for the devices above.
2. Pull the plunger and watch the **Live test**: a drawn plunger follows yours, and releasing it launches a ball up a short lane. The scope below shows the position and the release speed of the last five seconds.
3. If full pull doesn't reach the end, or the rest position is off, press **Calibrate**. Let go of the plunger for the first step, then pull it all the way back and hold it for the second.
4. If pulling moves the drawn plunger forward, set **Direction** to *Inverted*.

The live test uses a default plunger and ball. A table's own plunger can be stronger or weaker.

For a Pinscape joystick that reports the plunger speed on RZ, set **Signal** to *Position + velocity*. For other devices, **Advanced axis settings** lets you choose the axes, the dead zone around rest and the full scale of the speed axis.

The **Plunger settings** card has one more option, **Retract after one second**: when you hold the keyboard plunger fully back, it moves forward and back again instead of staying, like Visual Pinball's retract option. It's off by default.

## In the editor

The *Simulation Thread* component lists the plunger sensors under *Plunger Sensors*, and its inspector shows the connected plunger devices in Play Mode with buttons to map them, plus the filtered position and speed.
