---
uid: developer-guide-spring-hinge-magnet-implementation-plan
title: Spring Hinge and Magnetic Bash Toy Implementation Plan
description: Proposed implementation of a ball-driven spring hinge and a magnet that transmits a captured ball's load to the hinge.
---

# Spring hinge and magnetic bash toy implementation plan

Status: implementation in progress, revised after Fable 5.1 review on 2026-09-07. Phase 0 adds the numerical contract fixtures; runtime and editor integration follow in phases 1–7. Code baseline: `VisualPinball.Engine` commit `2a830b504853e0d83b39795129ed884545d5cbff`. See section 14 for the review and disposition of its findings.

## 1. Behavior and first-release boundaries

A ball hits a toy and rotates it backward around a fixed pivot against a return spring, typically through 0–20 degrees. The ball and toy exchange momentum at contact. With suitable inertia and low surface elasticity, the ball continues forward while pushing the toy; the spring progressively resists the displacement. A weak shot produces a smaller deflection, while a strong shot can reach the mechanical stop. Contact can separate naturally. The toy returns under spring torque, gravity, and damping.

An optional energized magnet on the toy can capture the ball at the impact region. The captured ball remains a live ball with mass, gravity, spin, and collisions. Its load affects hinge acceleration and the oscillation period. Switching off the magnet releases the ball with its existing velocity. There is no animation-derived slowdown, no parenting/freezing of the ball, and no manual addition of its mass to the hinge.

Implement this as another specialized dynamic mechanism in VPE's existing physics loop, using the flipper's scheduling and reciprocal-contact architecture as the precedent. Do not introduce a second general contact solver, a new collider store, a global collider-reference migration, or Unity Rigidbody/HingeJoint simulation.

Version one supports:

- A fixed hinge base, an arbitrary fixed axis in playfield space, and one rotational degree of freedom.
- Arbitrary visual meshes moving with one authored closed box collision proxy. The proxy may be thin, offset, and rotated relative to the hinge. It represents the ball-contact region, not necessarily the complete visual silhouette.
- Several independent hinges on a table, at most one owned Spatial magnet per hinge, and at most one magnetically attached ball per owned magnet. Other balls can strike the toy or the held ball.
- Contact with the playfield and passive surfaces, magnetic capture/release, angular limits, threading, table packaging, and straightforward editor setup.
- Existing magnet coil, Hit, and Ball Held device semantics, with explicit ownership and lifecycle rules.

Deferred work: triangle-mesh or compound proxies, multiple held balls on one hinge, nested joints, moving hinge bases, hinge-to-hinge geometry contact, motors, flexible toys, and an isolated editor physics-preview world. Existing hit targets and unowned magnets retain their behavior. Holding a ball while it contacts an active legacy mechanism such as a flipper, plunger, kicker, bumper/slingshot, or turntable is outside the first-release qualification envelope; section 10 defines validation and runtime handling. Free balls still interact with existing table items normally.

These restrictions reduce the first implementation to the requested toy-and-magnet behavior. The inspector must show them explicitly; it must not offer unsupported mesh or arbitrary owned-magnet modes.

## 2. Current implementation and exact integration seams

All source paths in this document are relative to the `VisualPinball.Engine/` repository. Abbreviations: `R` = `VisualPinball.Unity/VisualPinball.Unity`, `E` = `VisualPinball.Unity/VisualPinball.Unity.Editor`, `T` = `VisualPinball.Unity/VisualPinball.Unity.Test`.

| Existing source | Observed behavior | Required work |
| --- | --- | --- |
| `R/VPT/HitTarget/TargetCollider.cs`, `HitTargetAnimation.cs` | Wall response precedes a fixed-angle animation. | Leave this behavior intact; the hinge is an independent mechanism. |
| `R/VPT/Flipper/FlipperCollider.cs`, `FlipperMovementState.cs`, `FlipperVelocityPhysics.cs` | Finite rotational response, relative contact velocities, reciprocal impact/contact impulses, and stop timing. | Reuse the scheduling and mechanical principles without importing flipper shape/solenoid/live-catch assumptions. |
| `R/Game/PhysicsUpdate.cs:118` | Once-per-tick velocity updates, followed by magnets and then `PhysicsCycle.Simulate`. | Add hinge velocity preparation and an owner-aware magnet pass on this same tick. |
| `R/Game/PhysicsCycle.cs:99`, `ApplyFlipperTime` | Angular displacement per accepted substep and step shortening at stops. | Add hinge displacement and hinge-stop time selection. |
| `R/Physics/Collision/ColliderType.cs`, `R/Physics/NativeColliders.cs`, `R/Physics/Collider/ColliderReference.cs` | Per-shape buffers, lookup switches, static/kinematic stores. | Add `SpringHinge` as a specialized collider type in the existing static store, including allocation/copy/disposal/debug dispatch. |
| `R/Game/PhysicsState.cs:391`, `R/Game/PhysicsStaticCollision.cs`, `R/Physics/Collision/ContactPhysics.cs:78` | Shape hit-test, collision, and contact dispatch. | Add dedicated hinge hit-test/impact/contact branches; never run its wall/target handler as well. |
| `R/Game/PhysicsEngine.cs:750`, `PhysicsEngineContext.cs:295`, `PhysicsState` constructor | Component registration and native state construction. | Register a hinge state map and owner lookup; add accessors and every `CreateState`/constructor/disposal argument. |
| `R/VPT/Magnet/MagnetPhysics.cs:322`, `MagnetState.cs` | Physical hold is a capped acceleration spring; it currently affects only the ball. Owner/local pose is absent. | Add owner-local state and a reciprocal finite-inertia hold; preserve the unowned paths. |
| `R/Game/PhysicsEngineThreading.cs` constructor, `SnapshotAnimations`, `ApplyMovementsFromSnapshot`; `R/Game/PhysicsMovements.cs` | Snapshot ID arrays, float animation channels, direct snapshot application, synchronous movement path. | Add hinge IDs/counts/output/emitters to both paths. Current rendering does not interpolate snapshots. |
| `R/Simulation/SimulationState.cs` | Fixed snapshot capacities and native arrays. | Include hinges in source-count/overflow checks and guarantee matching ball/hinge publication time. |
| `R/VPT/Magnet/MagnetPackable.cs`, `R/Packaging/RuntimePackageReader.cs` | Magnet version 3; component values and hierarchy restore before reference resolution. | Version new owned settings; add hinge/proxy packaging and hierarchy-based owner resolution after unpack. |
| `T/Physics/MagnetPhysicsTests.cs`, `PhysicsRegressionTests.cs` | Existing magnet, contact, transform, and state regression coverage. | Add independent numerical fixtures and targeted regressions. |

