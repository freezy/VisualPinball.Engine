# Deformable Rubber Free Spans Implementation Plan (Phase 3b)

Status: superseded. Commits 1 (numerical contract) and 2 (authoring) landed; the commit-1 fixture falsified the contact formulation of section 2.4.4 at the engine substep (section 14), and the maintainer chose the static route on 2026-09-18. The runtime is specified by `RUBBER_STATIC_REBOUND_SLINGSHOT_PLAN.md`, which reuses the authoring commit's descriptors and the fixture's reference solvers. Sections 3 to 9 of this document describe a runtime that is not being built; they are kept as the record of the design and its review.

Scope: free spans of guided rubbers using `RubberColliderMode.Physical`, compiled and enabled only with the scripting define `VPE_EXPERIMENTAL_RUBBER_DEFORMATION`

Non-goals: slingshot switches, coils, and arms (phase 3c); supported arcs lifting off their guides; tension exchange between spans or deflection-induced tension; more than one active mode; kinematic (`IsMovable`) rubbers

## 1. Outcome

A free span of a Physical rubber becomes a pinned, damped, single-mode transverse oscillator. A ball that hits it deflects the cord, the cord's tension decelerates the ball over a finite contact duration, and the stored strain energy returns the ball with a rebound that depends on ball speed, cord mass, tension, and where along the span the hit lands. Collision geometry, contact response, and rendering all read one modal state that is advanced inside the physics time-of-impact loop.

This replaces the static round-cord capsules and the per-capsule elasticity profile on free spans of rubbers that opt in. Supported arcs, Legacy rubbers, Physical rubbers that do not opt in, and every build without the define keep today's behavior and collider stream exactly.

The engine plumbing follows the spring hinge (`VPT/SpringHinge/*`) for where things go: a static collider with a full-travel bounding box, geometry evaluated from item state at hit-test time, conservative advancement for continuous collision detection, displacement stepped inside the time-of-impact loop, stop arrival as an event, and per-tick publishing through the snapshot. Its contact discretization is not reused.

## 2. Model

### 2.1 Reduced span

Each free span element of the baked path has installed arc length `L` (VPX units, `RubberPathElement.Length`) and unloaded length `L0 = L / lambda0` (section 2.3). The transverse deflection at arc-length coordinate `s` along the rest curve is

```text
w(s, t) = sum(n = 1..N) q_n(t) * sin(n * pi * s / L),  0 <= s <= L
```

The state carries four lanes so phase 3c can activate four modes without a layout or package migration; phase 3b sets `ActiveModeCount = 1`. Unused lanes stay zero and never participate in integration, collision, hashing, or rendering.

Positive `w` points toward the rubber loop interior. The inward direction at a point is the rest tangent rotated by 90 degrees toward the interior side, in the bake plane. The interior side is determined once per rubber from the signed area of the whole baked path sampled as a polyline at each element's geometric subdivision count. Deflection is confined to the bake plane; the direction along the bake normal is rigid.

With unloaded linear density `mu0 = rho * A0`, installed tension `T`, and damping ratio `zeta`:

```text
M_n = mu0 * L0 / 2 = mu0 * L / (2 * lambda0)      (mass conserved under stretch)
K_n = T * (n * pi)^2 / (2 * L)                    (installed coordinates)
C_n = 2 * zeta * sqrt(K_n * M_n)
omega_n = sqrt(K_n / M_n)
```

The redesign brief writes `M_n = mu * L / 2` without distinguishing unloaded from installed density; that is corrected here and pinned by a test.

Tension is fixed at its installed value for the whole of phase 3b. Curved rest spans use the same equations along their arc length with the local rest normal as the deflection direction; this is an approximation, exact for straight spans.

### 2.2 Units

VPE physics units per the brief's section 5.0, restated for self-containment:

```text
S  = 1852.71 VPX units per meter        (Physics.ScaleToVpx(1f))
t0 = 0.01 s                             (PhysicsConstants.DefaultStepTimeS)
m0 = 0.080 kg per VPE mass unit         (BallComponent.Mass = 1 is one 80 g ball)

M_vpe = M_kg / m0            K_vpe = K_N_per_m * t0^2 / m0      C_vpe = C_Ns_per_m * t0 / m0
F_vpe = F_N * S * t0^2 / m0  x_vpe = x_m * S                    v_vpe = v_m_per_s * S * t0
```

`A0 = pi * (Thickness / 2 / S)^2` square meters from `RubberComponent.Thickness` (cord diameter, VPX units). `T = sigma(lambda0) * A0` newtons, with `sigma` read from the compiled stress table by linear interpolation between bracketing samples. Commit 1 pins the conversions with a free-fall test against engine gravity and a two-body impulse test.

### 2.3 Installed stretch and rest length

`lambda0 = fittedLoopLength / RestLength`, with `fittedLoopLength` the sum of all baked element lengths. `RestLength` zero means "not configured"; the inspector offers **Set Rest Length From Fitted Length** at a default installed stretch of 1.10. The bake outcome for unconfigured or slack rest length is defined in section 4.3.

### 2.4 Contact

#### 2.4.1 Engine integration contract

The design is built against the engine's actual per-substep order, which this plan does not change except where section 2.4.6 says so:

1. Once per 1 ms tick, before the time-of-impact loop, `BallVelocityPhysics` applies the whole tick's gravity and nudge velocity change; magnets and other mechanisms update their state before the loop as well.
2. Per substep: mechanism stop times shorten the window *before* the ball pass; the ball pass finds each ball's earliest hit within the window; `ApplyStaticTime` reduces the substep to the earliest accepted hit and may *enlarge* a very small hit time to `StaticTime` for anti-starvation (it has no remaining-time clamp of its own); mechanism hit-time protections clamp again; balls are displaced by `Position += Velocity * h`; mechanisms are displaced; the collide pass resolves each ball's selected hit one ball at a time and then clears the ball's event; `PrepareContacts` snapshots ball velocity and spin, deduplicates, and projects support; the contact pass resolves each sustained contact one at a time.
3. Within a substep a ball is force-free: gravity, nudge, and magnets are already in its velocity. Kinematic surfaces can add work at boundaries through their surface velocity; kinematic rubbers are excluded from deformation, and other kinematic items count as external inputs for energy accounting.
4. Modal state is modified only by this plan's code.

#### 2.4.2 Contact basis

The collision geometry is a chain of `K` segments whose end points move with the modal state (3.2). A contact at fraction `alpha` along chain segment `(i, j)` has, per active lane `n`, the interpolated basis vector

```text
b_n = (1 - alpha) * Direction_i * Shape_i,n + alpha * Direction_j * Shape_j,n
```

