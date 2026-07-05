---
uid: nudging
title: Nudging
description: How table authors configure nudge behavior and cabinet input defaults.
---

# Nudging

Nudging in VPE moves the virtual cabinet, not the ball directly. When the player
nudges, VPE computes cabinet acceleration and cabinet offset. The ball reacts to
that acceleration through physics, the camera/table can move visually, and the
table's plumb tilt component can tilt from the same movement when the table
author adds one.

As a table author, think about nudging in two layers:

- Table feel: how strong keyboard nudges are, how quickly the cabinet settles,
  how much visual motion is shown, and whether the table has a tilt bob route.
- Player hardware: which real device and axes are mapped for a specific
  cabinet.

The first layer belongs in your table defaults. The second layer usually belongs
to the player's cabinet or player app configuration.

## Table Defaults

The main table-level nudge settings live on the `PhysicsEngine` component.

| Setting | What it affects |
| --- | --- |
| `Keyboard Nudge Mode` | How keyboard/button nudges are converted into cabinet motion. |
| `Keyboard Nudge Strength` | Strength multiplier for keyboard/button nudges. |
| `Keyboard Cabinet Damping` | How firmly the cabinet settles after `CabModel` keyboard nudges. |
| `Visual Nudge Strength` | How much of the cabinet offset is shown visually. This is cosmetic. |

The `Keyboard Nudge Mode` setting has three choices:

| Mode | What it does | When to use it |
| --- | --- | --- |
| `PushRetract` | Applies the older VP-style push/retract pulse. It gives the ball a sharp kick, then quickly retracts the cabinet offset. | Use it only when a table was tuned around this older keyboard nudge feel. |
| `BoxModel` | Treats the table as a displaced box with a spring pulling it back to rest. It can visibly ring if the timing is long. | Use it when matching legacy VP behavior for a table that depends on that spring-style response. |
| `CabModel` | Applies a short cabinet impulse and lets the cabinet oscillator settle it. This is meant to feel like a sturdy cabinet shove rather than a long bounce. | Use it for new tables and for tables where keyboard nudging should resemble cabinet nudging. |

The `TableComponent` also has `NudgeTime`, imported from VPX table data. In VPE
this is used by the legacy `BoxModel` keyboard nudge path to set the spring
timing.

Use `CabModel` for new work unless you are matching an existing table's VP
behavior. It models a cabinet shove and a short return rather than a long visible
spring bounce. `PushRetract` and `BoxModel` are available for legacy VP-style
compatibility.

Higher keyboard cabinet damping makes the cabinet feel sturdier and settle
faster. Lower damping makes it ring more. If the table visibly bounces too many
times after one keyboard nudge, raise the damping or lower the visual strength.

## Visual Nudging

Visual nudge is only presentation. It moves the rendered table/cabinet in
response to the same cabinet offset used by the nudge model, but it does not add
extra physical force.

Keep visual strength modest. A little movement helps the player feel the nudge;
too much looks like camera shake and can make a physically reasonable nudge feel
wrong.

## Tilt

Tilt-bob routing and simulated plumb-bob tuning live on the
[`Tilt Bob`](xref:tilt_bobs) mechanism.

## Hardware Sensors

Hardware sensor mapping is handled by the `SimulationThreadComponent` nudge
sensor list and by the player app's cabinet input settings. These mappings are
specific to a cabinet, so avoid baking your personal KL25Z/Pinscape device ID
into a distributable table unless you are intentionally building a cabinet
profile for that setup.

Sensor types mean:

| Type | Use it for |
| --- | --- |
| `GamepadIntent` | Gamepad sticks or other analog controls where the player expresses "nudge this way". |
| `CabinetIntent` | A real cabinet sensor where motion should be interpreted as a nudge attempt. |
| `CabinetDirect` | A real cabinet sensor where measured motion should drive cabinet physics directly. |

Most Xbox-style controllers do not provide accelerometer data for nudging. Map
their sticks as `GamepadIntent`.

KL25Z/Pinscape boards are cabinet sensors. Map their acceleration axes, then use
mount rotation and mirror to match how the board is installed in the cabinet.
The available rotations are 0, 90, 180, and 270 degrees. Mirror flips the board X
axis before rotation.

## Editor Calibration

For direct play in the editor, use the `SimulationThreadComponent` inspector:

1. Enter Play Mode with the cabinet sensor connected.
2. Use auto-configuration if the first device should be mapped as the cabinet
   sensor.
3. Set mount rotation and mirror until graph movement matches cabinet movement.
4. Keep the cabinet still and calibrate sensor centers.
5. Nudge the cabinet lightly and watch the input graph.

Calibration captures the current resting raw value as zero. It does not remove
spikes, pick better axes, or tune the overall gain. If the graph spikes while the
cabinet is still, check the mapped axes, increase dead zone, reduce scale, or
inspect the physical board/device signal.

## Shipping Tables

Ship table defaults that feel good without special hardware. A player should be
able to play with keyboard nudging alone, and cabinet owners should be able to
override sensor mappings from their own configuration.

Good release checks:

- Test keyboard nudging with the intended `Keyboard Nudge Mode`.
- Verify visual nudge is noticeable but not distracting.
- If the table has a tilt bob, keep or add a `TiltBobComponent`, map the tilt switch to
  it, and verify it can tilt the table without tilting from ordinary play.
- If you include sensor defaults, confirm the table remains playable when those
  devices are absent.
- Do not rely on a specific personal device ID for public tables.