Line numbers are orientation aids for this baseline, not permanent API contracts. All new state is unmanaged and Burst-compatible. Keep runtime, editor, and test responsibilities in their existing layers.

## 3. Physical and ownership invariants

1. The simulation owns hinge angle, angular velocity, proxy pose, and magnet target. Render transforms are outputs, never the source of inferred hinge velocity.
2. The toy's authored mass/inertia includes its physical magnet hardware but excludes any captured balls. Captured balls remain in `BallState`; do not add their inertia or gravity to the toy a second time.
3. Impact/contact forces and magnetic attraction/hold reactions act on both ball and toy. Reaction torque is projected onto the allowed axis; the fixed bearing carries the constrained force/torque components.
4. Contact normal response is unilateral. The magnet is the only source of tensile attachment force. Surface elasticity and return-spring torque are separate properties.
5. Every force is integrated once per outer tick. Every collision is resolved at its accepted hit time. Per-substep contact support integrates only over the accepted substep. There is no trial-state replay and no warm-start impulse applied once per solver iteration.
6. An attachment's force magnitude is bounded by current-dependent holding capacity. Damping is relative to owner motion and must have a reciprocal reaction. No ball-only spin multiplication in the owned hold path.
7. Capture/release preserves momentum through impulses and existing velocities. Release clears ownership, not velocity; removal of a ball does not instantly speed up the toy.
8. Spring stiffness and physical damping stay unchanged on capture. A UI damping ratio is converted using the unloaded toy once, not recalculated from loaded inertia.
9. Distinguish mechanical owner IDs, collider IDs, and gameplay item IDs. A child magnet keeps its own coil/switch identity while transmitting load to its parent hinge.
10. Numerical iterations and collision feature changes cannot fire repeated hit/capture/release events. Reset, disable, and destruction clear actual runtime IDs and cached state.

## 4. Authoring contract

Add `SpringHingeComponent` and `SpringHingeColliderComponent`, with `SpringHingeApi`, state structs, and versioned packables. The setup action **Add Spring Hinge** creates a dedicated pivot root when needed, preserves selected objects' world poses with Undo, and moves only the chosen visual parts under it. Fixed brackets remain outside. The collider component authors the single physical box; selected visuals do not keep overlapping independent static colliders.

| Property | Authoring and storage policy |
| --- | --- |
| Pivot / axis | Position and axis scene handles independent of imported mesh origin. Store a local reference frame; runtime axis is normalized. |
| Rest / minimum / maximum angle | Degrees in the inspector, radians at runtime. Bash preset starts at 0 degrees with a 20-degree upper stop. |
| Toy mass | Multiples of VPE's standard ball mass, which defaults to 1. Label it as ball-relative mass, not kilograms. |
| Centre of mass / inertia | Editable centre marker and an inertia estimate from an independent box mass proxy. Include the parallel-axis term about the actual hinge. Manual inertia override for hollow/unusual toys. |
| Spring stiffness | Torsional stiffness with a clear unit label and presets; linear spring first. |
| Damping | Physical damping coefficient; optional ratio authoring converted from unloaded inertia. |
| Preload | Optional spring equilibrium beyond the lower stop, represented by one canonical equilibrium angle. Do not expose two independently additive preload settings. |
| Stops | Zero stop restitution in version one. Bound angle and outward speed; returning motion is allowed immediately. |
| Collision box | Local centre, orientation, and half-extents. Scene handles show current and full-travel poses. The collision box and mass proxy may differ. |
| Material | Existing elasticity/falloff/friction settings. Low elasticity in the bash preset. |
| Switch | Optional angle switch with different close/open thresholds; optional impact pulse. |

Bake scale into proxy dimensions and centre-of-mass coordinates before simulation; use a rigid hinge frame afterward. The initial release rejects sheared/invalid transforms, runtime base motion, nonpositive inertia, inverted limits, duplicate drivers, nested hinges, and simultaneous hit-target animation. Editing mass, pivot, scale, or mass proxy recomputes estimated inertia. A uniform cube with known dimensions is the calibration fixture; arbitrary render-mesh volume integration is not required.

Add the existing magnet as a child of the hinge and select Spatial physical behavior. Enable a new explicit **Couple to parent hinge** setting; the setup action sets it automatically. Resolve the nearest ancestor `SpringHingeComponent`, display the owner, and reject ambiguity. Store a local pole point and a distinct **held ball centre** offset. Place the latter just outside the intended collision face for the standard ball radius; radius-dependent adjustment must be included if a different ball is used. A target inside the box is invalid. Spatial attraction remains a radial point-field approximation, not a new magnetic surface-field model.

The coupled magnet exposes current-dependent holding capacity, hold stiffness/compliance, and relative damping separately from the influence radius. Derive sensible defaults from existing magnet strength, but changing influence range must not silently change attachment rigidity. Keep coil mapping, rise/fall time, capture region, and Ball Held switch familiar. Version one rejects owned Playfield/Cylindrical modes; the current cylindrical field is upright and does not become an arbitrarily rotating surface by parenting it.

