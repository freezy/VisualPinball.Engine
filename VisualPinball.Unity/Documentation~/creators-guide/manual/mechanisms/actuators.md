---
uid: actuator
title: Actuators
description: Drive one or more moving objects from a single coil input.
---

# Actuators

The **Actuator** component turns one gamelogic coil into a normalized mechanical position. Any number of **Actuator Transform** components can follow that position with independent translation, rotation, direction, and response curves. Use this combination for moving ramps, diverters, doors, bridges, toys, linkages, and other coil-driven geometry that does not have a dedicated VPE component.

## Setup

1. Add **Pinball > Mechs > Actuator** to a dedicated active GameObject below the table's Player.
2. Map its **Actuator** coil in the [Coil Manager](xref:coil_manager).
3. Put each moving object under a GameObject whose local origin matches the real mechanical pivot or linkage origin.
4. Add **Pinball > Animation > Actuator Transform** to every independently moving pivot and assign the same Actuator as its **Emitter**. A follower below the actuator in the hierarchy can also find it automatically.
5. Enable **Position**, **Rotation**, or both and author the offsets representing normalized position 1. The existing local transform is position 0. Translation defaults to **World**. Choose **Local** when the offset should follow the follower's authored red, green, and blue Local gizmo axes.
6. If the mechanism has position, limit, or encoder switches, add them under **Position Switches** and map the resulting items in the [Switch Manager](xref:switch_manager).
7. Mark every VPE collider that moves with these pivots as **Kinematic** before the table loads.

Children inherit their parent's movement, so decorative meshes and collision belonging to one rigid part should normally be grouped below one driven pivot. Add another follower only when a second rigid part needs a different offset, direction, or response curve.

## Edit-mode preview

Select the Actuator and scrub **Preview Position** to inspect every connected Actuator Transform without entering Play Mode or firing a coil. The slider uses each follower's position offset, rotation offset, response curve, and reverse setting. **Reset Preview** returns every follower to its authored local pose. Preview poses are also restored automatically before saving the scene, entering Play Mode, reloading scripts, quitting Unity, undoing, or leaving the Actuator inspector; they are never intended to become authored transform values. Preview is disabled for objects opened in Prefab Mode so a temporary pose cannot be saved into the prefab asset.

The preview slider changes only editor geometry. It does not emit position switches to the gamelogic engine.

## Position switches

Each actuator can expose any number of switches derived from its actual normalized position. The configured name appears in the Switch Manager, while VPE maintains a stable internal item ID so mappings remain intact when the display name changes. Position ranges are inclusive and use the same `0` to `1` travel value as the animation.

- **Enable Between** is a maintained switch. It becomes enabled while the actuator position is inside the configured range and disabled immediately after leaving it. Two narrow ranges near `0` and `1` model ordinary endpoint or limit switches; both will be disabled through the middle of travel.
- **Pulse Between** emits pulses at fixed normalized travel intervals while the actuator crosses a configured position range. Pulse marks are anchored at the beginning of the range and work in both travel directions.
- **Always Pulse** uses the same travel-based interval over the entire stroke. This is useful for simple encoder or cam-wheel feedback.

Set **Pulse Duration** on each pulse switch to control the minimum time its close edge remains active. When one frame crosses multiple marks, the actuator queues every mark and emits each close and open edge on a separate update frame so the gamelogic engine and direct wire consumers observe every transition. This limits sustained output to roughly half the Unity update frequency; choose a pulse interval appropriate for the mechanism's travel speed or queued feedback will lag behind its animation. `SnapTo(float)` recalculates maintained switch states, cancels pending pulses, and deliberately does not synthesize encoder pulses. The editor preview changes only geometry and does not affect any switch. These rules make restore and diagnostics deterministic rather than pretending that instantaneous repositioning was physical travel.

## Coil modes

### Follow Coil

The actuator travels toward position 1 while the coil is energized and toward 0 after it remains off for **Release Delay**. This fits held solenoids, motors, and ordinary energized/de-energized diverters.

### Toggle On Pulse

Every qualified rising edge swaps the target between 0 and 1. Repeated nonzero strength updates count as the same pulse, and short zero gaps below **Release Delay** do not rearm it. Use this for bistable or mechanically toggled mechanisms driven by brief pulses.

### One Shot

A rising edge travels to 1, waits for **One Shot Hold Duration**, then returns to 0. A held coil produces one cycle and must release before it can trigger another.

### Follow Value

The normalized coil duty cycle becomes the target position. Select this only for a genuinely proportional mechanism. It requires a plain coil mapping: wire and dynamic-wire paths carry only boolean state and cannot preserve a proportional value.

Binary modes never interpret coil strength as mechanism position. A value such as `0.25` can be a reduced-power solenoid pulse that still produces one complete mechanical stroke.

## Travel and curves

**Activation Duration** and **Release Duration** are full-stroke times. A command that starts midway through the stroke scales its duration by the remaining distance. Activation and release have independent curves; every follower can additionally reshape the shared position with its own **Response Curve** or select **Reverse**. A follower's **Translation Space** controls only the axes of **Position Offset**: **Local** uses the follower's authored Local gizmo orientation, while **World** uses global axes and continuously keeps the authored baseline attached to a moving parent. World offsets are measured in scene units; Local offsets inherit the parent's scale.

The actuator clamps its position to the range 0 through 1. Curves can ease movement but cannot drive collision beyond the authored endpoints. A zero duration, or the runtime API's `SnapTo`, is an immediate transform teleport intended for initialization, restore, or diagnostics; it is not a gameplay-safe way to push a ball.

## Moving collision

An Actuator Transform only moves a Unity local transform. It does not discover, configure, enable, disable, or change collider modes. Collision setup remains the table author's responsibility. VPE follows collision only when the corresponding collider component is active and marked **Kinematic** when the physics engine initializes. Do not enable the collider later or switch the kinematic flag during play and expect it to register dynamically.

Keep render and collision for a rigid moving part on the same transform branch. VPE derives linear and angular velocity from that branch's pose changes, allowing a moving ramp or bridge to interact with balls instead of merely changing appearance. Scale animation is deliberately unsupported; build moving mechanisms from rigid translated and rotated parts.

## Example: linked collapsing bridge

Use one actuator mapped to the bridge coil and set it to **Toggle On Pulse** when each pulse mechanically swaps the bridge state. Create left and right hinge pivots as separate followers of that actuator. Give one the measured positive hinge rotation and the other the measured negative rotation, and place each half's render mesh, side walls, and kinematic collider beneath its pivot. Fixed frame and coil-bracket geometry stays outside both moving branches.

If a destroyed bridge half should wiggle when struck by a ball, add that compliance on a separate child pivot below the actuator-driven hinge. Commanded bridge travel and impact response must not both write the same transform.

## Runtime API

`ActuatorApi` exposes the current `Position`, `TargetPosition`, and `IsMoving` state. Set `IsActive` or call `Toggle()` for authored travel, listen to `Reached` for endpoint completion, and use `SnapTo(float)` only for restore or diagnostics. A script command takes precedence until the next qualified coil edge; it deliberately does not rewrite the sampled physical coil state.

Treat `CoilMode` as authoring configuration rather than a live control. Changing modes during a pulse, stroke, or one-shot hold is unsupported; finish or restore the mechanism before changing it.

The component also publishes its normalized position through `IAnimationValueProvider<float>`, allowing custom reusable followers to consume the same motion without depending on a particular table or game.
