---
uid: magnets
title: Magnets and Turntables
description: Native playfield magnets, ball holds, VPX repel/eject behavior, and spinning disc turntables.
---

# Magnets and Turntables

VPE provides native mechanisms for playfield magnets and spinning magnetic discs. They run inside the physics loop, so they can react every physics tick instead of waiting for a script timer.

Use **Magnet** for radial attraction, grab/hold behavior, VPX-compatible repulsion, and eject-style mechanisms. Use **Turntable** for a spinning disc that pushes the ball tangentially, such as VPX `cvpmTurnTable` mechanisms.

## Magnet Setup

Add the **Magnet** component to a GameObject with *Add Component -> Pinball -> Mechs -> Magnet*. Place the GameObject at the magnet core or hold position. The component uses the transform position as the center of the force field.

The selected object shows a radius gizmo in the scene view. Playfield magnets draw a cylinder; spatial magnets draw a sphere. If grab is enabled, a smaller grab radius is drawn as well.

| Field | Description |
|---|---|
| **Magnet Type** | **Playfield** for under-playfield magnets with a cylindrical range, **Spatial** for mech-mounted magnets that grab and carry balls in 3-D. |
| **Radius** | Influence radius in millimeters. Playfield magnets use a planar radius; spatial magnets use a spherical radius. |
| **Height Range** | Vertical window above a playfield magnet. Use this to avoid affecting balls on ramps above the playfield. Spatial magnets ignore this field. |
| **Strength** | Full-power magnet force. In VPX Compatible mode, this uses familiar `cvpmMagnet` strength values and negative values repel. |
| **Force Profile** | **VPX Compatible** for imported VPX behavior, **Physical** for new VPE tables that use the finite-pole field model. Spatial magnets always use physical 3-D force semantics. |
| **Pole Radius** | Effective radius of the circular or annular pole face. The Physical force is strongest in a ring near this pole and is zero laterally at the exact center. |
| **Coil Rise Time** | Electrical rise time constant in milliseconds for Physical and Spatial magnets. Current reaches about 63% after one time constant. |
| **Coil Fall Time** | Electrical decay time constant in milliseconds for Physical and Spatial magnets. Tune this to the coil driver and flyback circuit. |
| **Grab Ball** | Enables center hold behavior inside the grab radius. |
| **Grab Radius** | Capture volume for grab mode. VPX Compatible snaps on entry. Physical and Spatial magnets capture only when the current field can arrest the ball before it leaves this volume, then use a spring-damper hold that a hard hit can overcome. |
| **Is Enabled On Start** | Starts the magnet on before a coil or script changes it. |
| **Is Kinematic** | Moves the magnetic field with the GameObject transform during gameplay. Use this when the magnet is mounted on a moving mech. |
| **Draw Debug Forces** | Draws play-mode force vectors for balls inside the radius. |

## Coils and Switches

Magnets expose one coil:

| Device item | Description |
|---|---|
| `magnet_coil` | Enables the magnet while the coil is active. |

Magnets also expose one switch:

| Device item | Description |
|---|---|
| `ball_held` | Closes while one or more balls are grabbed by the magnet. |

Map ROM solenoids to `magnet_coil` in the [Coil Manager](../../editor/coil-manager.md). The `ball_held` switch can be used by game logic when a table needs explicit confirmation that a ball is held.

Some ROMs pulse-width-modulate (PWM) their magnet coils to vary grip — Iron Man, for example, drives its magnets at partial strength. The normalized coil command travels through the simulation-thread path without being reduced to a boolean. VPX Compatible scales its authored **Strength** directly by that command. Physical and Spatial magnets instead integrate effective coil current using **Coil Rise Time** and **Coil Fall Time**, then derive force from current squared. Set **Strength** to the full-power grip at 100% current.

## Force Profiles

### VPX Compatible

Use **VPX Compatible** for imported tables or ports that already have tuned `cvpmMagnet` values. This profile follows the VPX magnet force curve and normalizes it to VPE's 1 kHz physics loop.

This mode is the default. It is the right choice for ROM-controlled magnets and Magna-Save style fields.

### Physical

Use **Physical** for new VPE-authored tables. Its electrical response ramps toward the commanded current instead of switching the field instantaneously, and the field decays after power is removed. The force uses a finite axisymmetric pole approximation: lateral force is zero on the center axis, strongest in an annulus near **Pole Radius**, weakens with vertical air gap, and falls rapidly outside the pole. **Radius** is a hard performance boundary with a smooth force taper, so the magnet never evaluates an infinite tail.

Physical magnets always attract an ordinary steel pinball. Reversing coil polarity changes magnetic flux direction but not the direction of attraction. Use VPX Compatible when a legacy script intentionally uses negative strength as a fictional repelling force.

Physical grab uses a capped spring-damper hold, so the ball visibly decelerates instead of snapping to the center. Capture is based on relative speed, remaining grab distance, and effective field strength; a fast fly-by or weak PWM command does not become an artificial full-strength hold.

Physical strength values are not VPX strength values. Start with a larger value than you would use in VPX Compatible mode and tune by watching ball speed and catch behavior in play mode.

## Spatial Magnets

Use **Magnet Type: Spatial** for mechanisms that physically carry the ball away from the playfield, such as a mouth, hand, or wand mounted on a moving mech. Spatial magnets use a spherical radius around the transform and treat the transform as the ball center hold point.

When **Grab Ball** is enabled, a ball inside **Grab Radius** is pulled to that 3-D hold point by a strong magnetic force and held there. The ball stays a live physics object throughout — it is not frozen — so it renders at its real position and other balls collide with it normally. If **Is Kinematic** is enabled and the GameObject moves during gameplay, the hold force drags the ball along in x, y, and z. Turning the coil off or calling `ReleaseBall()` simply drops the hold, and the ball continues with whatever velocity it has. `Eject(speed, angleDeg, verticalAngleDeg)` throws it directionally; the first angle uses the same convention as kickers, and the optional vertical angle lifts or drops the shot.

Because the hold is a force rather than a rigid lock, a hard enough hit from another ball can overcome it and knock the ball loose (and that ball can in turn be grabbed). A new ball is marked held only after the current field can arrest its relative motion inside **Grab Radius**. **Strength** sets how hard the magnet holds — a stronger magnet needs a harder hit to dislodge its ball. This is not a levitation model: use **Radius** and **Strength** to catch a nearby ball, not to pull one across the table or balance it far below the hold point.

## Turntable Setup

Add the **Turntable** component with *Add Component -> Pinball -> Mechs -> Turntable*. Place it at the disc center and set **Radius** to cover the area where the spinning disc should affect the ball.

Turntables expose two coils:

| Device item | Description |
|---|---|
| `motor_coil` | Turns the motor on while active. |
| `direction_coil` | Selects clockwise while active and counter-clockwise while inactive. |

The turntable ramps toward **Max Speed** using **Spin Up**, then ramps back toward zero using **Spin Down** when the motor turns off. Assign **Rotation Target** to the visible disc mesh if you want it to rotate with the simulated speed.

## Kinematic Magnets

Enable **Is Kinematic** when a magnet or turntable is parented under a moving transform. The physics engine tracks the transform during gameplay, so the force field follows the moving center. A held ball is dragged along by the hold force — planar for playfield magnets, full 3-D for spatial magnets.

Kinematic tracking follows the transform position and height. The magnetic field remains playfield-aligned for playfield magnets; tilted field axes are not modeled. Spatial magnets are the supported path for carrying a held ball away from the playfield.
