# Static Rubber Rebound and Slingshot Implementation Plan

Status: implemented on `feat/rubber-deformable-spans` (rebound, slingshot runtime, visuals, docs), after four GPT-6 review rounds; supersedes the runtime part of `RUBBER_DEFORMABLE_SPAN_PLAN.md`. Open items are in section 8.

Scope: straight free spans of guided rubbers using `RubberColliderMode.Physical`; the physical slingshot runtime behind `RubberSlingshotComponent`

Non-goals: moving collision geometry; a second ball seeing a deflected cord; the arm as a physical body; curved (rounded) free spans, which keep today's response

## 1. Decision record

The deformable-span plan was implemented up to its numerical contract and stopped there: at the engine's substep the end-of-substep impulse formulation keeps 0.03 percent of a ball's energy against a cord whose modal mass is about 1/135 of a ball, and the formulation that survives the numbers needs ball and cord integrated together inside the substep, a further redesign with unresolved integration problems (that plan's section 14). The maintainer chose the static route on 2026-09-18.

This plan keeps the collision geometry static and fixes the two things that matter for play: the rebound direction and speed depend on where along the span the ball hits and how hard, and the slingshot fires causally from a switch to a coil with the kick applied to a ball still in reach of the arm. The visual rubber and arm animate from the same events. The redesign brief's section 11 forbade an approximate Physical implementation that hides the VPX velocity boost behind a new component; the maintainer's decision amends that. Kept from the brief: separate switch and coil endpoints, no coil energy without a coil command, switch zones, stable span binding, Legacy isolation, and the deformable path as a later upgrade.

Reused from the branch: the deformable plan's units and material model, the authoring commit's baked descriptors, and the numerical-contract commit's fixtures, which supply the reference solver for the rebound.

## 2. Massless-string rebound

### 2.1 Surrogate model

The cord's mass is negligible against the ball's, so the response is computed for a *virtual point mass*: the ball's mass placed at the apex of a massless string pinned at anchors `A` and `B`. Work in the span's plane with the origin at the foot of the contact on the chord, `x` along the chord toward `B`, `y` along the outward normal `n` (from cord to ball). With `P` the apex, the string force on the surrogate is

```text
F(P) = T(l) * ( (A - P) / |A - P| + (B - P) / |B - P| )
l(P) = |A - P| + |B - P|,   T(l) = sigma(l / L0) * A0,   T = 0 where l <= L
```

The unit vectors point from the apex toward the anchors; for an apex below the chord their sum points outward, so `F` is the outward push with no sign change. On the chord between the anchors the vectors are anti-parallel and the force is exactly zero; `n` is only the limiting direction as the apex leaves the chord.

Initial conditions: `P(0) = (0, 0)`, the foot of the contact; the surrogate's velocity is the ball's relative velocity (against the capsule's surface velocity) projected into the plane, chord and normal components, the normal component negative on approach; the out-of-plane component is carried through unchanged. Entry is the first negative `y`, immediate for an approaching ball.

Integration: `m P'' = F(P)` with fixed-step fourth-order Runge-Kutta at `h = 0.5` ms of simulation time (0.05 VPE time units) until the *return*, the first step at which `y` crosses from negative to non-negative after entry, refined by linear interpolation of `y` between the two steps; the exit velocity is the velocity at that time. On the qualification matrix of 6.1 (both worked materials, four positions, three speeds, normal and both 30 degree obliquities) the return takes 27 to 101 ms, at most 201 steps, with a loaded frequency near 47 rad/s so `omega h` is about 0.024 and the step is far inside RK4's accuracy. The step budget is 600 (300 ms).

**Domain.** The V model is a string pinned at both ends, so the surrogate is valid only while the apex stays between the anchors. The apex's span coordinate is `xi = (P.x - A.x) / L` measured from anchor `A` (the integrator's origin is the contact foot, so `xi = u + P.x / L`). The following are *integration failures*, each counted, each falling back to the rigid response of 2.2: exhausting the budget; a non-finite state; `l` beyond the table's maximum stretch; `xi` leaving `[-0.02, 1.02]` at any step; and a return energy residual outside `[-1 percent, +1 percent]` of the initial in-plane kinetic energy, which covers both float drift and a return past an anchor with energy still stored in the string (a 70 degree oblique center hit at 18 units per step returns beyond `B` with 20 percent stored; it is a failure, not a lossless exit). Two cases of the qualification matrix are *expected* fallbacks under this rule and are asserted as such: `u = 0.05`, 30 units per step, minus 30 degrees, for both worked materials, where `xi` reaches about `-0.0205` within the first 9 ms. No interior or invalid state is ever presented as an exit velocity.

Numerical passivity: RK4 is not exactly conservative; within the accepted residual band, if the return kinetic energy exceeds the initial, the exit velocity is scaled down to equality. A valid return may leave up to the residual tolerance of energy in the string, because `l` can still exceed `L` slightly at the crossing; that is the accepted approximation, not a claim of zero stored energy.

The trajectory also yields the maximum depth `delta` and its chord position `u_h = xi` at the peak; the domain rule permits `u_h` up to 2 percent outside `[0, 1]` (the accepted default-material case `u = 0.1`, 30 units per step, minus 30 degrees peaks at about `-0.012`), and the zone and visual profiles clamp it per 2.3.

### 2.2 Response path

`SweptCircleCollider.Collide` keeps its current path (`Collide3DWall`) for untagged capsules. For a tagged capsule it calls `RubberSpanRebound.Respond`, which is transactional: everything up to step 5 is computed on copies of the ball state without touching the ball, the random state, or the event queue, and only step 6 commits.

1. Relative velocity against the capsule's surface velocity at the contact point (zero for the static rubbers this applies to); if the relative normal speed is not approaching by more than `LowNormVel`, return without any effect. `Collide3DWall` additionally kicks an embedded ball (`HitDistance` below `-Embedded`) outward at `EmbedShot` in that case; this path deliberately omits that exception, because the capsule's own touch-band classification and the static-contact recovery already handle embedded starts, and the omission is documented as a difference.
2. `e` resolved from the capsule's material as today (`ResolveElasticity` with the incoming normal speed along `n`, times the capsule's guide-distance elasticity scale), clamped to `[0, 1]`. Both branches below use this clamped `e`; the rigid branch is therefore not a call into `Collide3DWall`, whose elasticity is unclamped.
3. **Rigid branch** `v_rigid`: the wall's normal-restitution step, `v + (-(1 + e) * v_n) n`, on the relative velocity.
4. **Compliant branch** `v_compliant`: the surrogate of 2.1; on success, restitution on the normal component only: `v_out = e * (v_exit . n) n + (v_exit - (v_exit . n) n)`, the chord component the string produced kept, the out-of-plane component untouched. For normal incidence at the center this equals `v_rigid` exactly. For oblique incidence the string's cupping changes both components (a center hit at chord speed 9 and normal speed 15.6 exits at about chord 3.6 and normal 17.6), a deliberate difference from a flat wall, qualified in 6.1. On integration failure the compliant branch is `v_rigid`.
5. **End blend**: `s = smoothstep(0.02, 0.08, min(u, 1 - u))`, `v_pre = lerp(v_rigid, v_compliant, s)`, and the effective depth `delta_eff = s * delta` with apex `u_h` (zero depth when `s = 0` or on failure). Both endpoints are passive linear responses and the norm is convex, so `v_pre` is passive; no spin has been touched yet.
6. **Commit**, once, in the wall's order: penetration recovery along `n` exactly as `Collide3DWall` does (gain 0.9875, cap 5; a start penetrated deeper than the cap is left to the existing static-contact recovery in later ticks, as for every other collider); write the relative velocity change `v_pre - v`; impact friction through a helper extracted from `Collide3DWall` with the wall's own budget, `mu * m * |v_n,in|` on the incoming normal speed, and its own slip direction and velocity-table sampling on the post-response velocities, so a center normal hit reproduces the complete existing response, friction and spin included; scatter as today; the guard: if the relative velocity along `n` is still approaching, add the smallest impulse along `n` that stops it (non-approach at the response instant, not a tunneling proof for penetrated starts); the hit event as today; and the impact event of section 4 only when `delta_eff > 0`.