Initial testing uses a dedicated Play Mode fixture/demo scene with shot markers, weak/medium/strong launch controls, magnet off/on, timed release, reset, and diagnostic traces. It runs through the real Player/PhysicsEngine and keeps hardware output disabled in the fixture. An isolated editor preview context does not exist today and is deferred; scene gizmos and the test scene provide the first authoring workflow.

## 5. State and collider integration

| New/extended data | Contents |
| --- | --- |
| `SpringHingeStaticState` | Owner ID; fixed pivot and orthonormal reference frame; unloaded mass/centre of mass/inertia; spring equilibrium/stiffness/damping; limits; proxy parameters; full-travel bound and maximum point radius. |
| `SpringHingeMovementState` | Angle, angular velocity, tick-start angular velocity/angle error/gravity torque, committed hold reaction for the tick, continuous acceleration, current stop/blocked-torque state, and switch/hit event state. Angular velocity is canonical and angular momentum is derived as `I*omega`. |
| `SpringHingeCollider` | Collider header, hinge owner ID, immutable box-in-hinge frame and dimensions, full-travel bounds, distance/hit-test helpers. |
| Extended `MagnetState` | Optional runtime hinge owner ID, local pole/hold frame, explicit owned mode, hold stiffness/damping/force capacity, attached ball ID/generation, and capture/release hysteresis. |
| Hinge owner registry | Stable component/parent resolution built during initialization, separate from main-thread kinematic transform tracking. |

Place `ColliderType.SpringHinge` in the existing static collider store. Here static storage means the broadphase bound is fixed, not that the response has infinite inertia, exactly as for the flipper. The specialized collider gets its current angle from the hinge state during hit testing. A dedicated generated collider header routes directly to hinge physics; it must not retain `ItemType.HitTarget` and invoke `TargetCollider` first. Assign event identity to the hinge API; the child magnet retains its own identity.

Extend all type-specific storage paths in `NativeColliders` and `ColliderReference`, including buffer allocation/copy/disposal, lookup construction, header/bounds access, transformation/debug enumeration, and count reporting. Add the new branches in `PhysicsState.HitTest`, `PhysicsStaticCollision`, `ContactPhysics.Update`, and generic collider-bound access. Keep `CollisionEventData.ColliderId` and `IsKinematic` unchanged. Bounds remain valid for the full allowed rotation.

Initialization is two-pass: discover/register hinge and magnet components, then resolve owners and bake frames/proxies. Resolve from the same restored hierarchy in authoring and packaged player tables; component `Awake` order must not be assumed. Use owner state accessors rather than finding transforms from a Burst hot path. Add native maps and any scratch space to `PhysicsEngineContext.CreateState`, the `PhysicsState` constructor, and disposal. Preallocate for the validated number of hinges; do not allocate during ticks.

## 6. Units, inertia, and gravity

The actual constants are in `VisualPinball.Engine/Common/Constants.cs`: `PhysicsStepTime = 1000` microseconds, `DefaultStepTime = 10000` microseconds, `DefaultStepTimeS = 0.01`, and `PhysFactor = 0.1`. One outer tick is 1 ms, represented as `h = 0.1` normalized time units. Runtime linear velocity is VPU per normalized time unit; angular velocity is radians per normalized time unit.

Use ball-relative mass throughout. Runtime inertia has units `ball-mass * VPU²`, stiffness has `ball-mass * VPU² / normalized-time² / rad`, and damping has `ball-mass * VPU² / normalized-time / rad`. If stiffness and damping are authored per second with positions already in VPU, multiply stiffness by `T²` and damping by `T`, where `T = 0.01 s`. Convert degrees once to radians. Use `Physics.WorldToVpx`/`VpxToWorld` for scene geometry. `PhysicsConstants.MToVpu` and the matrix's rounded scale differ slightly; use the existing geometry conversion for geometry and the existing `Ms2ToVpuVpt2` for cabinet acceleration, with tests rather than another invented conversion constant.

Let `a` be the unit axis, `p` the fixed pivot, `x_cm` the toy centre of mass in its current pose, `theta` the angle, and `omega` the angular speed:

```text
surfaceVelocity(x) = omega * cross(a, x - p)
gravityTorque = dot(a, cross(x_cm - p, toyMass * effectiveGravity))
springTorque = -k * (theta - equilibriumAngle) - c * omega
I * angularAcceleration = gravityTorque + springTorque + contactTorque + magneticTorque
```

`effectiveGravity` is table gravity plus the same cabinet inertial acceleration/sign used by `BallVelocityPhysics`. Apply toy gravity once and ball gravity once. A captured ball's weight reaches the hinge through its actual hold/contact reaction; a ball also resting on the playfield does not transfer its full weight to the hinge.

A tightly held point-mass ball at perpendicular distance `r` gives approximately `I_loaded = I_toy + m_ball * r²`. This is an analytic check on the coupled behavior, not a value written into hinge state. Ball spin is free; locking orientation would be a different model. With weak damping and negligible gravity torque, the period is approximately `2*pi*sqrt(I_loaded/k)`. Gravity can change the equilibrium and restoring stiffness, so added weight does not universally make every pendulum slower.

## 7. Fixed scheduler contract

Retain VPE's existing kick-then-drift collision timeline. All continuous velocity preparation happens once per outer tick; all pose displacement and impact/contact response happens inside the existing accepted-hit-time loop. There is no per-ball migration between timelines and no replay of full-tick forces on shorter collision substeps.

The outer-tick order is:

1. Apply queued commands, cabinet input, and existing prescribed kinematics. Reset ball external acceleration and run the existing ball velocity preparation exactly once.
2. Compute and save each hinge's tick-start `omegaOld`, angle error, gravity/cabinet torque, and `h = physicsDiffTime`. A hinge without an owned magnet commits its free implicit step here: `omegaFree = (I*omegaOld + h*tauGravity - h*k*(theta-equilibriumAngle)) / (I + h*c + h²*k)`. A hinge with an owned magnet does not commit a free step yet, including when it has no attached ball at tick start.
3. Advance every magnet's coil current once. Preserve the unowned magnet update behavior and its existing ownership first; then determine owned-magnet capture/release eligibility. Evaluate owned free-field contributions from tick-start poses into preallocated ball-impulse and owner-angular-impulse buffers before committing owned hinge velocities. The magnet that captures a ball applies no separate attraction to that ball on this tick: its hold block replaces that contribution. Commit each owned hinge exactly once: the free implicit step including other field reactions if no ball is attached after eligibility, or the local hold block if a ball is attached. Other magnets' forces on that ball are included in `vPre`; reactions from attracting other balls are included in `QOther` in section 9. This also handles a newly captured or newly released ball without replaying spring/gravity integration.
4. Run the existing `PhysicsCycle.Simulate` for the tick. Add hinge stop times to the candidate hit-time minimum, add hinge hit tests, and displace hinge angle alongside balls/flippers before resolving the accepted collision. After an impulse changes motion, recompute continuous spring/damping/gravity torque at the current pose/speed without integrating it again; keep the committed magnetic reaction torque fixed for the remainder of the tick. Never reinterpret the impact's instantaneous velocity jump as a continuous `deltaOmega/h` torque.
5. Process sustained contacts through the existing contact pass, with a specialized hinge branch. Continue for remaining substep time. Fire committed events once and publish matching ball/hinge snapshots.

Within a collision substep, trajectories are the velocities already prepared at the tick boundary plus preceding collision/contact impulses. Continuous TOI prediction uses those piecewise-constant velocities and `theta(t) = theta0 + omega*t`, bounded by stop arrival. Do not include another force-driven acceleration trajectory in narrowphase that actual displacement does not follow. Spring/hold forces are recalculated next outer tick. This is an intentional first-order splitting approximation matching the existing flipper contract. Ordinary magnetic capture/release decisions occur at tick boundaries; mid-tick collisions affect the next hold solve up to 1 ms later. A future stop truncates motion predicted by the free implicit spring step without replaying that step. Lifecycle and unsupported-interaction releases remain immediate exceptions. Phase-0 fixtures qualify `holdFrequency*h <= 0.2`; combined with the initial ten-to-one hold/hinge frequency ratio this requires a loaded hinge period of at least 0.314 s at the 1 ms tick. Implicit Euler contributes an approximate numerical damping ratio `h*hingeFrequency/2` (about 0.01 in the reference oscillator). The reference support-lag fixture bounds residual hold acceleration below 0.75 VPU per normalized-time squared. The single-projection saturated hold is qualified only while its constitutive residual is below ten times the force-cap impulse; tables relying on sustained saturation require tighter validation.

Clamp the result of `ApplyStaticTime` to the previously selected earliest positive mechanism-stop time and confirmed hinge TOI, after all candidate processing. The current rule can raise the interval after a stop was selected. Apply the stop-time correction to flippers as well as hinges and run flipper regressions. Use dedicated just-before-stop and repeated-zero-time fixtures; distinguish a zero-time contact progress rule from permission to overrun a positive stop time. At a stop, zero outward speed, retain the blocked-direction sign, and allow any impulse away from the stop. Do not inject energy with a rebound coefficient in version one.

## 8. Analytic box collision and reciprocal contact

### Geometry and continuous detection

Use one oriented box fixed in the hinge-local frame, with a fixed full-travel broadphase AABB. A conservative sphere about the pivot with radius equal to the farthest box corner is sufficient for the initial bound; optionally tighten along the hinge axis. The bound includes intermediate arc positions and remains valid if an impact reverses rotation. Keep the render mesh separate from this proxy.

For time-of-impact, transform the sphere centre at time `t` into the predicted box frame and compute its closest point by clamping to the box half-extents. Distance from the sphere centre to that point minus ball radius gives the outside separation. Handle centres inside the box with the nearest outward face and signed depth. Return a world-space witness point and normal; this full 3-D calculation covers side faces, edges, and corners. Merely reusing a two-dimensional rotating line with a height-band test would miss end-face and corner cases for an arbitrary hinge orientation.

Find the first zero of separation with conservative advancement over the interval before the next stop. A safe global distance-rate bound for the piecewise-constant motion is `length(ballVelocity) + abs(omega)*maxProxyRadius`. Use that to advance by separation/rate with a conservative factor, then refine near contact. Endpoint sign tests alone are insufficient: the toy can pass through and leave again. Bound iterations, and on failure subdivide time with a conservative speculative-contact fallback; never silently accept a missed crossing. Validate numerical slop against ball radius. No general sphere-to-triangle mesh distance subsystem is needed.

Inside `HitTest`, distinguish an approaching impact from sustained contact using relative normal velocity, separation, and existing contact tolerances. Produce contact data in one documented frame. Rotating normals, hit distance, original relative velocity, and force vectors must be transformed consistently. All later response uses the same current witness and hinge state.

### Impact and sustained contact

For normal `n` from toy to ball, `r = witness-pivot`, `s = dot(axis, cross(r,n))`, and pre-impact relative normal velocity `vn`:

```text
inverseEffectiveMass = ball.InvMass + s*s / hinge.Inertia
J = -(1 + restitution) * vn / inverseEffectiveMass
ball.Velocity += J * n * ball.InvMass
hinge.AngularVelocity -= J * s / hinge.Inertia
```

Apply impact only when approaching. At an active stop, suppress the hinge response only if the proposed impulse pushes farther into that stop; an impulse away from the stop must still recoil the toy. Retest the stop after response. Use existing material elasticity/falloff/LUT semantics but no flipper solenoid, live-catch, scatter-driven speed, or special recoil heuristics. The normal impulse through a sphere centre leaves ball spin unchanged; friction uses the real surface lever arms and applies reciprocal angular impulses.

