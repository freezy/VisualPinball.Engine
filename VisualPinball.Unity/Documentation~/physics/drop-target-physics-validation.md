# Drop target physics validation

The advanced drop-target implementation is developed against three distinct evidence sets. They must not be conflated.

## Compatibility fixtures

`RothDropTargetGoldenData` contains reviewable inputs extracted from these local Roth/nFozzy references:

- `Dark Chaos (apophis 2025) 2.0.vbs`, SHA-256 `D5DC776B80D919E4418732F03CE55BF4586C93C24AFEBABE9E4D28C98E74DD39`;
- `Catacomb (Stern 1981) v2.0.1.vbs`, SHA-256 `5A814437D836211DA377BE4EBFD91BE242043B8F97D4897D86E32C5942DE5E6B`.

Phase 0 deliberately does not assert fixture literals against themselves. Phase 2 must run production `RothCompatible` code against every golden case. The fixture records that:

- target mass is `0.2` in VPE mass units and the actual ball mass participates in the correction;
- normal velocity uses the elastic two-mass ratio while tangent velocity is retained;
- both inspected tables ship with scripted bricking disabled;
- the synthetic brick fixture may enable the existing `30` velocity and `8` center-distance thresholds, but must remain labeled synthetic;
- Dark Chaos backside release uses a velocity threshold of `15`;
- Catacomb's vertical `TargetBouncer` is a table-level post-effect, not part of the Roth drop-target class.

## Moving-collider feasibility gate

Mechanical mode depends on moving mesh contact. Before enabling it for authored content, tests must cover triangle faces, generated edges and points, relative-velocity time of impact, transformed bounds, sustained reset contact, and the supported maximum reset speed. A failure blocks Mechanical mode; it must never fall back to assigning a fixed ball Z velocity.

## Physical calibration

No bundled profile may be called realistic until it has been fitted and validated against a measured target mechanism. Until those measurements are checked in, all Mechanical profiles are provisional. The minimum dataset records ball and target trajectories, contact location, drop/brick outcome, target parts and spring configuration, and reset motion. Fit and validation shots must be separate.

## Performance gate

Profile a deterministic scene with the same ball trajectories in Legacy and Mechanical modes. Capture at least 10,000 physics ticks after warm-up on the reference machine and report median, p95, and maximum tick time. The profiler markers `DropTarget.MechanicalUpdate`, `DropTarget.ContactReduction`, and `DropTarget.ImpactGroup` isolate mechanism integration, stable contact collection/reduction, and the fixed-iteration contact solve.

Record separate runs for 20 idle latched targets, 20 simultaneously moving targets, and the five-target/two-ball contact fixture. Mechanical mode passes the provisional rollout gate only when the complete physics tick adds less than 5% to the Legacy reference. Do not infer that result from the isolated markers or from editor-frame timing. If the gate fails, retain full collision coverage and optimize collider complexity or localized broad-phase updates rather than reducing contact correctness.

## Collision-dispatch integration check

Before promoting a Mechanical profile beyond provisional, run the deterministic five-target scene in Unity with the physics trajectory logger enabled. Use two balls arranged to reach the same target at the same global collision time, then repeat with their creation order reversed. Record the complete 1 ms ball/target state stream and event stream for both runs and require byte-identical results.

The fixture must exercise all of these dispatch handshakes:

- two physical-face candidates are collected, sorted, and solved as one target/time group;
- each grouped ball state is written back exactly once and its collision event is cleared;
- a side or back collider at the same tick follows the generic collision path exactly once;
- a sustained reset contact is marked handled and is not processed again by the generic contact loop;
- triangle-face, generated-edge, and generated-point contacts remain distinct and deterministic;
- reversing ball creation order does not change state transitions or `Hit`, dropped, or raised events.

The analytical tests cover the finite-mass group solve and total candidate ordering. They do not replace this scene-level dispatch check; attach the two trajectory logs and profiler capture to calibration evidence.