Passivity of the whole path: the blend is passive before any spin change; restitution with `e <= 1` removes energy; the extracted friction is the wall's dissipative friction; the guard is dissipative. The rigid branch under this path differs from `Collide3DWall` only by the clamp on `e`, and the clamp only matters for materials authored above 1.

### 2.3 Capsule tags and coordinates

Each `SweptCircleCollider` emitted for an eligible span gains `SpanId`, `U0`, and `U1`, the arc-length fractions of its ends computed by the generator from its own emitted polyline (cumulative chord length over the final point list divided by the total), so the tags match the geometry that collides. Untagged capsules (supported arcs, curved spans, Legacy) have `SpanId = -1`; tags survive `Transform`. At impact `u = lerp(U0, U1, alpha)` with `alpha` the closest-point parameter along the capsule axis.

For a straight span the Bezier parameter equals the arc-length fraction, so the slingshot's authored `Position01` and `ArmContactPosition01`, which the resolver feeds to `PointAt` as curve parameters, keep their meaning without migration. Reversed bindings map `u -> 1 - u` as `ResolvedRubberSpan.ToPathCoordinate` does. Below `u = 0.02` the response is purely rigid with zero effective depth, which covers span ends inside supported-arc capsules and the full-roundness case where two straight spans share an anchor with no arc; zone and visual profiles clamp `u_h` into `[0.02, 0.98]`. Both sides of the blend are tested.