Add `SpringHingeCollider.Contact` to `ContactPhysics.Update`, following `FlipperCollider.Contact`: compute ball and hinge surface velocities and accelerations, including rotating-normal/centripetal terms; solve the nonnegative force required to prevent approaching normal acceleration; apply the resulting impulse over the accepted substep; then bound tangential friction by the actual support load. Use the ball's `ExternalAcceleration` exactly once and the hinge's tick acceleration/stop state, rather than assuming an infinitely heavy moving wall. Derive arbitrary-axis cross products instead of copying z-only helper functions.

The toy/playfield/ball-ball interactions remain sequential under VPE's current solver. They must pass the multiball and squeezed-contact fixtures; they are not exact simultaneous constraints. If qualification fails, first improve bounded local hinge contact iteration within this contract. A general island solver is a separate architecture decision and must not be introduced silently during implementation.

### Ball-ball broadphase after impulses

Static broadphase re-queries current ball bounds each substep, but the ball-ball octree is built once per cycle. `BallState.Aabb` is generously inflated by the full speed even though a tick travels about one tenth of that distance. This is useful margin, not a proof for a stationary ball suddenly accelerated by a fast returning toy or a nearby magnet.

Track the ball AABBs inserted into the dynamic octree. After any new hinge/contact/hold response, test whether the ball's conservative remaining-tick envelope fits its inserted bound. Rebuild/refit the existing ball octree only if containment fails, before the next ball-ball query. This is a conditional correction, not an unconditional new broadphase every substep. Include the just-stationary-ball counterexample in tests. Do not constrain user physics with a guessed speed-multiplier limit to preserve stale bounds.

## 9. Reciprocal owned magnet and attachment

### Frames, force evaluation, and capture

Add a hinge-local pole position and held-centre target. Compute their current world/playfield pose from hinge state each tick; their velocity is `omega * cross(axis, point-pivot)`. The new path never derives velocity from rendered transforms. `OwnerId` is runtime-only. Preserve the child magnet's coil and switch IDs.

Refactor owned force evaluation to return acceleration/force contributions before committing them. Existing functions often call an acceleration variable `force`; convert with the actual ball mass before calculating reaction impulse. Attraction is a central ball-to-pole force with its reaction at the pole, giving reciprocal hinge torque. If a damping model applies a noncentral force, its reaction couple must be defined; do not claim angular-momentum conservation just from opposite linear forces. Keep the old unowned damping/profile behavior unchanged.

An owned magnet can attract several free balls, but attachment ownership is exclusive: one attached ball per owned magnet, one owning magnet per ball. Choose a deterministic eligible ball using distance and then stable ball ID. Other magnets may continue applying attraction, but must not also execute their old grab/hold path on an already attached ball. Add a central ownership check for legacy grabs and kicker captures. Preserve the existing 64-ball bookkeeping limit and report capacity overflow rather than alias bits.

Capture eligibility requires an energized field, a near-surface gap, a nonpenetrating target, and relative motion that available magnetic work can arrest. Let `u = cross(axis, ballPosition-pivot)`, `vRel = ballVelocity-u*omega`, and `K = identity/m + outer(u,u)/I`. Required relative kinetic energy is `0.5*dot(vRel, inverse(K)*vRel)`; in one direction this reduces to `vRel²/(2*Kdirection)`. At an outward-blocking stop use zero inverse hinge inertia in that direction. The current ball-only stopping-distance inequality assumes an immovable owner and is not copied unchanged.

Define version-one capture-work eligibility as the explicit heuristic `Wcapture = max(0, min(fieldForceNow, maxHoldForce(current))) * max(0, GrabRadius-distanceToHoldTarget)`, with force magnitudes in engine force units, and require relative energy no greater than this budget. It generalizes the existing stopping-distance check; it is not an exact integral of magnetic potential. Qualify weakening-field, boundary, and zero-relative-speed cases. Existing attachment ownership wins until released; new owned capture excludes balls already held by a legacy magnet or frozen by a kicker. New owned candidates are arbitrated before field/hold application, and the committed capped hold determines whether capture persists.

### Once-per-tick implicit hold

Keep the ball orientation free and constrain its centre near the local target with a compliant translational hold. Avoid a ball-only critically damped spring plus an after-the-fact reaction: a light hinge changes the effective mass and therefore damping/stability. This is a small local hinge/held-ball velocity solve in the magnet pass, not a second contact/island solver.

For one held ball, solve hinge scalar speed and ball's three linear velocity components together once per outer tick. Use the virtual hinge material point currently coincident with the ball centre for the reaction Jacobian: `u = cross(axis, ballPosition-pivot)`. The target supplies only the displacement error `C = ballPosition-target`; interpret its update in the locally co-rotating frame. Let `vPre` include the ball's ordinary external kick and other magnets' contributions, `omegaOld` be the saved tick-start hinge speed, and `QOther` the accumulated angular impulse from attracting other balls. A linearized implicit model is:

```text
m * (vNew - vPre) = P
I * (omegaNew - omegaOld) = h*tauGravity + QOther - h*k*(angleError + h*omegaNew) - h*c*omegaNew - dot(u,P)
vRelativeNew = vNew - u*omegaNew
P = -h*holdStiffness*(C + h*vRelativeNew) - h*holdDamping*vRelativeNew
length(P) <= maxHoldForce(current) * h
```

Solve the unconstrained small block system first. If `length(P)` exceeds `Pmax = maxHoldForce(current)*h`, version one projects `P` once onto that sphere along its unconstrained direction. Then set `vNew = vPre+P/m` and recompute `omegaNew = (I*omegaOld+h*tauGravity+QOther-h*k*angleError-dot(u,P))/(I+h*c+h²*k)`. The saturated solution preserves the committed momentum balance but is an approximation to the exact nonlinear capped damper; measure its constitutive residual and acceptable saturation envelope in phase 0. Do not label that residual a converged exact solution. Independent axis clamping is prohibited because it exceeds the vector force cap.

