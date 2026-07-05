---
uid: tilt_bobs
title: Tilt Bobs
description: VPE can route tilt switch events from a simulated plumb bob or a real cabinet tilt bob.
---

# Tilt Bobs

A tilt bob is the table's tilt switch route. Add a `TiltBobComponent` when the
table should have a tilt switch that can be driven either by VPE's simulated
plumb bob or by a real cabinet tilt bob wired into the player's cabinet
controller.

VPX imports add a `TiltBobComponent` to the table root by default. Remove it if
the table should not have a tilt bob route.

## Setup

To add a tilt bob manually, select the table object or another object under the
table, click *Add Component*, and select *Pinball -> Mechs -> Tilt Bob*.

Use the [Switch Manager](xref:switch_manager) to map the game logic tilt switch
to the component's `Tilt Bob` switch item.

If a table does not have a `TiltBobComponent`, VPE does not simulate a plumb bob
for that table and does not pull the player's physical cabinet tilt switch. This
lets tables opt out by omission instead of by a hidden setting.

## Source

The player chooses the plumb source in cabinet/player settings:

| Source | What happens |
| --- | --- |
| `Simulated` | Cabinet acceleration swings VPE's simulated plumb bob. When it crosses the threshold, the `TiltBobComponent` sends its mapped switch. |
| `Physical` | The player's physical cabinet `Tilt` input drives the `TiltBobComponent`. Use this for a real tilt bob wired into a cabinet controller. |

This source is a player setting, not a table setting. The table only decides
whether a tilt-bob switch route exists and which game logic switch receives the
signal.

## Simulated Plumb Bob

For the simulated source, these `TiltBobComponent` fields shape how easily the
bob trips:

| Field | Lower value | Higher value |
| --- | --- | --- |
| `Plumb Threshold Angle` | Easier to tilt. | Harder to tilt. |
| `Plumb Damping` | Bob rings longer. | Bob settles faster. |

Test tilt with the same keyboard and sensor defaults you expect players to use.
Very strong nudging plus a low threshold can make a table tilt too easily. For a
real cabinet bob, test the physical source with the actual cabinet input binding.
