---
uid: drop-target-physics
title: Drop Target Physics
description: Choose and calibrate legacy, Roth-compatible, or mechanical drop-target behavior.
---

# Drop Target Physics

Drop targets provide three physics modes. Existing tables and prefabs remain on **Legacy** unless an author explicitly changes the mode.

## Choosing a mode

| Mode | Use it for | Behavior |
|---|---|---|
| **Legacy** | Existing tables and maximum compatibility | Preserves the original solid-wall hit and animation behavior. |
| **Roth Compatible** | Tables that previously used the common Roth `DTHit`/`DTBallPhysics` scripts | Uses a non-solid front sensor and offset collision wall, with the Roth mass correction, optional brick heuristic, backside threshold, and deterministic optional vertical bouncer. |
| **Mechanical** | New tables and physical calibration work | Simulates a finite-mass moving blade, rear spring and stop, latch release/relatch, vertical guide, switch crossings, and a moving reset stroke. Bricks and ball lift emerge from contact and mechanism parameters. |

Mechanical mode is the most physical model, but its generic defaults are deliberately marked **provisional**. A profile should only be marked **Measured** after it has been fitted to real-machine footage and validated against shots that were not used for fitting.

## Collider meshes

The collider component exposes three mesh slots:

- **Front Collider** remains the legacy solid face. In Roth mode it becomes the non-solid hit sensor.
- **Back Collider** represents the passive rear/body surface.
- **Collision Collider** is the dedicated offset wall in Roth mode and the moving physical face in Mechanical mode.

For Roth parity, author a separate front sensor and offset collision mesh. If Collision Collider is empty, VPE retains a solid-front fallback, but it cannot exactly reproduce the sensor-plus-offset-wall arrangement. Mechanical mode can fall back to Front Collider, though a simplified dedicated collision mesh is preferable for predictable contact and lower broad-phase cost.

## Mechanical profiles

Create a reusable profile with **Assets > Create > Pinball > Drop Target Physics Profile**. Assign it to each target built from the same mechanism. Enable the local override only for a measured per-target difference.

VPE includes **Generic Sliding Blade (Provisional)** as an explicit evaluation starting point. It is not physical calibration data and must remain labeled provisional.

The most influential parameters are:

- **Effective Face Mass**, material elasticity, and friction control the ball/target impulse and rebound.
- **Deflection Kind** selects a sliding blade or a hinged blade. Hinged axes, pivots, and reference contact points are authored in target-local VPX coordinates; hinge travel thresholds are radians, and effective face mass is defined at the reference point.
- **Latch Release Travel**, **Latch Relatch Travel**, and **Latch Escape Drop** define release versus brick timing.
- **Rear Spring Frequency**, damping, rear clearance, and stop restitution control blade recoil.
- **Drop Mass**, spring force, guide damping, and guide friction control downward escape.
- **Reset Duration**, **Reset Effective Mass**, overshoot, and settle time control contact-derived ball lifting during reset.

Scene gizmos show the latch release, rear stop, down stop, and reset overshoot for a selected Mechanical target. Inspector errors identify impossible masses, travel ordering, and switch thresholds.

## Events and game logic

The public drop-target API is unchanged:

- `Hit` fires for the first qualified face contact, including a brick.
- the dropped switch and `TargetEventsDropped` close once at the downward switch crossing;
- the raised switch and `TargetEventsRaised` open once during reset;
- setting `IsDropped = true` starts a physical forced drop;
- setting `IsDropped = false` starts the moving reset stroke.

Mechanical targets remain collidable while dropping and while re-entering the playfield. A ball resting above a resetting blade is lifted by the moving finite-mass contact solver; advanced modes do not assign a fixed vertical ball velocity.

The reset bar is modeled as a powered, prescribed trajectory. Its configured effective mass controls the ball impulse during contact, while the actuator resumes the authored trajectory on the next physics step. This intentionally represents energy supplied by the reset coil rather than an unpowered momentum-conserving collision.

## Calibration

Record front and side views with a scale marker at 240 fps or faster. Track ball center, rear blade deflection, vertical target travel, switch timing, and reset motion across controlled speeds, angles, and contact positions. Fit parameters on one set of shots and record validation error on held-out shots in the profile's **Calibration Source** field.

At minimum, document the mechanism/parts family, camera frame rate and scale, ball mass, sample count, fitted dataset, held-out dataset, and errors in post-impact velocity, peak deflection, drop timing, brick classification, and reset lift.

## Compatibility and export

The mode, third collider mesh, and profiles round-trip in VPE packages. Old packages without these fields load as Legacy, and mesh indices 0 and 1 retain their previous meaning.

VPX BIFF has no fields for VPE's mechanical target masses, latch geometry, springs, or reset actuator. VPX export therefore retains the ordinary target data but cannot preserve advanced physics settings. Keep the VPE package or Unity source as the authoritative editable copy, and use table scripts when a VPX-only distribution needs Roth-style behavior.

## Performance

Tables without Mechanical targets bypass the advanced update and contact-reduction paths. Idle latched and fully down Mechanical targets take a rest-state fast path. Moving targets update their collider transforms and the kinematic broad phase only while their pose changes. Prefer simple dedicated collision meshes, and profile a full active bank on the target platform before shipping.