If the hinge is already at a stop and the candidate speed is outward, resolve the hold with fixed `omegaNew = 0`, project its impulse if needed, and compute the resulting bearing reaction. Check that this reaction has the permitted unilateral sign; otherwise use the free candidate because the hinge can leave the stop. An approaching future stop still belongs to substep stop-time handling. Include the unconstrained block reference, already-stopped hold, release-from-stop, zero-cap, and cap-saturation fixtures. Handle nonpositive parameters and singular cases explicitly. No branch commits both a free hinge step and the coupled block.

Record exactly `ball.ExternalAcceleration += P/(ball.Mass*h)` from the full committed capped impulse, including damping. The old unowned magnet path records only its spring acceleration; that is not the contract for this new path. The hold is applied once; `ContactPhysics` balances the recorded load against surfaces over its substeps and does not apply the hold again. Save the owner's committed magnetic torque as `(QOther-dot(u,P))/h` and hold it fixed for the remaining tick. After collisions, re-evaluate only spring/damping/gravity and stop reactions for continuous contact acceleration. Audit `BallSpinHackPhysics` and other post-collision corrections; disable an incompatible spin hack for attached balls if it removes momentum without a physical reaction.

Using the actual ball-centre point for both the velocity Jacobian and the reaction arm makes the ball impulse and hinge reaction act at the same point: projected angular momentum exchange is exact for that impulse even with finite displacement error. The mount transmits the equivalent wrench to the toy; the ball orientation remains free. Using co-rotating `C+h*vRelativeNew` is a local first-order integration approximation, whose truncation error must converge under step refinement. It must not be described as the exact inertial-frame displacement derivative at the separate target. Surface friction/spin torque still uses the actual contact witness.

Separate attachment stiffness from magnetic holding capacity. As an initial qualification setting, require the small-signal attachment frequency to be at least ten times the loaded hinge frequency, while keeping the tick resolution adequate for measured convergence. The ratio is a starting test condition, not a universal proof; verify period error under tighter holds and halved ticks. If a force cap saturates under ordinary gravity, the rigidly held inertia identity is not an appropriate expected result. Show an authoring diagnostic for a weak/compliant hold instead of promising the same added-mass response at every setting.

### Release and lifecycle

Advance coil rise/fall once per tick using existing behavior. On switch-off, reduce hold capacity with decaying current; release when current/gap/work criteria or persistent cap saturation under separating load require it. A hard second-ball hit can break attachment on the next eligibility check. Distinguish temporary saturation during capture from sustained breakaway; use bounded gap and separating-velocity hysteresis.

Release clears attachment IDs/state and emits Ball Released once, preserving current ball velocity/spin and hinge angular velocity. Never transfer the detached ball's angular momentum back into the toy. Keep it collidable at its current position. Release also precedes ball destruction/reuse, manual control, kicker freeze, magnet/hinge disable, and table reset. Avoid removing owner state before cleaning its attachment; include generation/stale-ID checks.

## 10. Legacy interaction and first-release validation

No general legacy-mechanism adapters are introduced. The existing ball-ball, passive surface, target, trigger, and impact dispatch remain in their current order. Add hinge handling through its own type and keep each hit event/response single-owned.

For a held ball, qualify passive surface support and ball-ball impacts first. Before enabling the feature on a table, editor validation expands the whole held-ball centre sweep by its radius and checks it against the swept bounds of flippers/plungers and the capture/force regions of kickers, bumpers/slingshots, and turntables. A coarse overlap is a warning requiring visual inspection or a tighter query, not proof of an actual collision. Unsupported actual overlaps are reported as placement errors for version one. The diagnostic includes the two named items and their swept volumes.

Runtime must still behave safely if table scripts bypass authoring validation. If an attached ball reaches an unsupported active-mechanism interaction, release its attachment before the legacy handler, retain its current velocities, emit the release event once, and issue a rate-limited diagnostic. This is an explicit unsupported-placement fallback, not claimed physical magnet breakaway. Kicker capture always follows the same release-before-freeze rule. A free ball may contact these mechanisms normally.

If the actual toy cannot be placed within this envelope, extending that specific interaction becomes required follow-up work before claiming the user's table is supported. Do not equate passing the isolated demo with universal table compatibility.

## 11. Threading, events, and package reconstruction

Add hinge state to `PhysicsEngineContext` and the `PhysicsState` constructor/accessors; register it in `PhysicsEngine.Register`, include it in `CreateState`, and dispose all native buffers. The simulation thread is the only mutable owner. Inspector/API updates use the existing mutation/command boundary. Geometry/base edits require a safe reset/rebuild; ordinary coil changes preserve current motion.

Add a hinge snapshot ID array in the `PhysicsEngineThreading` constructor, include its source count in `MaxFloatAnimations` checks, write hinge angle in `SnapshotAnimations`, and add the appropriate float emitter. Add `PhysicsMovements.ApplySpringHingeMovement` to the synchronous movement path. Register emitters only after IDs are resolved. Snapshot overflow must not render the ball from a newer state while leaving its owner stale; validate capacity on load and report/fail unsupported capacity rather than silently desynchronize the pair.

`ApplyMovementsFromSnapshot` currently applies one snapshot directly. Version one publishes and renders hinge and balls from the same snapshot and timestamp. It does not add hinge-only interpolation or assume ball interpolation exists. A later smooth-rendering change would retain previous/current samples for both ball and hinge and use one interpolation time; that is outside this feature's first pass. Exclude the hinge and physics-owned magnet descendants from prescribed kinematic-transform detection to avoid render-to-physics feedback.

Send impact pulses, angle-switch transitions, capture and release events only after committed physics transitions. Preserve source device IDs. A normal feature change, repeated contact substep, or numerical hold iteration cannot generate another scoring hit. Angle switches use close/open hysteresis. Existing coil current handling and Ball Held switch names are preserved.

