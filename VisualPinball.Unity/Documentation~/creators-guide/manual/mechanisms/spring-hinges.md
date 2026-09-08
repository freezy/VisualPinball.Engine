---
uid: spring_hinges
title: Spring Hinge Bash Toys
description: Author a spring-returning toy with reciprocal ball impact and an optional moving magnet.
---

# Spring Hinge Bash Toys

A Spring Hinge simulates a rigid toy rotating about one fixed axis. Balls push its analytic box with finite inertia, and its spring and damping return it toward an equilibrium angle. An optional child Spatial magnet can attract and carry one ball while applying the equal reaction torque to the toy.

## Create a Toy

For an existing model, make the rotating object's local origin the physical pivot, select that object, and choose **GameObject > Pinball > Add Spring Hinge**. VPE adds Spring Hinge, Spring Hinge Collider, and Spring Hinge Transform to the selected object and disables its independent colliders. Other selected moving parts are parented below the active object while preserving their world poses. Keep fixed brackets outside this hierarchy, then use the scene handles to set the axis, centre of mass, analytic box, and travel limits.

Choose **GameObject > Pinball > Spring Hinge Bash Toy** to create a complete example object with a cube visual, analytic box, transform driver, and owned magnet. The bash preset starts at 0 degrees and stops at 20 degrees with a low-elasticity box.

Spring Hinge Transform applies the physics angle directly to the rotating object around its local origin. The simulation caches the authored rest pose and owns this angle; do not animate or move the object from another behavior during play.

## Mass, Spring, and Stops

**Toy Mass** is relative to VPE's standard ball mass. A value of 1 means one standard ball mass; it is not kilograms. **Fit From Renderers** fills the centre of mass, inertia-estimate box, and collision proxy from the rotating object's renderers and their children in millimeters. This is a conservative bounds fit, not mesh-volume integration, so move the centre marker and mass box when the toy is hollow or uneven. Enable **Override Inertia** for a measured or separately calculated moment of inertia.

**Spring Stiffness** and **Spring Damping** are torsional values in VPE's normalized simulation units. **Equilibrium Angle** is the one canonical rest/preload setting and may lie beyond a stop to hold the toy against it. Stops have zero restitution: the toy may leave a stop immediately when an inward impulse or spring torque acts.

The optional angle switch closes at **Switch Close Angle** and opens at **Switch Open Angle**. Use different thresholds to avoid chatter.

## Analytic Collision Box

Version one supports one oriented box attached to the hinge. Edit its local centre, rotation, and half-extents independently of the mass box. The scene view shows its current pose and both travel limits. Remove or disable mesh and static colliders on the moving visual so a ball cannot contact two representations.

Enable **Show Collider** on Spring Hinge Collider to display the current analytic box in green in the Scene view. This follows the simulated hinge angle during Play Mode.

The box is continuous-collision tested against the ball in 3-D, including its faces, edges, corners, and return travel. The qualified envelope uses the 1 ms physics tick, a loaded period of at least 0.314 seconds, `hold frequency × tick <= 0.2`, positive proxy half-extents, and ordinary pinball shot speeds from 8 to 30 VPU per normalized time. Conservative advancement is bounded to 32 steps, followed by at most 32 local fallback segments and 14 refinements. A table relying on sustained force-cap saturation, a stiffer/faster mechanism, or penetration above 0.5% of ball radius needs a narrower proxy, a softer configuration, or further qualification.

## Couple a Magnet

Create a child GameObject below the rotating object, position it at the physical magnet pole in the toy, and add a Magnet component. Here, "below" means a descendant in the Unity hierarchy; the magnet may physically sit anywhere in the toy, such as Mechagodzilla's belly. Select **Spatial** and **Physical**, then enable **Couple To Parent Hinge**. The child inherits the toy's rotation and resolves the Spring Hinge from its parent. Only one owned magnet and one attached ball are supported per hinge; the inspector rejects unsupported types or duplicates.

The magnet transform is the moving pole. **Held Ball Centre Offset** is a separate target in millimeters at the authored rest pose. Place it outside the box at the intended collision face; for a standard 25-unit-radius ball, begin one radius beyond the face. Adjust it for a different ball radius. The green scene marker shows the target.

**Hold Stiffness** and **Hold Damping** control attachment compliance. **Max Hold Force** is the current-dependent capacity. These settings are independent of Influence Radius. Tune the field to attract the ball, then tune capacity and compliance so the intended shot captures without living at the force cap. Turning the coil off honors coil decay before release. Release preserves ball and hinge velocity.

## Validate in Play Mode

Test both magnet-off impacts and magnet-on capture, a timed release in each travel direction, a second-ball strike, and balls resting on passive playfield geometry.

## First-release Limits

- The pivot frame is fixed and must have nonzero orthogonal axes. Moving bases, shear, nested hinges, hinge-to-hinge contact, motors, and flexible toys are not supported.
- Collision uses one box proxy. Triangle, compound, and arbitrary mesh proxies are not supported.
- One Spatial Physical magnet may own one ball. Other balls remain free and can strike the toy or held ball.
- Playfield and passive-surface support are qualified by the current sequential solver. Simultaneous squeezed contacts are an approximation and can retain a one-tick support lag.
- If an attached ball reaches a flipper, plunger, kicker, bumper, slingshot, or turntable, VPE releases it before the existing active mechanism runs and emits a rate-limited diagnostic. Place the held-ball sweep away from those mechanisms.
- Runtime save states and an isolated editor physics preview are not included. Packaged tables preserve authored values and hierarchy, not captured-ball IDs or warm solver state.