### 2.4 Span records

Spans are registered in `NativeParallelHashMap<int, RubberSpanRebound>` keyed by a dense span id assigned at collider creation: anchors `A` and `B` in playfield space, chord direction, outward normal, `L`, `L0`, `A0`, `MaximumStretchRatio`, the 128-sample stress table over `[1, MaximumStretchRatio]` in a `FixedList4096Bytes<float>` (the compiled material's own layout, both endpoints included), and the rubber's item id. Constant data, written once at registration, read only by the collide pass.

### 2.5 Eligibility

The rebound is active on a Physical rubber whose bake outcome is `Deformable`, relabeled **Compliant Rebound** in the inspector and documentation (the data field stays `DeformationEnabled`; the scripting define is dropped, because the runtime addition is a response branch and two state maps with no new collider type, advancement, or cycle change). Per span the baker additionally requires straightness (`RubberPathMath.IsStraightFreeSpan`) and records `ReboundEligible` on the descriptor; a rubber with Roundness above zero bakes `StaticFallback` with the reason "compliant rebound requires straight free spans in this version". The material domain check: stress non-negative over the whole table (the default curve is zero at stretch 1, which is correct) and tension strictly positive from the installed stretch to the maximum stretch. A default-material bake is an acceptance test. At generation, a kinematic rubber, a stale bake (the kinematic flag is a hash input), a missing table, or a descriptor without `ReboundEligible` emits untagged capsules with one warning.

## 3. Slingshot runtime

### 3.1 Devices, state, and the tick contract

`RubberSlingshotComponent` becomes live: `RuntimeAvailable` is true, `Awake` no longer disables it, `CoilDevice` returns a `RubberSlingshotApi` that registers with the player as the bumper api does, implementing the switch device and the coil device without the bumper's immediate push shortcut. Validation requires, in addition to today's checks, that the bound rubber has `DeformationEnabled`, a current `Deformable` bake, and a `ReboundEligible` descriptor for the bound span; the wizard reports these and the inspector diagnoses them.

Physics state: `RubberSlingshotStates`, keyed by item id, holds the span id; per-zone closed-until times and the logical switch level; **two source levels**, `SelfWireLevel` and `CommandedLevel`, and the derived `CoilLevel = SelfWireLevel || CommandedLevel`; a fixed ring of at most four self-wire pulses `{ riseTime, fallTime, causalBallId, closureTime }`; an ordered ring of at most eight **commanded transitions** `{ level, stampTime }`; and counters for kicks, dropped pulses, and dropped commands. It is registered with the other item states and advanced once per tick by `PhysicsUpdate.UpdateSlingshots` before the cycle.

Per tick: expire zone dwells; then collect every due source transition (self-wire rises and falls whose time has come, commanded transitions whose stamp has come) into one list and apply them **one at a time** in a single total order: by due time, then by an explicit same-time precedence (a self-wire pulse's rise before its own fall, so a pulse whose rise and fall were both skipped by the engine's time skip still kicks; otherwise falls before rises, so a pulse re-armed at the instant another ends produces a fresh edge), then by command insertion order. After *each* applied transition `CoilLevel = SelfWireLevel || CommandedLevel` is recomputed and its edge handled immediately: a rising aggregate edge applies the kick once and enqueues `CoilEventsEnergized`; a falling aggregate edge enqueues `CoilEventsReleased`. Aggregating only after the batch would erase transitions, so commanded on-off inside one drain yields one energize and one release, on-off-on yields two energizes, and a self-wire fall while the commanded level is held high changes nothing.

**Command path.** Coil commands from the main thread (gamelogic engine, player wiring, script) never run inside the physics step. The existing managed input-action delegate, drained at the start of `ExecuteTick` before `PhysicsUpdate.Execute`, appends `{ level, stamp }` to the commanded-transition ring, with `stamp` the physics time of the tick boundary at which the delegate runs; the tick hook consumes transitions in order at the first executed tick with time at or after the stamp, which is that tick, including under catch-up. Every transition is preserved in order, so on-off-on inside one drain yields two rising edges of `CommandedLevel`; the ring drops the oldest on overflow and counts it. `DeviceCoil`'s repeated enable callbacks append repeated `true` levels that the hook treats as no transition, so they cannot retrigger.

### 3.2 Switch zones and pulse delivery

Every impact on the bound span with `delta_eff > 0` reports `u_h` and `delta_eff`. The V deflection at a zone is `w(u) = delta_eff * u / u_h` for `u <= u_h`, else `delta_eff * (1 - u) / (1 - u_h)`, with `u_h` clamped per 2.3. A zone closes when `w(Position01) >= CloseDeflection` and stays closed for `MinimumClosedTimeMs` in simulation time; `OpenDeflection` is unused by the static model, kept in data, and marked so in the inspector. Zones are OR-ed into the logical switch; only edges are enqueued as `EventId.SwitchEventsClosed` and `SwitchEventsOpened` with the simulation time in `FloatParam` and the causal ball id. Rigid-end impacts and integration failures have zero effective depth and close nothing.

**Pulse delivery.** The main-thread event drain can deliver a close and its open in one frame. `RubberSlingshotApi` delivers the close to the gamelogic engine immediately and defers the open until at least `MinimumSwitchPulseMs` (default 30, main-thread time) after the delivered close; a second close inside the window extends it. Player wires receive the raw edges, because wires exist for immediate direct control. This contract guarantees the spacing at the api's delivery call. It does not, and this plan does not claim to, guarantee spacing past a gamelogic engine that queues switches onto its own thread: PinMAME's integration drains its external switch queue before advancing the emulation fence, so a stall spanning the whole interval can still present close and open to the emulator consecutively. Fixing that belongs to the PinMAME integration (a timestamped switch queue applied between emulation steps) and is recorded as an open item; the self-wire kick does not depend on it.

### 3.3 Wire, coil, and kick

Two sources energize the coil:

- **Self-wire** (default on, `SelfWired`): when the logical switch closes, a pulse with `riseTime = closure + WireDelayMs` (default 8) and `fallTime = riseTime + SelfWirePulseMs` (default 20) is appended, bound to the causal ball id and closure time, entirely inside the physics thread. A closure while a self-wire pulse is pending or `SelfWireLevel` is high schedules nothing; pulses are bounded by the zone dwell, and an overflow attempt is counted and dropped. This is the deterministic path and the one qualified below.
- **Commanded**: the gamelogic engine or a player wire drives `CommandedLevel` through the command path. Latency is whatever the engine and the drain add; a ball that has left the reach by then is missed, as a real arm misses a fast ball. No latency bound is claimed for this path.

On a rising aggregate edge the kick applies once, on that tick, to every ball that is not frozen, is on the outward side of the span, is within the rubber's height range, has contact fraction `u` within `ArmContactPosition01 +- ArmHalfWidth01` (new field, default 0.25), and satisfies one of:

- **reach**: closest distance to the rest chord at most the fired arm displacement `ArmTipTravel - ArmRestClearance` (16 units with defaults, the convention the inspector already draws) plus the ball radius plus the cord radius;
- **window**: it is the causal ball of the self-wire pulse whose rise produced this edge and the pulse's closure is at most `KickWindowMs` (default 30) old. This is deliberately a remote boost standing in for the contact duration the static model does not have; it is what lets a hard hit still get its kick. A causal ball that is frozen, captured, or removed is not eligible. Commanded edges have no causal ball and use reach only.

The kick is a velocity change along the outward normal at the arm position of magnitude `KickSpeed * (1 - x^2)`, `x = (u - ArmContactPosition01) / ArmHalfWidth01`, with `KickSpeed` a new authored field in VPX units per step (default 30; the inspector shows the equivalent in meters per second). The actuator asset stays assigned and packaged for a future physical arm and is not consulted; the inspector says so.

Timing with the defaults, in simulation time: a switch-closing hit at 18 units per step (1.8 units per 1 ms tick) rebounds at roughly 1.2 to 1.5 units per tick; with an 8 ms wire and one tick of processing the ball has traveled about 11 to 14 units, its center is about 29 plus that from the chord, and the reach is 16 plus 25 plus 4, so it is inside. At 30 units per step it is not, and the window rule kicks it anyway. Held coil, rapid retrigger, and stuck switch are bounded by construction: one kick per aggregate rising edge, level-based sources, dwell on the zones, bounded rings, no stored energy.

### 3.4 Packaging

`RubberSlingshotPackable` gains `SelfWired`, `WireDelayMs`, `SelfWirePulseMs`, `KickSpeed`, `ArmHalfWidth01`, `KickWindowMs`, and `MinimumSwitchPulseMs` with a version bump and defaults. Unpacking validates as today and no longer disables a valid component.

## 4. Visuals

Main-thread animation driven by events, as the legacy slingshot component does; no snapshot channel.

- **Impact events.** `EventId.RubberSpanImpact` carries the rubber item id, the ball id, the span index in the event's integer payload (`EventData` gains an `IntParam` if it has none), the simulation time in `FloatParam`, and `u_h` and the signed `delta_eff` packed as two half-precision values in a new `FloatParam2`. Adding fields to `EventData` is compatible with every consumer found: queues and deferred lists copy the struct and the constructors zero unspecified fields; existing overloads are preserved and dispatch is extended. The event is emitted only for impacts with `delta_eff > 0`, independently of the gameplay hit-event settings; rigid ends and failures emit nothing. The renderer selects the span by index, restarts its twang on each event, and drops events older than 100 ms of simulation time when they arrive.
- **Coil events.** `CoilEventsEnergized` and `CoilEventsReleased` from the tick hook on aggregate transitions, with item id and simulation time; the api routes the energized event to the animation, since a self-wire pulse is otherwise invisible to the main thread.
- **Passive twang.** The renderer displaces render vertices along the span's *inward* direction with a V profile with apex at `u_h` and depth `delta_eff`, decaying exponentially with a 40 ms time constant.
- **Kick animation.** On the energized event the renderer plays an authored curve over `AnimationDurationMs` (default 60) that displaces the span *outward* with a V profile at `ArmContactPosition01` and depth `(ArmTipTravel - ArmRestClearance) * curve(t)`, and rotates the arm visual from `VisualRestAngle` to `VisualFiredAngle` on the same curve, as the legacy component does. While the kick animation plays it replaces the twang; the two are signed and never summed.
- The renderer's curve and duration are packaged with a version bump of its packable. Rubbers without a renderer show nothing.

## 5. Code changes

Runtime:

- `Physics/Collider/SweptCircleCollider.cs`: `SpanId`, `U0`, `U1`, preserved by `Transform`; the collide path branches to the response.
- `VPT/Ball/BallCollider.cs`: the impact-friction helper extracted from `Collide3DWall`, which keeps calling it, so untagged behavior is unchanged.
- `VPT/Rubber/Rebound/RubberSpanRebound.cs`: span record, the surrogate integration with its domain rules, energy correction, the transactional response of 2.2.
- `VPT/Rubber/RubberPhysicalColliderGenerator.cs`, `RubberApi.cs`: span records from the descriptors, capsule tags from the emitted polyline, eligibility checks.
- `Game/PhysicsEngineContext.cs`, `Game/PhysicsState.cs`, `Game/PhysicsEngine.cs`, `Game/PhysicsUpdate.cs`, `Game/PhysicsEngineThreading.cs`: the two state maps, registration, disposal, the slingshot tick hook, the commanded-transition append from the input-action delegate.
- `VPT/Rubber/Slingshot/RubberSlingshotApi.cs`, `RubberSlingshotState.cs`, `RubberSlingshotPhysics.cs`, `RubberSlingshotComponent.cs`, `RubberSlingshotPackable.cs`.
- `VisualPinball.Engine/Game/EventId.cs`, `Physics/Event/EventData.cs`, `Game/Player.cs`: the five events, the payload extension, routing.
- `VPT/Rubber/Deformation/RubberDeformationRenderer.cs`: the body, its packable.
- `VPT/Rubber/Deformation/RubberSpanDescriptor.cs`, `RubberPackable.cs`: `ReboundEligible`, packable version 4, bake version bump so older bakes read stale.

Editor: `RubberDeformationBaker.cs` (straightness, domain check, `ReboundEligible`), `RubberColliderInspector.cs` (relabel, drop the define), `RubberSlingshotInspector.cs` and `RubberSlingshotWizard.cs` (new fields, eligibility diagnostics, actuator note).

Docs: `rubbers.md`, `slingshots.md` physical section, `CHANGELOG.md`. The deformable plan's status line points here.

## 6. Tests

### 6.1 Rebound, outside the engine

- **Reference**: the surrogate trajectory in double precision at 10 microseconds, for both worked materials, at `u` of 0.5, 0.25, 0.1, and 0.05, at 8, 18, and 30 units per step, at normal incidence and at plus and minus 30 degrees (the two obliquities are not equivalent off center).
- The runtime integration (single precision, 0.5 ms, 600-step cap, linear return refinement) matches the reference in exit speed within 1 percent and angle within 1 degree on every case of that matrix that integrates successfully (the review's replay measured 0.03 percent and 0.006 degrees), records its step count per case (at most 201 measured), and reports exactly the two expected fallbacks of 2.1 as failures and nothing else.
- Domain: a 70 degree oblique center hit at 18 units per step is an integration failure by the residual rule; an apex leaving the anchor interval is a failure; an absurd speed exhausting the table is a failure; a non-finite state is a failure; each falls back to the rigid branch and counts.
- Energy: the reference conserves energy within 0.01 percent; the runtime's correction engages only above float noise; with `e` in `[0, 1]` the whole committed response never adds translational-plus-rotational energy over 10 000 random impacts including spin and obliquity; the guard fires only when needed.
- Center normal incidence reproduces `Collide3DWall` within numerical tolerance, friction and spin included, for every `e` in `[0, 1]`, on cases where the guard stays inactive; the case where it activates (zero restitution, friction with enough spin, and a scatter rotation that leaves the wall's result slightly approaching) is asserted as an expected difference, as is the embedded-ball exception of 2.2 step 1. Oblique center incidence differs by the recorded amounts in both components.
- The end blend is continuous across `u = 0.02` and `u = 0.08`, passive on both sides including spin (the review's 50-50 counterexample with tangential spin is a fixed case), and its effective depth is `s * delta`.
- The eight-mode oracle is a declared sanity check, not a gate: at the center and quarter points its exit speed with unit restitution is within 10 percent of the surrogate reference, which confirms the massless limit for this cord mass; where it is not, the plan records it.

### 6.2 Slingshot logic

- Zone closing from the effective-depth V profile for center, quarter, and near-end hits, none for rigid-end hits; dwell and opening in simulation time; OR into one switch; only edges enqueued.
- Pulse delivery: a close and open arriving in one frame reach the gamelogic delivery call as a pulse of at least the minimum duration; wires receive raw edges.
- Sources and edges: repeated enables do not retrigger; on-off in one drain yields one energize and one release, and on-off-on two energizes, each tested with the self-wire level low and held high; a self-wire fall while the commanded level is high does not clear the coil or emit a release; a pulse with both times skipped kicks then falls in one tick; falls before rises across different pulses; one kick per aggregate rising edge.
- Kicks: balls in reach, the causal ball inside its window, never behind the rubber, outside the arm width, frozen, captured, or removed; commanded edges use reach only; a coil with no ball in reach only animates; rings never overflow under rapid retrigger and count dropped entries.
- Fault cases: held coil, rapid retrigger, stuck switch, impact during the animation.
- Packable round-trips and old-payload defaults for the slingshot, the renderer, and the collider (version 3 payloads read stale and fall back).

### 6.3 Integration

- Collider stream identical for Legacy, Physical without the toggle, and the fallback outcomes; tags present with the toggle; curved spans untagged.
- Play mode, through the existing `EnterPlayMode` editor-test pattern: a three-post rubber (two-post loops are ambiguous to the binding resolver by design) with a physical slingshot on one span, once self-wired and once wired through the player's wiring with the self-wire off: a scripted shot closes the switch, the coil fires, the ball leaves faster than the passive rebound; with the wire removed it rebounds passively. The self-wired variant asserts the kick tick exactly.
- The existing slingshot authoring test asserting `RuntimeAvailable == false` is updated; the rubber, swept-circle, and Legacy slingshot suites are unchanged.

## 7. Delivery

Commits on `feat/rubber-deformable-spans`, each with the Unity suite green:

1. Rebound: the friction helper extraction, span records, capsule tags, the transactional response with domain rules and end blend, baker eligibility and domain check, descriptor and packable version, inspector relabel, tests of 6.1.
2. Slingshot runtime: state, tick hook, command ring, api, events and payload extension, zones, pulse delivery, wire, source levels, kick, packables, inspector, wizard, tests of 6.2 and the play-mode tests.
3. Visuals: renderer body, twang, kick animation, arm rotation, packable.
4. Docs, changelog, plan status lines.

Commit 3 depends on the events of commit 2; commits 1 and 2 are sequential.

## 8. Limitations and open items

- The rebound is instantaneous; the kick window is a remote boost standing in for contact duration.
- Only straight free spans get the compliant rebound; rounded spans respond as today.
- Oblique hits lose chord speed to the string's cupping; this is the model, not a calibrated effect. Hits that would carry the apex past an anchor fall back to the rigid response.
- The kick is a velocity change; the actuator asset is not consulted.
- The commanded-coil path has unbounded latency and may miss fast balls.
- Switch pulses can still be collapsed by a gamelogic engine that queues switches onto its own thread; the fix belongs to that integration.
- Supported arcs and pinned ends respond as today; the end blend is a passive interpolation, not a physical model of the anchor.

## 9. Review questions answered

1. Direction rule: none; the exit velocity is the surrogate's, integrated in the string's field.
2. Kick versus hold: reach-based kick with a simulation-time wire and a causal-ball window; no hold state.
3. Guide-distance elasticity: retained as the authored attenuation of `e` on the tagged capsule; direction does not control energy loss.
