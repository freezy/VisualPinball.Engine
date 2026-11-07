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

Tilt-bob routing and the table's default plumb-bob tuning live on the [`Tilt Bob`](xref:tilt_bobs) mechanism.

In the Player's Nudge settings, **Tilt Source** offers **Simulated + Physical** and **Physical Only**. A physical Tilt binding can trigger the table's Tilt Bob in either mode. With **Simulated + Physical**, choose **Custom** under **Plumb Tuning** to override damping and the trip angle for all tables. Higher damping helps the bob settle faster. A lower threshold makes it easier to trip.

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

Recognized gamepads offer a Left Stick preset for `GamepadIntent` nudging. The Player's Auto-map uses a single recognized gamepad when no cabinet sensor is available. For a generic joystick, assign the axes manually.

KL25Z/Pinscape boards are cabinet sensors. Map their acceleration axes, then use mount rotation and mirror to match how the board is installed in the cabinet. The available rotations are 0, 90, 180, and 270 degrees. Mirror flips the board X axis before rotation.

### Pinscape Joystick Setup

Recognized Pinscape joystick layouts offer acceleration and velocity presets on Windows, Linux, and macOS in the Player's Nudge settings and the `SimulationThreadComponent` inspector. Acceleration uses X/Y with an initial full-scale value of 9.80665 m/s² (1 g). Set the scale to match the range configured in firmware. The Player offers 1, 2, 4, and 8 g presets and an editable custom scale. Negative scale reverses an axis.

Velocity setup uses RX/RY with the default Pinscape joystick full-scale value of 0.2048 m/s. Enable velocity reporting in firmware before selecting this preset. Pinscape Pico can assign arbitrary signals to joystick axes, so use the named axis selectors and custom scales when its layout differs from the standard Pinscape layout. Velocity mode uses the reported integrated velocity directly through VPE's cabinet sensor model.

Auto-map recognizes cabinet layouts and explicitly typed accelerometer axes. Generic joystick X/Y axes require manual assignment. The Player adds a sensor slot when auto-mapping and preserves existing slots. The inspector's setup buttons configure its first sensor slot. Player settings are saved in the cabinet profile and applied to loaded tables.

When several eligible devices are available, use **Nudge Device** in the Player's Nudge settings to choose which device supplies analog nudging. **Automatic** uses all mapped sensors.

### Pinscape Pico Open Pinball Device Setup

Enable Open Pinball Device in the Pico firmware. With an OPD-capable NativeInput library, VPE lists the Pico with an OPD suffix and named nudge axes on Windows, Linux, and macOS. Auto-map prefers OPD when both interfaces are present. If you already mapped the Pico's joystick axes, select the OPD preset in that sensor slot to switch interfaces and then calibrate again.

Auto-map uses acceleration. Choose the velocity preset if the firmware reports integrated X/Y nudge velocity. The Pico velocity preset uses 0.32768 m/s at full scale, matching the default firmware `vscale` of 100 counts per mm/s. Set custom scales if you use a different `vscale` or accelerometer range. The report format allows firmware to send zero for unused fields, so confirm which signals your firmware exposes.

Preassigned OPD buttons use the existing VPE cabinet actions, including digital nudge and physical tilt. OPD also exposes plunger position and speed for diagnostics.

For a local Engine checkout, follow the [development setup](../../developer-guide/setup.md) to build the dependencies. The Engine build copies the NativeInput library from its pinned NuGet package to the platform's plugin directory. Linux needs an X11 display and USB device permissions. On Linux and macOS, reconnect the board to the same USB port to preserve its mapping. See the [NativeInput setup instructions](https://github.com/VisualPinball/VisualPinball.NativeInput#open-pinball-device) for platform details.

### Nudge and Tilt Bindings

In the Player's Nudge settings, the **Button Bindings** section accepts joystick and gamepad buttons, POV directions, axis thresholds, and generic OPD buttons for left, right, and forward nudge, Tilt, and Slam Tilt. Use **Press to Bind**, move or press one control, and release it. To require several controls together, use **Bind Combo**, hold the controls together, and release them all. Each binding is an alternative for its action. You can also pick a single control under **Add Alternative** and adjust its threshold if it is an axis. These bindings supplement keyboard and predefined OPD inputs and are saved in the cabinet profile.

### Rest Calibration

In the Player, open the Nudge settings and choose Calibrate under Rest Calibration. This works from the main menu before loading a table. In the editor, enter Play Mode and choose Calibrate Centers in the `SimulationThreadComponent` inspector.

Keep the cabinet still for about one second. Calibration averages fresh input reports for every active mapped channel. It checks sample count, sample duration, and input variation before updating any center. A disconnected device, stale reports, or excessive motion causes calibration to fail with the saved centers intact. Changing settings or leaving the tab cancels a running calibration.

Calibration adjusts neutral centers. Gain, axis selection, mount orientation, and dead zone remain explicit settings. Use the signed raw input labels to check side and forward directions, then check the cabinet acceleration/offset graph while a table runs. A disconnected sensor is cleared on the physics thread and resumes from rest when all of its mapped devices reconnect.

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
