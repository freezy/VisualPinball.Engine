---
uid: magnets
title: Magnets and Turntables
description: Configure playfield, spatial, and cylindrical magnets, ball holds, coils, and spinning disc turntables.
---

# Magnets and Turntables

VPE simulates magnets inside the physics loop, so their force, electrical response, and ball capture are updated every physics tick. Use a **Magnet** for attraction, holding, legacy VPX repulsion, or a mech that carries a ball. Use a **Turntable** for a spinning disc that pushes a ball tangentially.

## Magnet Setup

Add a **Magnet** component with *Add Component -> Pinball -> Mechs -> Magnet*. Its transform defines the field origin. Playfield and Spatial magnets create only a force field, so their solid hardware needs a separate collider. A Cylindrical magnet can also generate its own exact circular sidewall collider.

All Magnet distance fields use **VPX units**, not millimeters or Unity world units. A standard pinball has a radius of 25 VPX units, which is a useful reference when choosing distances.

> [!IMPORTANT]
> Magnet distance fields in older VPE versions were authored in millimeters. When upgrading an existing table or code that sets `MagnetApi.Radius`, multiply the old value by **1.85271** to preserve the same physical distance. New Magnet components use converted defaults, so no adjustment is needed for a newly added component.

### Choose a Magnet Type

| Magnet Type | Field shape | Typical use |
|---|---|---|
| **Playfield** | A vertical field around a point on the playfield. Distance is measured in the playfield plane. | Under-playfield magnets, Magna-Save, and imported `cvpmMagnet` behavior. |
| **Spatial** | A sphere around the transform. Distance is measured from the transform to the ball center in 3-D. | A mouth, hand, wand, or other mech that catches and carries a ball. |
| **Cylindrical** | A field around the exterior sidewall of a finite upright cylinder. Distance is measured from the ball surface to the sidewall or its top and bottom rim. The cap faces are not magnetic surfaces, and attraction fades to zero as the ball centre moves inward over a cap. | An exposed magnet core that balls can approach and touch from any direction around its side. |

Playfield and Spatial magnets treat the transform as the center of the field. A Cylindrical magnet treats the transform as the center of the cylinder base; **Cylinder Height** extends upward along the playfield normal. Tilted cylinder axes are not modeled.

### Understand the Distance Fields

The fields have different jobs and are not interchangeable:

| Inspector field | Meaning |
|---|---|
| **Influence Radius / Influence Distance** | The outer cutoff of the field. A ball beyond this distance receives no force. Playfield and Spatial modes measure from the transform to the ball center. Cylindrical mode measures the empty air gap between the ball and the exterior cylinder sidewall. |
| **Pole Radius** | Shapes a Physical Playfield or Spatial field. It is not used by a Cylindrical magnet. |
| **Grab Radius** | Defines the capture area around a Playfield or Spatial magnet's center. It is not shown for a Cylindrical magnet, which grabs automatically at contact. |
| **Cylinder Radius** | The radius of the magnetic cylinder itself. Match it to the associated collider, in VPX units. It does not control how far the field reaches. |
| **Cylinder Height** | The height from the component origin to the top of the sidewall, in VPX units. Match it to the associated collider. A value of zero makes the sidewall vertically unlimited. |
| **Generate Cylinder Collider** | Creates one smooth, circular sidewall collider using Cylinder Radius and Cylinder Height. It requires a positive height. Disable any mesh collider covering the same cylinder. If that collider is on the Magnet GameObject itself, move it to a separate GameObject before disabling it so the two components do not share one physics item ID. |
| **Height Range** | The vertical window above a Playfield magnet. Use it to prevent a magnet from affecting balls on an overhead ramp. Zero removes the limit. Spatial and Cylindrical modes ignore this field. |

The Cylindrical controls are deliberately simpler. **Strength** controls the pull at the metal surface. **Influence Distance** says how far the pull reaches outside that surface: relative to the configured contact pull, force is 100% at contact, 50% at half the Influence Distance, and zero at or beyond the Influence Distance. There is no separate falloff setting.