Package `SpringHingeComponent`/collider values, visual hierarchy, local box/mass frames, material references, and optional angle-switch settings via the existing `IPackable` and reference facilities. For magnet version 4, add an explicit owned-mode flag, local hold offset, and independent hold parameters; old versions default to unowned behavior even when parented beneath a new hinge. Resolve runtime `OwnerId` from the restored parent hierarchy after components exist, not from an opaque serialized instance ID. An explicit mode flag prevents a future reparent from silently changing old magnet behavior. Preserve the hierarchy even if package optimization would otherwise strip an empty pivot node.

The table asset must not persist captured-ball IDs, warm solver state, or runtime angle. Runtime save states are a separate feature. Test package round-trip in the HDRP authoring project and reconstruct the same component hierarchy and collider/device IDs in `VisualPinball.Engine.Player` without relying on editor-only mesh data or `Awake` ordering.

## 12. Implementation phases and file-level deliverables

| Phase | Deliverables | Gate |
| --- | --- | --- |
| 0. Numerical fixtures | Add pure fixture builders in `T/Physics`; fix the tick contract from section 7; implement test references for oscillator, impact, hold block, and stop. | Unit conventions, unconstrained spring step, reciprocal impact, and capped local hold are independently verified before integration. |
| 1. Runtime skeleton | `R/VPT/SpringHinge/{SpringHingeComponent,SpringHingeColliderComponent,SpringHingeApi,SpringHingeState,SpringHingeVelocityPhysics,SpringHingeDisplacementPhysics}.cs`; context/register/state/accessor/disposal seams. | A hinge evolves in the existing tick/substep loop and reaches/releases stops correctly. |
| 2. Specialized collider | `SpringHingeCollider.cs`, collider generator, `ColliderType`, `NativeColliders`, `ColliderReference`, bounds and hit-test/collision/contact switches. | Nonmagnetic ball pushes the box, rebounds/returns correctly, and cannot tunnel in the tested envelope. |
| 3. Owned magnet | Extended magnet state/component/update; local implicit hold helper; owner registry and capture/release lifecycle. | Loaded period, gravity transfer, momentum, release and second-ball breakaway pass. |
| 4. Integration qualification | Conditional dynamic-octree containment/refit, event deduplication, spin-hack audit, supported/unsupported legacy interaction handling. | Passive support and multiball work; unsupported active contact diagnoses/releases predictably; no duplicate impulses/events. |
| 5. Render and packages | Threaded/synchronous hinge output, source-count checks, hinge packables, magnet version 4, hierarchy reconstruction. | Authoring and player load the same mechanism; no render feedback, stale IDs, or native leaks. |
| 6. Authoring | `E/VPT/SpringHinge` inspectors/setup/handles, box mass-property helper, magnet inspector owner controls, validation, bash preset. | An author can configure the visual toy, proxy, pivot, spring, and magnet without a behavior script. |
| 7. Demonstration and docs | Dedicated shot-control Play Mode fixture/demo package, creator guide, changelog, regression/performance results. | The acceptance matrix passes and first-release limits are documented. |

The phase-0 spike validates a chosen architecture; it does not leave the main scheduler undecided. If the existing sequential contact architecture fails the user's required passive-support or multiball cases, stop claiming readiness, record the specific failed fixture, and propose the smallest justified solver extension. Do not solve the problem by disabling collisions, parenting the ball, increasing mass twice, or silently removing the requirement.

Phases 0–3 are implemented by the numerical fixtures, runtime spring-hinge skeleton, specialized analytic collider, and reciprocal owned-magnet coupling alongside this plan. Phases 4–7 remain gated by their tests and pre-commit reviews.

## 13. Acceptance and regression matrix

Thresholds are initial qualification targets to establish with the numerical fixtures. Record step size, solver settings, mass/scale, proxy dimensions, and hold-frequency ratio with results. Test against independent equations or a finer-step reference, not duplicate implementation arithmetic.

| Fixture | Required result |
| --- | --- |
| Unit conversion | Numeric checks for degrees/radians, normalized time, ball-relative inertia, geometry scale, and cabinet acceleration sign/scale. |
| Free torsional oscillator | Lightly damped, gravity-free small-angle period within 1% of `2*pi*sqrt(I/k)`; error decreases when halving the tick. |
| Gravity and preload | Correct torque sign about arbitrary axes, zero torque for a vertical axis, equilibrium/preload against either stop, and correct cabinet inertial torque. |
| Isolated impact | Angular momentum about the hinge axis within 0.1% for a frictionless short impact away from stops with negligible external torque; velocities/restitution match an independent solution. |
| Lever arm | Near-axis and far-axis impacts give the expected difference; arbitrary frame rotations preserve the physical result. |
| Sustained push | Ball continues with an appropriate low-elasticity toy, spring resistance grows, and no repeated static wall bounce occurs. |
| Stops | Exact stop arrival ordering, blocked-direction finite response, no outward resting velocity, immediate ability to move away, no zero-time infinite loop. |
| Passive energy | No secular energy growth over 10 seconds without actuation/external work; quantify numerical damping rather than claiming exact energy conservation. |
| Local hold block | Closed-form free/coupled cases, reciprocal momentum with spring/gravity off, vector force cap, cap-active residual, very light/heavy toy, and damping stability. |
| Loaded inertia | Tight gravity-free hold converges to point-mass loaded period within 2%; verify at capture distances r and 2r. Document hold/hinge frequency ratio and force-cap headroom. |
| Weight and playfield support | Expected equilibrium without playfield support; for a held-centre target at resting ball height, near-zero vertical hold load as the playfield supports gravity. The hinge receives the full actual magnetic reaction, not a manually reduced weight. Measure the residual one-tick support lag against hold damping. |
| Moving capture | Capture with owner swinging toward/away from the ball transfers incoming momentum rather than discarding it. |
| Release | Coil decay honored; ball position/velocity/spin and hinge speed continuous at release in either direction; no instantaneous speed gain when mass detaches. |
| Breakaway and ownership | Another ball can knock the held ball free; weak magnet fails capture; competing magnets cannot create two holds; deterministic arbitration and one release event. |
| 3-D box CCD | Thin face, edge, corner, box end-face, off-axis proxy, long lever arm, fast shot, return into a stationary ball, and interior recovery. |
| Contact error | Target maximum penetration below 0.5% of ball radius in the documented speed/stiffness envelope; bounded fallback diagnostics on exhausted TOI budget. |
| Dynamic bounds | A stationary ball accelerated beyond its inserted ball-octree margin still collides with a second ball in the remaining tick; no unconditional refit cost. |
| Multiball | Two balls on one toy, strike on a held ball, and held-ball/playfield support are stable; reverse registration order and quantify sequential-solver error. |
| Existing items | Free balls preserve target/trigger/bumper/flipper behavior; unsupported attached-ball active interactions release before legacy effects with one diagnostic. |
| Lifecycle | Disable/delete/reset/manual control/kicker freeze/ball-ID reuse and repeated Play Mode do not retain ownership or leak native allocations. |
| Rendering | Threaded and synchronous paths at 30/60/144 Hz apply matching ball/hinge simulation times; render hitches do not drive the hinge. |
| Packaging | Values, pivot node, material/device bindings, explicit owned flag and parent resolution round-trip; version-3 magnets remain unowned and unchanged. |

