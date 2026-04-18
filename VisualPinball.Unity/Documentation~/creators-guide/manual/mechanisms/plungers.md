---
uid: plungers
title: Plungers
description: VPE supports manual plungers, auto plungers, and ROM-controlled kickback-style plungers.
---

# Plungers

Plungers are physics objects that can launch a ball by moving along their stroke. They are commonly used for manual ball launchers, auto-launchers, and kickback mechanisms such as an outlane ball saver.

## Setup

Add a *Plunger Collider* to the plunger object to make it part of the physics simulation. The plunger can then be wired through the [Coil Manager](xref:coil_manager) or [Wire Manager](xref:wire_manager), depending on whether it is controlled by game logic or player input.

The most important plunger settings for gameplay are:

- *Stroke*: total travel distance of the plunger tip.
- *Park Position*: normalized rest position from `0` to `1`, where `0` is fully forward and `1` is fully retracted.
- *Speed Fire*: release speed used when the plunger fires.
- *Speed Pull*: pull-back speed used when the plunger is pulled back by a coil or input.
- *Mech Plunger*: enables synchronization with analog plunger input.
- *Auto Plunger*: makes the plunger rest at *Park Position* and fire from full retraction when triggered.

## Coil Modes

The plunger exposes multiple coil items. Pick the one that matches the real mechanism or the original VPX script behavior.

| Coil item | Inspector label | Coil enabled | Coil disabled | Typical use |
| --- | --- | --- | --- | --- |
| `c_pull` | Pull back | Pulls the plunger back | Fires from the current position | Manual launch plunger wired to a button or input |
| `c_autofire` | Auto-fire | Fires the plunger | No action | ROM-controlled auto-launchers where the plunger returns to rest naturally |
| `c_fire_pullback` | Fire and pull back | Fires from full retraction | Pulls the plunger back | Kickbacks that call `Fire` on solenoid on and `PullBack` on solenoid off |

## Manual Launchers

For a normal shooter lane plunger, map player input to `c_pull`. Pressing the input pulls the plunger back, and releasing it fires from the current position. If the table uses analog plunger input, enable *Mech Plunger* so the physics plunger follows the analog input position.

## Auto-Launchers

For ROM-controlled launch buttons, enable *Auto Plunger* and map the game logic coil to `c_autofire`. In this mode the plunger rests at *Park Position*. When the coil fires, VPE launches from full retraction, which gives a consistent launch impulse.

## Kickbacks

Some VPX tables implement kickbacks with a plunger object and script the solenoid like this:

```vb
Sub SolKickback(enabled)
	If enabled Then
		Plunger1.Fire
	Else
		Plunger1.PullBack
	End If
End Sub
```

Use `c_fire_pullback` for this pattern. It fires from full retraction when the ROM enables the coil, and pulls the plunger back when the ROM disables the coil again.

VPE also applies the disabled state once when this coil mode is mapped at startup. This mirrors VPX tables that call `PullBack` during table initialization, and ensures a kickback starts clear of the ball path before the ROM has emitted its first coil event.

For kickbacks that should not block the outlane while idle, enable *Auto Plunger* and set *Park Position* to the clear/resting position. This keeps the physics target at the parked position while idle. If the original VPX table uses a small park position such as `0.1666`, start with the same value and tune only if the VPE geometry needs it.

Avoid enabling *Mech Plunger* on kickbacks unless an analog input should control that specific plunger. A mechanical plunger with no analog input targets position `0`, which is fully forward and can make the plunger block the lane during gameplay.