**Damping** is viscous drag, not collider friction. It damps motion toward or away from the cylinder throughout the field. It also damps motion around the cylinder and ball spin around the cylinder's upright axis only near the sidewall: the effect is full within 1 VPX unit of the wall, blends smoothly, and is gone by 2 VPX units. This lets Damping settle a captured ball without slowing an ordinary fly-by farther out in the field. It does not change vertical velocity or rolling spin around horizontal axes. Set it to **0** for no magnetic damping, **1** for the standard response, or higher for faster settling. Lower it below 1 when the real ball should swing back and forth or spin longer before coming to rest.

For an exposed cylindrical magnet:

1. Place the Magnet transform at the center of the collider's bottom face.
2. Set **Cylinder Radius** and **Cylinder Height** to the visible cylinder dimensions in VPX units. If the solid geometry is a child Primitive Collider, click **Fit Cylinder to Child Collider Mesh** to fill both values automatically.
3. Enable **Generate Cylinder Collider**. If the visible cylinder already has a Primitive Collider or Mesh Collider, disable that collider so the ball does not contact both shapes. A collider on the Magnet GameObject must first be moved to its own GameObject; disabling a co-located collider would disable their shared physics item ID.
4. Set **Influence Distance** to the maximum air gap where the ball should begin to bend toward the magnet.
5. Set **Strength** for the amount of pull at contact.
6. Set **Damping** for how quickly motion along the sidewall should settle after contact.
7. Enable **Grab Ball** if a ball that reaches the metal should close `ball_held` and remain pressed against the collider while the field can retain it.

The Cylindrical field measures its air gap from the closest point on the exterior sidewall or either boundary rim. The flat cap faces are excluded, and attraction fades as the ball centre moves inward over a cap until it reaches zero inside the cylinder radius. Magnetic acceleration is always parallel to the playfield and directed toward the cylinder axis; it never adds or removes vertical velocity. Grab has no distance setting: sidewall contact starts a hold when the active field is strong enough to retain the ball's separating motion and the ball is not moving outward past a finite top or bottom rim. A moving ball can glide around the cylinder while Damping gradually reduces the swing. Gravity still acts along the sidewall, so a fixed upright cylinder naturally settles the ball at its downhill middle point.

**Generate Cylinder Collider** creates real VPE collision geometry, not a hidden movement constraint. It is an exact circle at every angle, so the outward contact normal always points directly away from the cylinder axis. The magnet pulls in the opposite radial direction, the collider balances that inward load, and gravity remains free to move the ball around the sidewall toward the downhill point. A polygonal mesh cylinder has flat faces and edge contacts instead; using it at the same time can apply two contacts or create false resting directions, so disable the overlapping mesh collider. The ball remains a normal live physics object and can roll, swing, collide, and separate. The generated sidewall starts with zero collider friction; use magnetic Damping to control swing and spin settling without adding another authoring control.

### Other Magnet Fields

| Field | Description |
|---|---|
| **Strength** | The authored full-power force control. VPX Compatible uses familiar `cvpmMagnet` strength values. Physical, Spatial, and Cylindrical modes always attract and use the magnitude of this value, but their numeric acceleration is calculated by their respective field models. |
| **Force Profile** | Selects **VPX Compatible** or **Physical** for a Playfield magnet. Spatial and Cylindrical magnets always use physical electrical response and force semantics. |
| **Coil Rise Time** | Electrical rise time constant in milliseconds. Current reaches about 63% of a steady command after one time constant. |
| **Coil Fall Time** | Electrical decay time constant in milliseconds after the command is reduced or switched off. |
| **Grab Ball** | Enables capture and hold behavior. Cylindrical magnets acquire at contact; Playfield and Spatial magnets use Grab Radius. |
| **Generate Cylinder Collider** | For a Cylindrical magnet, creates the smooth solid sidewall needed for stable contact. Disable overlapping colliders. |
| **Is Enabled On Start** | Starts the field at full command before a coil or script changes it. |
| **Is Kinematic** | Updates the field origin from the GameObject transform during gameplay. Leave this disabled for a fixed magnet. |
| **Draw Debug Forces** | Draws play-mode force lines and a runtime coil-status gizmo. The magnet surface and marker are green while the coil is on and red while it is off. |

The selected object also shows scene-view gizmos. Blue indicates the physical field shape and influence boundary, orange indicates the grab boundary for point magnets, and purple indicates Pole Radius for a Physical Playfield or Spatial magnet. These gizmos use the authored VPX dimensions directly and do not change size when the magnet model's Transform scale changes.

## Electrical Response and PWM