used for geometry, surface velocity `sum(qdot_n * b_n)`, generalized force, and effective mass alike. Lane participation along a unit direction `e` is `g_n(e) = dot(b_n, e)`. The bake-normal direction has zero participation, so the cord is rigid along it. Nothing about a contact persists between substeps; `alpha`, the normal, and `b_n` are recomputed from current geometry every time.

#### 2.4.3 Per-span contact group

All contacts involving one span in one substep form one *contact group*, solved once. Rows, in stable order (ball id, then contact point index, stop row last):

- one normal row per (ball, contact point): unilateral, `lambda >= 0`, post-solve relative normal velocity `>= 0`, target zero for impacts (zero restitution) and for sustained contacts alike;
- one friction row per normal row, bounded by `mu_f * lambda` of its own normal row;
- one stop row when the lane is at its cap (2.5): unilateral in the cap's outward direction on that lane.

Storage is 12 rows, budgeted by *retained contact pairs* (a normal row and its friction row), not by balls: five pairs, the stop row, and one spare. Before budgeting, a ball's retained contacts on one span are reduced to at most its two deepest, so five pairs cover at least three balls with two contacts each or five balls with one. A group with more than five retained pairs solves the first five in row order and the remainder as a second group against the post-solve state; that remainder is sequential and dissipative, `GroupOverflows` records it, and the simultaneous-contact gate is claimed for up to five retained pairs per span.

The Delassus matrix `A = J W J^T` is assembled in full: per row, the ball's translational inverse mass `1/m` and, for friction rows, the rotational term `|r x t|^2 / I` (with the engine's solid-sphere inertia `I = 2 m R^2 / 5` this makes the tangential ball diagonal `7 / (2 m)`); off-diagonal ball terms for rows sharing a ball, `n_i . n_j / m` plus the rotational cross terms; the modal terms `sum(g_i,n * g_j,n / M_n)` coupling every pair of rows through the shared lanes; the stop row's unit participation on its lane. `A` is symmetric positive semi-definite.

**Solver.** Projected Gauss-Seidel is not used: on the two-ball symmetric case it leaves a 1 percent asymmetry after 16 sweeps, and with friction bounds that change during the sweep it can cycle. The group is solved by an alternating exact scheme:

1. *Normal pass*: the normal and stop rows are solved exactly as a linear complementarity problem by a Dantzig-style active-set pivot (direct solve on the active set, rows pivoted one at a time while processed rows stay complementary), with the friction impulses held fixed at their current values. The matrix diagonal is regularized by `1e-9` relative to its trace so a singular active set never fails.
2. *Friction pass*: friction bounds are computed from the normal impulses of step 1 and frozen, as the Open Dynamics Engine does (its `dSolveLCP` computes each friction row's bound from the already-solved normal impulse and does not revise it afterwards); the friction rows are then solved exactly as a boxed complementarity problem by the same pivot with the normal impulses held fixed.
3. Steps 1 and 2 alternate until every row's complementarity residual is within `1e-5`, at most four times.
4. *Terminal policy*: if the residual is still above tolerance, the friction impulses of the last pass are halved and step 1 runs once more with them fixed; because a shrinking normal impulse can leave friction above its bound, the halving repeats, at most four times, the last round zeroing friction, until friction is inside its bounds. The normal and stop rows are then exactly feasible and complementary by construction, friction is bounded and dissipative but approximate, and `SolverFallbacks` records the group.

Every pass is deterministic and bounded by its pivot count, capped at `8 * rows + 16` pivots per pass (a Dantzig pivot on 12 rows was measured at 139 pivots, so `2 * rows` is not a bound). The two-ball symmetric case is solved exactly in the first normal pass. The fixture in 7.1 checks, on every generated group, normal and stop feasibility *after* the friction pass, friction bounds, complementarity residuals, and agreement in velocities and residuals with an enumerated active-set solution where the solution is unique; the review's cycling case (`g_n = 0.045`, `mu = 0.3`, relative velocity `(-0.7, 20)`) and its sign-reversed variant are fixed test cases and must end normal-feasible.

No restitution is ever applied on a free span. The rubber hit event fires per impact row with the existing threshold rule. A ball touching two spans in one substep appears in two groups solved in span id order; the second sees the ball's post-solve velocity. This cross-span coupling is sequential and dissipative, documented, and tested with a wedged ball.

#### 2.4.4 Modal integration and the passive impulse step

Within a substep every free lane advances with the exact underdamped solution (2.5); a lane at its cap does not move (2.5). The contact group then applies impulses on end-of-substep velocities. Each group solve is a set of inelastic, reciprocal impulses on a positive semi-definite system and cannot add energy; the exact free step conserves the discrete energy without damping and dissipates it with damping. This is variant A2 of the previous revision, which the review confirmed removes the acceleration-based support hazard of the spring hinge's contact formula. The remaining question is accuracy, not passivity: the span flies freely during the substep and the impulse acts at its end, so at `h = 0.1` VPE time units a sustained contact lags the cord by up to one substep of motion. Commit 1's fixture measures rebound error, contact-duration error, and rest drift against the oracle; the acceptance thresholds are in 7.1.

The compliant formulation the brief asks to be prototyped is implemented in the fixture only, as a cross-check on outputs (rebound, duration, peak deflection, drift), with its own semi-implicit step at the oracle's 10 microsecond resolution so its stability limit is irrelevant. It is not a production candidate: a penalty contact stiff enough to hold penetration under a quarter unit at the shot envelope has a contact frequency of order 120 per VPE time unit, which the engine's substep cannot integrate. If A2 fails the fixture thresholds, the plan stops and is revised; there is no silent fallback to a formulation the engine cannot step.

Friction rows use the relative surface velocity including ball spin and the cord's surface velocity at the substep boundary, from the same body states the group uses; the group is solved before the legacy friction snapshots are taken (2.4.6), so those snapshots see post-group velocities as they see post-collision velocities for every other collider.

**Position policy.** After each group solve, for every normal row with penetration `p > StaticContactPenetrationTolerance` (0.5 units, the existing sustained-contact threshold), the ball is pushed along the row's normal by `min(DispGain * p, DispLimit)` unless the normal points within 0.2 of the gravity direction, the same guard the existing recovery uses. Penetration up to 0.5 units is tolerated, as it is for every other sustained contact. Gaps are not closed; a gap is a non-contact. This is the plan's own step at the group solve point, not a call into the existing static-contact handler, whose acceleration-based support term the group replaces. The fixture reproduces exactly this threshold, correction, guard, and ordering. Because a resting ball may sit up to 0.5 units into the cord, the rest gates are stated on the modal coordinate and on drift, not on ball position: `q` within 0.01 units of the analytic loaded equilibrium, ball penetration within 0.5 units, and no secular drift of either over the hold.

#### 2.4.5 Deduplication and seam ownership

`ContactPhysics` merges co-oriented contacts by item id. For deformable cords:

- Two rows of one ball on one span merge when their normals are within 1 degree and their basis vectors differ by under 1 percent of their norm; otherwise both stay.
- A cord row whose basis norm is below `1e-3` is at a *pinned anchor*, where the cord cannot move, and is dropped. The anchor itself is covered by an ordinary static collider: generation emits, at every free-span end point, a degenerate swept-circle collider (`V1 == V2`, which the existing `SweptCircleCollider` already treats as a sphere of `CordRadius`) carrying the anchor's backed material from its descriptor (4.2). It exists whether or not a supported arc is present, so the full-roundness case, where the hull solver omits zero-length arcs and two free spans share an anchor with nothing between them, has exactly one rigid collider at that anchor. Anchor impacts therefore go through the ordinary wall-impact response with restitution and hit events, anchor sustained contacts through the ordinary contact path, and duplicates between the sphere and an adjacent arc capsule are merged by the existing item-id-and-orientation rule because both belong to the rubber. No new deduplication identity is needed.
- Rows from different spans away from anchors are never merged.

#### 2.4.6 Cycle restructuring

`PhysicsCycle.Simulate` changes as follows, and nothing else in its order changes:

1. Before the ball pass, alongside the flipper and hinge stop times: each span contributes its cap-arrival time (2.5) to the window.
2. During the ball pass: a cord hit test may record a *search limit* (3.4) into a per-substep accumulator, reset at the start of each substep.
3. After `ApplyStaticTime` and the existing mechanism clamps, one final clamp takes the minimum of the window, every mechanism stop, every accepted moving-surface hit (the cord's `Record`/`Clamp` pair mirroring the hinge's), and the search-limit accumulator. Nothing after this point enlarges the substep.
4. Displacement: balls, then mechanisms including free-lane advancement and cap arrival.
5. Collide pass: before a ball's selected event is cleared, if the event is a cord, its impact row is collected into that span's group instead of being resolved; all other collisions resolve as today.
6. Sustained-contact candidates against cords are collected into their span's group with their geometry refreshed; frozen balls and invalid rows are discarded; deduplication of 2.4.5 runs; anchor contacts go to the ordinary contact list.
7. Each span group is solved once, in span id order, followed by the position policy.
8. Each solved cord normal row is appended to the ordinary contact list as a *support-only* entry: `PrepareContacts` includes its normal in the friction-acceleration projection for the other contacts, exactly as it would for any resting contact, so a ball resting on the floor against a cord keeps the same Coulomb budget it has today, but the contact pass skips resolving it because the group already did. Snapshotting and deduplication of the ordinary contacts are otherwise untouched; a support-only entry is never resolved twice.

## 2.5 Time advancement, cap, and the in-window trajectory

Between boundaries each free lane advances with the closed-form underdamped solution. The coefficient domain is validated at bake (4.2): `M_n > 0`, `K_n > 0`, `0 < zeta < 1`, finite. The runtime asserts finiteness in checked builds and treats a non-finite state as a fatal diagnostic.

**In-window trajectory.** Within any substep the trajectory of every lane is one of exactly two things: the free closed form, or constant (at-cap). Every consumer, the hit test, the displacement step, and the cap predictor, uses the same one, so prediction and advancement never diverge.

**Cap arrival.** Each span has a baked `MaxDeflection`. Before the ball pass, a free lane computes the earliest time in the window at which `q_1` reaches `+cap` or `-cap`: the closed form's extrema `t_k = (atan2(...) + k pi) / omega_d` split the window into monotone intervals, each is checked in order for a sign change of `q - cap` and `q + cap`, and the first bracket is refined by bisection to `1e-6` VPE time units (17 iterations for a full tick), with a validity check at `t = 0`. Arrival shortens the substep as `ApplySpringHingeTime` does. At arrival the lane's outward velocity is set to zero, the removed kinetic energy is counted, and `CapSign` is set.

**Cap residence.** An at-cap lane has `q = +-cap` and does not advance; its in-window trajectory is constant, so the hit test's rate bound for that span is the ball speed alone. The group solve includes the stop row, so balls pressing the cord into the cap receive compressive impulses against a rigid lane, sustained loading at the cap holds by construction, and the energy change is the group's. **Release** is decided at the substep boundary after the group solve, by two rules that never divide by the substep length, so a zero-duration substep and any partition of a tick give the same answer:

- *Impulsive release*: if the post-solve lane velocity points away from the cap (the stop row is unilateral and permits it), `CapSign` clears and the lane resumes the free trajectory with that velocity preserved.
- *Quasi-static release*: otherwise the sustained load is estimated from continuous quantities only: the spring force `K * cap` toward release against the sum over the group's normal rows of each ball's external load pushing the cord into the cap, `m_b * dot(a_ext,b, -n_b) * g_b`, where `a_ext,b` is the ball's tick-level external acceleration (gravity plus nudge plus magnet, the same vector the engine's contact pass uses). If the spring exceeds the load, the lane releases with `qdot = 0`; otherwise it stays. Impacts in the same boundary do not enter this rule; the stop row absorbs them.

This is the hinge's total-torque release rule expressed in forces rather than in per-substep impulses, which is what makes it independent of how a tick is partitioned. The review's counterexample (constant outward load 0.4, cap 6, one 0.1 substep versus two of 0.05) is a fixed test in commit 1 and must give the same residence in both partitions. A stop-only group still evaluates release, so an unloaded lane leaves on the first boundary at which the spring pulls it back.

**Accepted-hit safeguard.** The cord's `Record`/`Clamp` pair (2.4.6 step 3) prevents `StaticTime` enlargement from advancing a ball past a moving cord after an accepted hit.

## 3. Runtime data

### 3.1 State

```csharp
public struct RubberSpanState
{
	public float4 Q, QDot;
	public float4 ModalMass, ModalStiffness, ModalDamping, Omega, DampingRatio;
	public byte ActiveModeCount;
	public sbyte CapSign;
	public float MaxDeflection, SurfaceFriction;
	public int ColliderId;
	public int CapArrivals, GroupOverflows, SolverFallbacks, SearchLimitSubsteps, SearchLimitCapHits;
	public float CapEnergyRemoved;
}
```

No per-contact state persists in the span; groups live in a substep-local 12-row buffer. State lives in `PhysicsEngineContext.RubberSpanStates`, a `NativeParallelHashMap<int, RubberSpanState>` keyed by a dense span id, passed into `PhysicsState` like `SpringHingeStates`. Span ids are assigned at registration: rubbers sorted by item id, spans by path element index. A main-thread dictionary maps `(item id, path element index)` to span id.

### 3.2 Collider

`ColliderType.DeformableCord`, one collider per span:

```csharp
internal struct DeformableCordCollider : ICollider
{
	public ColliderHeader Header;
	public int SpanId;
	public float CordRadius;
	public int SegmentCount;                            // 8..64
	public FixedList4096Bytes<CordChainPoint> Points;   // SegmentCount + 1
	public ColliderBounds Bounds { get; }               // envelope, 3.3
}

internal struct CordChainPoint
{
	public float3 RestPosition;   // rubber-local VPX at generation, transformed at insertion
	public float3 Direction;      // unit inward deflection direction
	public float U;               // arc-length fraction
	public float4 Shape;          // sin(n * pi * U), n = 1..4
}
```

Deformed chain at time `t`: `P_k(t) = RestPosition_k + Direction_k * dot(Shape_k, Q(t))`; surface velocity `Direction_k * dot(Shape_k, QDot(t))`. End points never move, so they coincide with the adjacent capsules.

**Resolution.** Chain points are spaced uniformly in arc length using the baked table. The displaced curve is affine in amplitude, `P(s; A) = R(s) + A * D(s)` with `D(s) = d(s) * sin(pi s / L)`, so the chord interpolation error at amplitude `A` is bounded by `E_R + |A| * E_D`, where `E_R` and `E_D` are the corresponding-fraction interpolation errors of the rest curve and of the unit displacement shape. Each is certified by the standard bound `E <= (delta_s)^2 / 8 * max |P''(s)|` per segment, with the second derivative bounded conservatively over the segment: for the rest curve from the Bezier hodograph's control-point bounds converted to arc length with the segment's minimum tangent speed; for `D(s)` from the analytic derivatives of `d(s)` (which involve the curve's curvature bound) and of the sine. A segment whose minimum tangent speed falls below `1e-3` of the mean is degenerate and cannot be certified. `K` is the smallest multiple of 4 from 8 to 64 with `E_R <= 0.05` units and `E_R + cap * E_D <= 0.25` units; the straight-span estimate `K >= pi * sqrt(cap / 2)` seeds the search. Failure to certify at 64 segments, including the degenerate case, records the static fallback of 4.3. The `RubberPathMath` flatness subdivision remains what the rest capsules use; it is not the certificate.

**Coordinates.** The generator emits chain points in rubber-local VPX space with the item matrix. The collider implements `Transform(matrix)` for uniform-scale matrices (positions and envelope through the point transform, directions through the rotation, radius by the scale); `ColliderReference.Add` bakes the geometry and inserts at identity; non-uniform scale throws as for swept circles; a deformable cord flagged kinematic is rejected in `Add` and never produced by the generator.

### 3.3 Envelope, cap, and speed envelope

`MaxDeflection` per span is the smaller of the rubber's **Deflection Clearance** (default 60 VPX units) and the single-mode deflection at which the span's stretched length reaches `MaximumStretchRatio`. With fixed tension the model is too soft at large deflection, so fast hits reach the cap routinely (section 12); cap arrival is therefore an ordinary event.

The collider's `Bounds` is the rest chain's box expanded by `MaxDeflection` along both signs of every direction, plus `CordRadius`, plus the generation margin; the static octree is never rebuilt for deflection. Per query, a cheap rejection uses the current chain box expanded by the rate bound times the window.

**Speed.** There is no enforced cord-speed ceiling: a position cap does not bound post-impact modal velocity (a unit ball at 100 units per step gives the default span a modal speed near 580 after a zero-restitution impact at rest). The hit test's rate bound always uses the actual state, so correctness does not depend on a ceiling; only work does, and work is bounded by the per-tick cap in 3.4. The tunneling gate is tested to 100 units per step of ball speed and to the post-impact modal speeds those shots produce.

### 3.4 Hit test

Conservative advancement: at most 32 iterations, safety factor 0.8, hit tolerance `PhysTouch`. The distance function is the minimum sphere-to-segment separation against the chain at time `t` using `ball.Radius + CordRadius`. The rate bound is

```text
rateBound = |ball.Velocity| + sum(n active, free lanes) sqrt(qdot_n^2 + omega_n^2 * q_n^2)
```

rigorous because within a window every lane follows the free closed form or is constant (2.5): `d/dt (qdot^2 + omega^2 q^2) = -2 (C / M) qdot^2 <= 0`, and linear interpolation between chain points preserves the bound. Impulses land only at boundaries, where the bound is recomputed.

**Classification at `t = 0`.** With `sep` the current separation and `v_n` the relative normal speed (positive when separating):

- `sep <= PhysTouch` and `|v_n| <= ContactVel`: a sustained contact row for this substep.
- `sep <= PhysTouch` and `v_n < -ContactVel`: an impact at `t = 0`.
- `sep <= PhysTouch` and `v_n > ContactVel` (in the band but separating): no zero-time event. The advancement below runs from `t = 0` with the *surface* as its target: the cord can return, penetrate, and leave within one substep, so a fixed search limit is not enough; the search must find the first return.
- `sep > PhysTouch`: advancement below.

**Advancement.** Iterate `t += max(1e-7, 0.8 * sep(t) / rateBound)` while `t < window`, where `sep(t)` is the signed separation to the cord surface. The rate bound is a Lipschitz constant for `sep`, so this never skips `sep` reaching zero; at each iterate, if `sep(t) <= PhysTouch` and the relative normal velocity at `t` is approaching, that is a *hit* at `t` (never a contact, even if slow, so it shortens the time of impact; the next boundary's classification makes it a sustained row if slow). Starting inside the band and separating, the iterates are small at first and grow as the ball leaves the band; if the cord returns, `sep` shrinks, the iterates shrink, and the return is caught before penetration. The review's counterexample (cap 0.01, `qdot = -1`, ball 0.001 away, a return at `t = 0.0375`) is a fixed test. Outcomes:

- a hit at some `t > 0`;
- window covered: certifiably no contact in this window;
- 32 iterations exhausted with covered time `t_c < window`: no hit, and search limit `t_c`.

**Search-limit contract.** Limits are accumulated during the ball pass and applied once, as the final minimum after `ApplyStaticTime` (2.4.6 step 3), so nothing enlarges a certified interval. Work is bounded by a per-tick cap rather than by an arithmetic claim: each tick allows at most 64 search-limit substeps from cords; beyond that, further limits in the tick are ignored, the substep proceeds on the ordinary hit time, and `SearchLimitCapHits` counts the tick. That is a documented failure mode with a possible missed cord contact, chosen over an unbounded inner loop, which `PhysicsCycle.Simulate` has no cap for today. A ball skimming inside the touch band along a cord for a whole tick is the worst case (on the order of 200 limit substeps for a stationary cord at 100 units per step of tangential speed) and is exactly the case that hits the cap; the performance gate and commit 1's search-limit fixture measure how often it happens on real shots.

The hit test counts under `PhysicsCounters.DeformableCord`; the span counters, including `SearchLimitCapHits`, are trace tick columns. The hit test is a pure function of the collider, the span state, and the ball, so it is testable outside the engine; commit 1 includes its tunneling and search-limit fixtures (7.1).

### 3.5 Dispatch and registration sites

Every runtime site is inside the define (4.1):

- `ColliderType.cs`, `Collider.cs`, `ColliderReference.cs`, `NativeColliders.cs`: the standard collider-type additions, copied from `SweptCircle`.
- `PhysicsState.cs`: `RubberSpanStates` field and constructor parameter, hit-test dispatch with state, `GetRubberSpanState`; `PhysicsStateHarness` in `MagnetPhysicsTests.cs` updated.
- `PhysicsStaticCollision.cs`, `ContactPhysics.cs`, `PhysicsCycle.cs`: the restructuring of 2.4.6, cap and search-limit clamps, the `Record`/`Clamp` pair, the group buffer and solver call, counters.
- `PhysicsEngineContext.cs`, `PhysicsEngine.cs`: state map and disposal; registration.
- `PhysicsEngineThreading.cs`, `PhysicsMovements.cs`, `SimulationState.cs`, `SimulationThreadComponent.cs`: snapshot channel (5).
- `ColliderComponent.cs`: gizmo (rest chain in the editor, deformed chain in play mode).
- `RubberPhysicalColliderGenerator.cs`, `RubberApi.cs`: emission and the fallback dispatch of 4.3.

**Registration boundary.** `RubberApi.CreateColliders` is called by `PhysicsEngine.Start` before native allocation and by the editor gizmo preview. Generation is pure: colliders plus a separate list of initial span states, no engine state. Registration, called only from `PhysicsEngine.Start` after collider creation and before native population, assigns span ids, writes states, stamps `SpanId`, and copies the backed LUTs of 4.2 into the item-keyed LUT dictionaries. The preview never registers. `PhysicsRenderMaskComponent` needs no change.

## 4. Authoring and compilation

### 4.1 The define

`VPE_EXPERIMENTAL_RUBBER_DEFORMATION` is a consumer-set scripting define; the package has no precedent (`FPS60_IOS` is the nearest and undocumented). Per the brief's section 12.1 the gate is at the runtime boundary. Always compiled: every serialized type (descriptors, compiled material, the renderer component shell, packables), the baker and its inspector fields, diagnostics. Define only: state map and accessors, collider dispatch and containers, the cycle changes, snapshot channel, registration, generator emission, the renderer's runtime body. Without the define the inspector preserves every field behind **Physical deformation runtime unavailable**, the renderer component exists and does nothing, and deformation validation is not evaluated at all, so define-off loading preserves today's behavior exactly.

The always-compiled-dispatch alternative was rejected in review. The cost is `#if` at roughly twelve sites; the mitigation is the full Unity EditMode suite in both configurations before every commit (HDRP project with the define, the package's own test project without) plus a define-on export and define-off load test. CI runs no Unity suite today; restoring the game-ci job with both configurations is a maintainer decision and an open compliance item.

### 4.2 Editor bake

Per the brief's sections 5.7 and 10.1, coefficients and contact response are baked in the editor and packaged. Serialized on `RubberColliderComponent`:

```csharp
[Serializable] public struct RubberCompiledDeformationMaterial
{
	public int CompilerVersion;
	public float LinearDensity, DampingRatio, SurfaceFriction, MaximumStretchRatio;
	public float[] StressLut, TangentStiffnessLut;   // 128 samples, stretch 1..MaximumStretchRatio, VPE units
}

[Serializable] public struct RubberSpanDescriptor
{
	public int PathElementIndex, StartBindingIndex, EndBindingIndex;
	public float InstalledLength, UnloadedLength, InstalledTension, TangentStiffness;
	public float4 ModalMass, ModalStiffness, ModalDamping;
	public float MaxDeflection, SurfaceFriction;
	public sbyte InwardSign;
	public int SegmentCount;
	public float[] ArcLengthFractions;
}

[Serializable] public struct RubberBackedResponseDescriptor   // one per guide binding (anchor), whether or not an arc exists
{
	public int BindingIndex;
	public int PathElementIndex;                                // the supported arc, or -1 when the hull omitted a zero-length arc
	public PhysicsMaterialData Material;                        // flags and scalar record
	public float[] ElasticityOverVelocityLut, FrictionOverVelocityLut;   // 127 samples when the flags are set, the runtime's FixedList512Bytes<float> layout
	public float ElasticityScale;
}

public enum RubberDeformationBakeOutcome { Deformable, StaticFallback, Invalid }   // serialized field defaults to Invalid explicitly; the zero value must never read as a bake
```

plus `BakeOutcome`, `BakeReason` (a string diagnostic), `DeformationBakeVersion`, and `DeformationBakeHash`, a canonical hex string over every input that affects eligibility or outcome: baked path elements and classification, the bake transform, thickness, rest length, deflection clearance, the kinematic flag, the deformation material's scalars and every keyframe field including wrap modes, the backed contact material (asset contents or inline override values) including its velocity LUT sources, the guide elasticity settings, the guide slots' support properties, and the compiler version. Changing the kinematic flag after a `Deformable` bake therefore makes the bake stale.

The bake runs from the inspector's **Bake Deformation** action, from the rubber's rebake hook, and from a material-change hook: `RubberGuideDependencyTracker` skips path rebakes when the path is valid, so the collider inspector's change handler and an asset post-processor for both material asset types mark dependents stale directly. A bake produces one of three outcomes. `Invalid` for a material or geometry outside the domain (density not positive, damping ratio outside `(0, 1)`, non-positive stress at installed stretch, stretch above `MaximumStretchRatio`, non-finite values, zero-length spans, a stretch limit that leaves no usable deflection cap). `StaticFallback` for authoring states that are legitimate but not deformable: rest length unconfigured or slack, tessellation budget unmet at 64 segments, `IsMovable` set. `Deformable` otherwise. Stress and tangent-stiffness tables use 128 uniformly spaced samples; linear interpolation of the default zero-tangent curve at stretch 1.10 is then within 0.12 percent of the analytic value (the error scales with the inverse square of the sample count; 64 samples gave 0.45 percent). Fixture coefficients are analytic; baked coefficients carry this table error, which is why 7.1 compares fixture and baked coefficients explicitly.

All structures are added to `RubberColliderPackable` with a version bump; old payloads load with deformation disabled and no bake.

### 4.3 Fallback precedence at generation

`RubberApi.CreateColliders` evaluates, in this order, and the first match decides; each fallback logs one warning naming the reason:

| Condition | Stream |
| --- | --- |
| Define off | Existing behavior; deformation fields ignored |
| `Mode != Physical` or `DeformationEnabled` off | Existing behavior |
| `CanUsePhysical` false (no usable guided path, empty or invalid geometry, or a non-finite bake transform) | Legacy collision, as today |
| Bake missing, or `DeformationBakeHash` differs from the recomputed hash (which includes the kinematic flag) | Legacy collision (brief's stale policy) |
| `BakeOutcome == Invalid` | Legacy collision |
| `BakeOutcome == StaticFallback` | Static Physical capsules from the baked backed descriptors and the static generator |
| `BakeOutcome == Deformable` | Backed capsules from descriptors, one deformable cord per span |

The Legacy rows are stricter than the static Physical path, which since the guide-distance elasticity work consumes any structurally valid serialized path; enabling deformation opts the rubber into the brief's bake discipline. This is deliberate and visible; a maintainer may relax it later.

### 4.4 Inspector

`RubberColliderInspector`, Physical only. With the define: **Experimental Deformation** foldout with **Enabled**, the material field, **Deflection Clearance**, **Bake Deformation** with outcome, reason, and hash, and a read-only block per span (installed stretch, tension in newtons, first-mode frequency in hertz, cap, segment count). Without the define: the same fields preserved and disabled behind **Physical deformation runtime unavailable**. `RubberInspector` gains **Set Rest Length From Fitted Length**. The guide-distance elasticity settings are hidden while deformation is enabled; supported arcs keep their guide elasticity scale through the backed descriptors.

The material asset's default stress curve has zero-tangent keyframes (0.056 MPa at 10 percent stretch), much softer than a linear ramp; changing the default is a product decision flagged in section 12.

## 5. Publishing and rendering

The snapshot gains a third channel in all three buffers: `SpanAnimations[]` of `{ SpanId, float4 Q }`, capacity `MaxSpanAnimations = 256`, written count, source count, overflow flag, validated at initialization like the existing channels (which throw on excess configured capacity). One shared per-tick decision, `SuppressCoupledSnapshot`, computed from the ball, hinge, and span overflow conditions and consulted by all three writers, so any coupled overflow suppresses all coupled channels in both directions and in mixed scenes. `SimulationThreadComponent` reports the counts and the flag. The main thread applies through a `RubberSpanAnimatedComponents` dictionary keyed by span id to `IRubberDeformationReceiver.UpdateSpan(int pathElementIndex, float4 q)`; single-threaded mode applies directly in `PhysicsMovements`. A rubber's spans are contiguous in id order and are published whole or not at all.

Rendering: first the gizmo drawing the deformed chain; then `RubberDeformationRenderer`, an always-compiled component next to the mesh component whose runtime body is gated, displacing render vertices with the same basis as physics: each vertex bound to a chain segment and fraction by nearest point on the rest chain, displaced by `sum(q_n * b_n(alpha))`, cross-section vertices sharing their fraction, no fade along the span. Bindings are reconstructed deterministically from mesh and descriptors on enable and invalidated on mesh or descriptor change; the component packs as ordinary data in every configuration. The renderer owns a mesh instance, reuses arrays, writes vertices and recalculated normals once per frame when any `Q` moved more than 0.05 units, updates bounds, and destroys the instance on disable.

## 6. Planned code changes

New under `VisualPinball.Unity/VisualPinball.Unity/VPT/Rubber/Deformation/`: `RubberSpanDescriptor.cs`, `RubberBackedResponseDescriptor.cs`, `RubberCompiledDeformationMaterial.cs`, `RubberDeformationRenderer.cs` (shell always compiled); `RubberSpanState.cs`, `RubberSpanPhysics.cs` (closed form, cap prediction, residence, release, rate bound), `RubberSpanContactGroup.cs` (row collection, Delassus assembly, active-set solver, fallback, deduplication, position policy), `DeformableCordCollider.cs` (collider, transform, hit test, row emission), and the renderer body (define only).

Editor: `RubberDeformationBaker.cs` (material tables, descriptors, tessellation certification, hashing, outcomes, invalidation hooks).

Modified: the sites in 3.5, `RubberColliderComponent.cs`, `RubberPackable.cs`, `RubberPhysicalColliderGenerator.cs`, `RubberApi.cs`, `RubberColliderInspector.cs`, `RubberInspector.cs`, `RubberGuideDependencyTracker.cs`, `SimulationTrace.cs`, `CHANGELOG.md`, `rubbers.md`, new `developer-guide/rubber-deformation-qualification.md`.

## 7. Test plan

### 7.1 Numerical contract, written before production code

- Unit conversions; free-fall and two-body impulse checks; mass conservation across stretch.
- Closed-form step versus a reference integrator at 1000 substeps per period: frequency and decay within `1e-4` relative over 100 periods; composition `step(a + b) == step(b) after step(a)` within float tolerance; passive energy non-increasing over 100 000 random steps.
- Cap root isolation: for 10 000 random states and windows, the predicted first crossing brackets a sign change of the closed form at `t +- 1e-6` and no earlier crossing exists on any monotone interval, including states starting at the cap and windows with a crossing and return.
- **Group solver**: for 10 000 random groups of up to 12 rows with random positive semi-definite `A`, friction dependencies, and stop rows, the committed solution has normal and stop rows feasible and complementary within `1e-5` *after* the friction pass, friction within bounds, and agreement in velocities and residuals with an enumerated active-set solution where unique; the two-ball symmetric case is exact to float noise; the review's cycling case and its sign-reversed variant end normal-feasible through the terminal policy and are dissipative.
- **Hit test and search limits**, outside the engine: 10 000 randomized shots of a ball against a chain driven by the closed form, to 100 units per step and including post-impact modal speeds, checked against a 10 microsecond scan of the same closed form for missed penetrations (zero allowed) and for reported time no later than the scan's first contact; the in-band-separating return case from the review is a fixed test; grazing shots record search-limit counts per tick and the per-tick cap is exercised.
- **Cap residence**: the review's partition case (outward load 0.4, cap 6, one 0.1 substep versus two of 0.05) gives identical residence; impulsive release preserves the lane velocity; a zero-duration boundary is handled.
- **Coefficients**: the fixture's analytic coefficients and the baker's table-derived coefficients for the section 12 span agree within the table error stated in 4.2.
- **The oracle**: 8 modes, penalty contact sized on the ball for under one percent ball-radius penetration with damping ratio 0.1, fourth-order Runge-Kutta at a nominal 10 microseconds subdivided until the stiffest mode satisfies `omega * dt <= 0.5`; refined in the *step only* until rebound speed, angle, contact duration, and peak deflection change under 0.5 percent. Mode doubling is not a refinement axis: a point contact against a modal truncation has effective mass proportional to one over the mode count, so the local response never converges (section 14).
- **The contact fixture**: an engine-ordering replica per 2.4.1 and 2.4.6 (tick velocity kick, linear ball displacement, free advancement or cap residence, group solve, position policy with the 0.5 unit threshold and gravity guard) driving a ball and a span with the section 12 coefficients through impact, sustained loading under a constant external force with the external work tracked explicitly, release, a 10 second loaded hold, two simultaneous symmetric impacts, an impact while another ball rests, cap arrival under load, and residence at the cap, at random substep lengths. Assertions: `M_n * delta qdot_n == -sum_rows lambda_r * g_r,n` per solve; non-negative normal and stop impulses; complementarity of every row; no growth of ball, spin, and modal energy minus external work; loaded rest with `q` within 0.01 units of the analytic equilibrium, penetration within 0.5 units, and no drift under the production position policy; two-ball symmetry to float noise; rebound speed within 2 percent, contact duration and peak deflection within 5 percent of the oracle at the center and quarter points. The compliant cross-check reports the same outputs alongside.

### 7.2 Collider and contact, in the engine

- Tunneling: 10 000 randomized shots to 100 units per step and cord speeds to the envelope, against the oracle's contact times, misses detected by sampling the oracle trajectory at 10 microseconds; zero misses; reported times no later than the oracle's first contact plus one substep.
- Search limits: grazing and in-band-separating shots exercise the limits; substeps shorten; no contact is missed; the per-tick count stays within the bound of 3.4.
- Rest, side load and transverse load, 10 seconds each, within 0.01 units, no energy growth.
- Two balls symmetric in one substep: mirror velocities to float noise; reversed insertion order: float noise; opposite sides; a wedged ball between adjacent spans; seam contacts at an arc and at a full-roundness shared anchor produce one constraint each.
- Convergence against the oracle at 8, 18, and 30 units per step: rebound speed within 5 percent, angle within 3 degrees, contact duration and peak deflection within 10 percent, at center and quarter points; near-end (`U <= 0.1`) recorded, not gated (single mode has about 0.30 ball masses of contact effective mass at `U = 0.05` versus 0.0021 for eight modes).
- Friction: combined energy never increases; a spinning ball against a moving cord dissipates.
- Cap: arrival ends the substep; a ball pressing at the cap receives a stop-backed impulse; sustained load at the cap holds; release follows the unconstrained acceleration; an unloaded lane releases on the first boundary.
- Six balls on one span: the five-ball group plus the sequential remainder, counter increments, no energy growth.
- Bake outcomes: each `Invalid` and `StaticFallback` reason produces its outcome and diagnostic.

### 7.3 Compatibility and integration

- Collider streams per the table in 4.3, including define-off identity.
- Packable round-trip of every structure; old payloads load disabled; define-on export loads define-off with no missing scripts and all data preserved.
- Backed velocity LUTs survive bake, package, and registration; a supported arc with a velocity-dependent material responds as it does today.
- Determinism: a scripted scene with three balls and two deforming rubbers, 5 seconds, twice, under the simulation thread; fixed span ids, row order, and seeded inputs; hash over `Q`, `QDot`, cap flags, and ball states excluding inactive lanes; identical. Cross-platform recorded, not gated.
- Snapshot: receiver sees the published `Q`; initialization rejects excess capacity; 33-ball dynamic overflow suppresses spans and recovers; injected span overflow suppresses balls and hinges and recovers.
- Registration, preview, native copy, disposal with translated, rotated, and uniformly scaled rubbers.

### 7.4 Performance

On the HDRP project's representative table with rubbers converted, plus a synthetic 40-span loop: hit-test cost per candidate versus the static chain; step time with three balls in sustained contact and with one ball grazing a cord for a full second; registration time, state size, snapshot size. Budget: 40 spans and 4 balls add at most 0.15 ms to the 1 ms tick; envelope boxes raise candidates by at most 25 percent; the grazing second stays within the search-limit bound.

## 8. Delivery sequence

Each step is one or two commits, with the Unity suite run in both define configurations before each:

1. Numerical contract: conversions, closed form, cap isolation, group solver with its enumeration check and the cycling case, oracle with refinement, the engine-ordering contact fixture, the compliant cross-check. No production code. This commit ends with a short addendum to sections 2.4.4 and 13 recording the measured accuracy, drift, and solver behavior, and the plan is re-reviewed in the same reviewer session before commit 3.
2. Authoring: structures, baker, tessellation certification, hashing, outcomes, invalidation hooks, packable version, inspector, renderer shell.
3. Runtime skeleton under the define: state map, registration boundary, advancement, cap events and residence, search-limit clamp, snapshot channel with the gizmo as consumer.
4. Collider under the define: `DeformableCordCollider`, transform, envelope, hit test, cycle restructuring, group collection and solve, deduplication and anchors, position policy, generator emission, fallback table.
5. Qualification: 7.2 and 7.3, determinism harness, performance, the qualification document with every gate's status stated honestly.
6. Rendering, docs, changelog.

No phase 3c work on this branch.

## 9. Acceptance criteria

Against the brief's section 10.5: rest stability (7.2 rests), energy accounting (7.1 fixture, 7.2 friction and cap), determinism (7.3, one platform), continuous collision detection (7.2 tunneling and search limits), multiple balls (7.2 symmetric, wedged, seam, six-ball), numerical robustness (bake outcomes, cap, overflow, solver fallback), contact calibration (oracle stands in; formally open; blocks define removal, not the phase 3c prototype), performance (7.4), no regression (7.3 streams and the existing suites unchanged). Open compliance items carried in the qualification document: no CI job for either configuration; sequential cross-span coupling; the five-ball group limit.

## 10. Deliberate limitations

One active mode; fixed tension, so fast hits reach the cap; bake-plane deflection only; sequential coupling across spans and beyond five balls per span; static or Legacy fallbacks per 4.3; cross-section translation rendering; no physical calibration yet.

## 11. Resolved review questions

1. Contact formulation: exact free advancement plus a passive, exactly solved impulse group at each boundary (A2); the compliant formulation is a fixture cross-check only. The hinge's acceleration-based support is not reused.
2. Closing-rate bound: rigorous because in-window trajectories are free or constant, impulses land at boundaries, and cap arrival ends the window.
3. Compilation boundary: the brief's gate, with always-compiled serialized shells.
4. Cap: arrival by root isolation, residence as a frozen lane with a stop row, release by unconstrained acceleration at the boundary.
5. Tangential response: friction rows in the same group with modal terms, solved exactly with dependent bounds.

## 12. Worked magnitudes

Typical span: thickness 8 units (4.32 mm), installed length 150 units (81 mm), density 1100, stretch 1.10, damping ratio 0.08, 80 g ball. Default zero-tangent curve: stress 0.056 MPa, tension 0.82 N; linear ramp to 2 MPa at stretch 2: 0.20 MPa, 2.9 N. Analytic values; the 128-sample baked tables reproduce them within 0.12 percent.

| Quantity | Default curve | Linear ramp |
| --- | ---: | ---: |
| `M_1` (mass-conserving) | 0.593 g | 0.593 g |
| `K_1` | 50.0 N/m | 178.5 N/m |
| Free first-mode frequency, period | 46.2 Hz, 21.6 ms | 87.3 Hz, 11.5 ms |
| Ball-on-spring loaded half period | 126 ms | 67 ms |
| Uncapped, undamped, idealized center-hit peak at 8 / 18 / 30 units per step | 32 / 72 / 120 units | 17 / 38 / 63 units |

The 1 ms tick gives 11 to 22 samples per free period and over 60 per loaded half period; the closed-form step is exact regardless. An idealized undamped center hit reaches the 60 unit clearance above about 15 units per step with the default material; real hits are damped and off-center, so this is a threshold estimate, not a guarantee. The default material is soft enough that a product review of the asset's default curve is warranted before tuning.

## 13. What commit 1 settles

Measured by commit 1's fixture, not settled by this document: the accuracy of the end-of-substep impulse against the oracle (rebound, contact duration, peak deflection); rest drift under the production position policy; the solver's terminal-policy frequency and cost on real groups; the search-limit substep counts and cap hits on grazing shots. Deferred by the reviewer to the post-commit-1 addendum and re-review, before the authoring and runtime commits they affect: the exact mixed cord-and-ordinary support allocation in `PrepareContacts` (2.4.6 step 8); manifold reduction details for the row budget (2.4.3); the anchor sphere's interaction with the existing deduplication in every geometry (2.4.5); the generation-time handling when structural eligibility fails after a `Deformable` bake (4.3); and the tessellation certificate's derivative bounds (3.2). If the impulse formulation misses the thresholds, the plan stops rather than substituting a formulation the engine cannot step.

## 14. Commit-1 addendum: measured results

Recorded from the fixture in `VisualPinball.Unity.Test/Physics/RubberDeformation/` after commit 1. Everything the plan asked the fixture to pin that is not listed as failing passed: unit conversions (engine gravity 1.81751 reproduced from 9.81 m/s^2), mass conservation, the closed-form step against the reference integrator, composition, passivity over 100 000 steps, cap root isolation (4059 arrivals in 10 000 trials, none missed), the partition case (identical residence for both partitions and both materials), impulsive release, the section 12 coefficients, the table error (0.11 percent at 128 samples, 0.45 at 64), the hit test (10 000 shots to 100 units per step, zero missed penetrations, no late reports), and the solver's feasibility, bounds, symmetry, and dissipation on 10 000 random groups and the review's cycling cases.

**The contact formulation of 2.4.4 fails its thresholds at the engine substep.** Center hit, 8 units per step, default material, against a single-mode oracle (so mode truncation is excluded):

| substep `h` (VPE time) | rebound error | contact duration error | peak deflection error |
| ---: | ---: | ---: | ---: |
| 0.1 (one tick) | 99.3 % | 124 % | 59 % |
| 0.02 | 41 % | 4.3 % | 21 % |
| 0.005 | 12 % | 1.5 % | 5.9 % |
| 0.001 | 2.2 % | 1.3 % | 0.9 % |

The scheme converges to the oracle first-order in `h`, but reaches the 2 percent rebound target only at 100 substeps per tick. Across material, hit point, and speed at the fixture's random substeps the rebound error is 17 to 91 percent and the peak deflection error 36 to 53 percent. The loaded rest drifts by about 0.9 units over five seconds with the ball about 1 unit into the cord, against gates of 0.01 and 0.5; the solver's terminal policy fires on 39 percent of loaded-rest solves with the default material. The mechanism is not a bug: the modal mass is about 1/135 of a ball, so when the cord flies free for a substep and the group then removes the relative normal velocity inelastically, the energy lost per substep is about `K^2 q^2 h / (2 M)`, and a single 8-unit impact keeps 0.03 percent of its energy at `h = 0.1`. No solver, tolerance, or tessellation change alters this; it is intrinsic to resolving a nearly massless compliant body by end-of-substep impulses.

**Consequence.** Per section 13 the plan stops here. The formulation that survives the numbers is the one the second review round rejected on integration grounds rather than physics grounds: while a ball is in compressive contact, the ball's normal degree of freedom and the mode must be advanced *together within the substep* as one linear system (the exact coupled solution, or a fine internal sub-integration of just that pair), with the ball's normal position and velocity written back at the boundary, geometry refreshed per substep, and release by the constraint force sign. The reviewer's objections to that route (force ownership across the tick kick, non-linear ball motion inside a substep as seen by other colliders, frozen Jacobians, multi-ball coupling) are real and must be answered in a revised section 2.4 before any runtime commit. Alternatively the maintainer may decide the static approach is the better investment; see the summary delivered with this addendum.

**Oracle findings.** Mode doubling does not converge: effective contact mass at the center falls as 7.36e-3, 3.69e-3, 1.85e-3, 9.25e-4, 4.63e-4 for 2 to 32 modes and the 32-mode rebound collapses (0.204 against 7.8), so the oracle is fixed at 8 modes and refined in the step only, where it converges on center cases (1.3 percent rebound, 0.5 percent duration between 10 and 5 microseconds). A penalty sized on the ball with 8 modes is at RK4's stability edge at 10 microseconds (`omega * dt` about 2.1 at 18 units per step); the oracle subdivides its step until `omega * dt <= 0.5`.

**Solver findings.** On 10 000 random groups the terminal policy fired in 90 percent of cases, always feasible and dissipative, never exceeding the pivot cap; the maximum pivot count on a 12-row group was 139. The plan's original terminal policy (halve friction once) could leave friction outside its bound; the iterated form in 2.4.3 is what the fixture implements. Complementarity residuals are measured in velocity units through each row's diagonal; the enumerated reference covers the unilateral subproblem with friction fixed and the boxed friction subproblem with bounds fixed.

**Search-limit findings.** 18.5 percent of random shots ended in a search limit; the grazing case hit the 64-per-tick cap on every tick (65 limit substeps per tick), which confirms the cap is load-bearing and that grazing is the expensive case. The review's in-band return case reports the hit when the cord starts approaching at `t = 0.015`, before the penetration at `t = 0.026`, which is the rule working as written; the case does not exercise a return from outside the touch band.

**Commit-2 findings folded in above.** The `Invalid` reason "surface speed above the envelope" and the inspector's surface-speed readout were leftovers of the removed envelope and are gone; the outcome enum's zero value is `Deformable`, so the serialized field initializes to `Invalid` explicitly; the runtime velocity tables hold 127 samples, not 128; `PhysicsMaterialData` needed `[Serializable]` to sit in a descriptor; a stretch equal to the material maximum leaves no usable cap and is `Invalid`; the engine's `t0` and `S` are single-precision constants that match the plan's decimals to about 1e-7.
