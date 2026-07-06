---
uid: magnets
title: Magnets and Turntables
description: Native playfield magnets, ball holds, repel/eject behavior, and spinning disc turntables.
---

# Magnets and Turntables

VPE provides native mechanisms for playfield magnets and spinning magnetic discs. They run inside the physics loop, so they can react every physics tick instead of waiting for a script timer.

Use **Magnet** for radial attraction, grab/hold behavior, repulsion, and eject-style mechanisms. Use **Turntable** for a spinning disc that pushes the ball tangentially, such as VPX `cvpmTurnTable` mechanisms.

## Magnet Setup

Add the **Magnet** component to a GameObject with *Add Component -> Pinball -> Mechs -> Magnet*. Place the GameObject at the magnet core or hold position. The component uses the transform position as the center of the force field.

The selected object shows a radius gizmo in the scene view. Playfield magnets draw a cylinder; spatial magnets draw a sphere. If grab is enabled, a smaller grab radius is drawn as well.

| Field | Description |
|---|---|
| **Magnet Type** | **Playfield** for under-playfield magnets with a cylindrical range, **Spatial** for mech-mounted magnets that grab and carry balls in 3-D. |
| **Radius** | Influence radius in millimeters. Playfield magnets use a planar radius; spatial magnets use a spherical radius. |
| **Height Range** | Vertical window above a playfield magnet. Use this to avoid affecting balls on ramps above the playfield. Spatial magnets ignore this field. |
| **Strength** | Magnet force. In VPX Compatible mode, this uses familiar `cvpmMagnet` strength values. Negative values repel. |
| **Force Profile** | **VPX Compatible** for imported VPX behavior, **Physical** for new VPE tables that want a smoother inverse-square force. Spatial magnets always use physical 3-D force semantics. |
| **Grab Ball** | Enables center hold behavior inside the grab radius. |
| **Grab Radius** | Radius where the magnet starts holding the ball. VPX Compatible playfield mode clamps to center; Physical playfield mode uses a spring-damper hold; Spatial mode uses a 3-D spring-damper hold that a hard hit can overcome. |
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

## Force Profiles

### VPX Compatible

Use **VPX Compatible** for imported tables or ports that already have tuned `cvpmMagnet` values. This profile follows the VPX magnet force curve and normalizes it to VPE's 1 kHz physics loop.

This mode is the default. It is the right choice for ROM-controlled magnets and Magna-Save style fields.

### Physical

Use **Physical** for new VPE-authored tables. It uses a saturated inverse-square force so the field gets stronger near the core without exploding at zero distance. Physical grab uses a capped spring-damper hold, so the ball visibly decelerates instead of snapping to the center.

Physical strength values are not VPX strength values. Start with a larger value than you would use in VPX Compatible mode and tune by watching ball speed and catch behavior in play mode.

## Spatial Magnets

Use **Magnet Type: Spatial** for mechanisms that physically carry the ball away from the playfield, such as a mouth, hand, or wand mounted on a moving mech. Spatial magnets use a spherical radius around the transform and treat the transform as the ball center hold point.

When **Grab Ball** is enabled, a ball inside **Grab Radius** is pulled to that 3-D hold point by a strong magnetic force and held there. The ball stays a live physics object throughout — it is not frozen — so it renders at its real position and other balls collide with it normally. If **Is Kinematic** is enabled and the GameObject moves during gameplay, the hold force drags the ball along in x, y, and z. Turning the coil off or calling `ReleaseBall()` simply drops the hold, and the ball continues with whatever velocity it has. `Eject(speed, angleDeg, verticalAngleDeg)` throws it directionally; the first angle uses the same convention as kickers, and the optional vertical angle lifts or drops the shot.

Because the hold is a force rather than a rigid lock, a hard enough hit from another ball can overcome it and knock the ball loose (and that ball can in turn be grabbed). **Strength** sets how hard the magnet holds — a stronger magnet needs a harder hit to dislodge its ball. This is not a levitation model: use **Radius** and **Strength** to catch a nearby ball, not to pull one across the table or balance it far below the hold point.

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

## Importing VPX Magnets

Use *Pinball -> Tools -> Detect Magnets* to scan a VPX script after import. The tool looks for `cvpmMagnet` and `cvpmTurnTable` declarations, matches their trigger references, creates VPE components, and adds coil mappings when the script uses direct numeric solenoid callbacks.

Review every detected item before creating components. Script expressions, custom helper classes, or nonstandard callback indirection may need manual coil mapping after detection.