Magnets expose one coil:

| Device item | Description |
|---|---|
| `magnet_coil` | Sets the normalized magnet command while the coil is active. |

Map a ROM solenoid to `magnet_coil` in the [Coil Manager](../../editor/coil-manager.md). The command can be any value from 0 to 1; it is not reduced to a simple on/off flag.

**VPX Compatible** applies the command immediately and scales **Strength** directly. The physical modes use **Coil Rise Time** and **Coil Fall Time** to model effective current, then calculate force from current squared. A 25% command therefore produces much less than 25% of full physical force, and a short pulse may end before the current reaches its commanded level.

Tune **Strength** at 100% current first. If the game drives the coil with short or partial-power pulses, test with those real commands instead of tuning only with the coil held fully on.

## Force Profiles

### VPX Compatible

Use **VPX Compatible** for an imported table or a port that already has tuned `cvpmMagnet` values. It follows the legacy VPX force curve and normalizes it to VPE's 1 kHz physics loop. Positive strength attracts and negative strength repels, which preserves scripts that use fictional magnetic repulsion.

This profile applies only to a Playfield magnet. Its attraction acts in the playfield plane and can be limited vertically with **Height Range**.

### Physical

Use **Physical** for a newly authored under-playfield magnet. Its field ramps with coil current, weakens with vertical air gap, and tapers smoothly to zero at **Influence Radius**. The lateral force is zero on the center axis and strongest in an annulus determined by **Pole Radius**.

A physical magnet always attracts an ordinary steel pinball. Reversing coil polarity changes the magnetic flux but not the direction of attraction. Use VPX Compatible only when a legacy script intentionally needs negative-strength repulsion.

Physical strength values are not equivalent to VPX Compatible values. Tune them in play mode while observing ball speed, air gap, PWM command, and capture behavior.

## Ball Capture and Release

Magnets also expose one switch:

| Device item | Description |
|---|---|
| `ball_held` | Closes while one or more balls are currently grabbed. |

With **Grab Ball** disabled, the magnet only applies attraction. With it enabled, Playfield and Spatial magnets attempt capture inside Grab Radius. A Cylindrical magnet attempts capture when the ball touches its sidewall; it compares motion separating the ball from the wall with the strength available across the field and rejects vertical motion that its planar force cannot retain. A weak field may fail to retain a fast rebound, while fast motion along the cylinder is allowed.

VPX Compatible capture snaps the ball to the Playfield magnet's planar center. Physical Playfield and Spatial capture use a capped spring-damper force, so the ball decelerates visibly and a sufficiently hard collision can knock it away. Cylindrical capture retains motion that separates from the surface without locking the ball to one point around the cylinder or driving it continuously into the collider.

Turning the coil off or calling `ReleaseBall()` removes the hold without freezing or teleporting the ball. The ball keeps its current velocity. `Eject(speed, angleDeg, verticalAngleDeg)` releases and throws held balls; the horizontal angle follows the kicker convention, while the optional vertical angle applies to Spatial and Cylindrical magnets.

## Spatial and Kinematic Magnets

Use **Spatial** when a mechanism must carry a ball away from the playfield. The transform is the 3-D hold point, and a grabbed ball is pulled toward it while remaining a normal colliding physics object.

Enable **Is Kinematic** only when the magnet's transform moves during gameplay. The field then follows the transform position and a held ball inherits the carrier motion. Playfield and Cylindrical fields remain aligned to the playfield normal; transform rotation does not tilt their field axis.

## Turntable Setup

Add a **Turntable** component with *Add Component -> Pinball -> Mechs -> Turntable*. Place it at the disc center and set **Radius** to cover the area where the spinning disc should affect the ball. Turntable Radius and Height Range are authored in millimeters, as indicated by their inspector unit labels. **Height Range** prevents the disc from affecting balls on ramps above it; zero removes that limit.

Turntables expose two coils:

| Device item | Description |
|---|---|
| `motor_coil` | Turns the motor on while active. |
| `direction_coil` | Selects clockwise while active and counter-clockwise while inactive. |

The turntable ramps toward **Max Speed** using **Spin Up**, then ramps toward zero using **Spin Down** after the motor turns off. Assign **Rotation Target** to the visible disc mesh if it should rotate with the simulated speed. Enable **Is Kinematic** only if the entire turntable moves during gameplay.