Run the existing magnet, contact/transform/kinematic, flipper, target, turntable, and cabinet suites when their shared paths change. The checked-in test project targets `netcoreapp3.1` and references Unity assemblies: distinguish managed numeric tests from cases requiring the Unity Editor/native runtime. Validate Burst compilation and EditMode/PlayMode behavior in the configured Unity project. A managed build alone is not physics validation.

Benchmark no hinges, one moving hinge, one held ball, and several hinges with multiball. Record physics time, TOI iterations/fallbacks, active contacts, refit counts, and allocations. Require zero per-tick managed allocations; target under 1% added physics cost for tables without this feature on the same hardware. Establish and publish the supported speed, stiffness, proxy size, and hold-compliance envelope from measured convergence. Automatic substep growth must be bounded and reported; no claims about arbitrarily stiff springs at fixed cost.

## 14. Fable 5.1 review and disposition

The initial, broader draft was reviewed through Claude Code using `claude-fable-5-1` at high effort in persistent session `16755288-b5d6-4dff-b5cb-8d9ad1be422b`. The first review completed successfully and read the plan and relevant repository code. Its verdict was that the initial plan was not implementable as written because the scheduler was undecided and the generic solver/collider migration was too broad. This revision addresses that verdict with a fixed implementation contract. Review is design feedback, not evidence of implemented or tested behavior.

| Review finding | Disposition in this revision |
| --- | --- |
| 1. Unresolved scheduler | Accepted: preserve once-per-tick velocity preparation and per-substep displacement/contact. Specify exact order and stop/minimum-step interaction. |
| 2. Generic rotating-mesh CCD | Accepted scope reduction: one analytic 3-D box proxy. Retain full sphere-box distance/TOI rather than a 2-D line plus height band, which is insufficient for arbitrary-axis corners/end faces. |
| 3. Unnecessary collider-store migration | Accepted: specialized `ColliderType.SpringHinge` in the static store; no `ColliderSet` migration. |
| 4. Second island/contact solver | Accepted: existing sequential contact model with hinge response and explicit qualification limits. |
| 5. Reciprocal magnet | Accepted owner/reaction requirement; refine the suggested ball-only explicit spring into a local finite-inertia implicit velocity block with bounded force and defined torque accounting. |
| 6. Loaded-inertia test depends on hold stiffness | Accepted: separate stiffness from range, state the initial frequency-ratio/headroom conditions, and require convergence/stability tests. |
| 7. Ball-relative units | Accepted: use existing mass convention and exact normalized-time constants; no invented kilogram calibration. |
| 8. Ball bounds already have margin | Partially accepted: avoid unconditional refits. Retain a containment check and conditional refit because initial low speed does not bound a later impulse. |
| 9. Legacy placement restriction | Accepted with explicit supported passive contacts, editor sweep validation, and runtime release-before-unsupported-interaction behavior. |
| 10. Missing lifecycle/package/render seams | Accepted concrete constructor/register/snapshot/disposal seams and hierarchy resolution. Add an explicit versioned ownership flag to preserve old-package behavior. Defer an isolated preview context in favor of a real test scene. |

The same Fable 5.1 session completed a focused follow-up review successfully. Its verdict was: “the revised plan is implementable in architecture.” It requested four concrete corrections, all incorporated afterward: defer the owned hinge's velocity commit until capture eligibility is known; use the actual ball-centre material point for reciprocal hold momentum; report the full capped hold impulse as external acceleration and freeze its reaction for the tick; and define one projected force-cap solve with a stated approximation envelope. Additional edits specify capture-work eligibility, the StaticTime clamp, playfield-support expectations, and the one-tick splitting limits. These final textual corrections were checked locally; there was no third model pass and no implementation/test execution.

Full review transcripts were retained as local planning-task artifacts outside the repository. The first transcript evaluates the initial broader draft, and the second evaluates the narrowed revision before the final corrections above. They are not required to build, test, or understand this implementation.

## References

- [OpenStax: angular momentum and capture](https://openstax.org/books/university-physics-volume-1/pages/11-3-conservation-of-angular-momentum).
- [OpenStax: physical pendulums](https://openstax.org/books/university-physics-volume-1/pages/15-4-pendulums).
- [Box2D: revolute joint spring and limit concepts](https://box2d.org/documentation/group__revolute__joint.html).

These support the mechanics; implementation follows VPE's existing specialized-mechanism architecture and adds no external physics-engine dependency.
