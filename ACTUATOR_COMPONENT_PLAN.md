# Generic coil actuator implementation plan

## Purpose

Add a game-agnostic `ActuatorComponent` that turns one mapped gamelogic coil into a deterministic normalized mechanical position, plus reusable `ActuatorTransformComponent` followers that apply that position to any number of independently configured Unity objects. This covers solenoid-driven ramps, bridges, diverters, doors, gates, toys, and linked mechanisms without introducing table-specific code.

## Evidence driving the design

Godzilla LE 1.15.0's service-menu Single Coil Test was exercised twice in Kiki for Bridge driver 13 (`9-DR-3`). Each press emitted one SPIKE Node-9 long-driver command at raw level 64 for 120 ms, followed by a release; there was no repeated host-visible PWM edge train. VPE gamelogic engines normalize that raw level before dispatch, so the actuator receives approximately `64 / 255 = 0.251`. The bridge therefore needs edge-triggered binary motion, not `0.251` as a bridge angle. Other machines can expose sustained, repeated, or normalized duty-cycle values, so the component must still distinguish binary edge modes from an explicitly selected analog-follow mode.

## Scope

- Provide one coil destination named `actuator_coil` through VPE's normal coil mapping and wiring APIs.
- Convert the coil signal into a normalized position in `[0, 1]` using authored timing, curves, and one of four reusable input modes.
- Publish position through `IAnimationValueEmitter<float>` so several independent followers can subscribe to the same actuator.
- Move and rotate each follower around its authored local origin while preserving a single transform writer per GameObject.
- Package authored values and emitter references into `.vpe` files.
- Expose a concrete runtime API for scripts to query position, command either endpoint, toggle the target, or set an immediate normalized position.
- Document correct collider and hierarchy setup and add focused tests for state transitions, PWM-like input, reversal, initial pose, follower mapping, and packaging.

## Explicit non-goals

- The actuator will not infer mechanical travel from raw electrical strength in binary modes.
- The actuator will not animate scale because changing collider scale is not a safe rigid kinematic transformation.
- The actuator will not discover, configure, enable, disable, or change the mode of colliders; collision setup remains an explicit table-authoring responsibility.
- The actuator will not own route scoring, switches, balls, or game-specific mechanism states.
- Ball-impact compliance is not folded into the actuator follower. A bridge that wiggles after collapse should use a separate child compliance pivot beneath the actuator-driven pivot, so commanded travel and contact response never compete for the same transform. A future generic impact-spring component can drive that child from collision data without changing this API.
- Existing specialized components such as flippers, magnets, turntables, and gate lifters are not migrated in this change.

## Runtime architecture

### `ActuatorComponent`

`ActuatorComponent` is a `MonoBehaviour`, `ICoilDeviceComponent`, `IAnimationValueProvider<float>`, and `IPackable`. `IAnimationValueProvider<T>` extends `IAnimationValueEmitter<T>` with a read-only current value so a newly enabled follower can synchronize without waiting for another event. The actuator owns authoring data, the normalized mechanical state, and the animation event. It initializes its motion state and registers an `ActuatorApi` with the nearest `Player` during `Awake`, advances its state from `Update` using `Time.deltaTime`, and publishes only changed positions. There is no forced `Start` event.

The component exposes the following authored fields:

- `CoilMode`: `FollowCoil`, `ToggleOnPulse`, `OneShot`, or `FollowValue`.
- `InitialPosition`: normalized startup pose, allowing either endpoint or a deliberately intermediate authored state.
- `ActivationDuration` and `ReleaseDuration`: full-stroke travel time in seconds; partial travel scales by remaining distance so reversals retain a consistent physical speed.
- `ActivationCurve` and `ReleaseCurve`: independently authored easing for travel toward 1 and 0.
- `ReleaseDelay`: continuous inactive time required before a binary input is considered released; short zero gaps are filtered and cannot retrigger a toggle.
- `ActivationThreshold`: normalized duty-cycle threshold for binary modes; repeated values above it remain one activation. The value is clamped below 1 because PinMAME can report ordinary on/off outputs as approximately `1 / 255` after its global modulated-solenoid latch is active.
- `OneShotHoldDuration`: dwell at position 1 before automatic return in `OneShot` mode.

The current `Position`, current `Target`, and committed input state are runtime-owned and never serialized as authoring data. `Position` returns the clamped `InitialPosition` even before the component's `Awake`, which makes initialization independent of Unity's cross-object callback order. `OnValidate` clamps durations and normalized values without changing a live mechanism pose.

### Defaults

| Field | Default | Rationale |
|---|---:|---|
| `CoilMode` | `FollowCoil` | Least surprising held-solenoid behavior. |
| `InitialPosition` | `0` | Authored transform is the zero pose. |
| `ActivationDuration` | `0.3 s` | Matches the existing generic event-transform convention. |
| `ReleaseDuration` | `0.3 s` | Symmetric safe default, independently authorable. |
| `ActivationCurve` | ease-in-out 0→1 | Smooth full-stroke travel. |
| `ReleaseCurve` | ease-in-out 0→1 | Smooth full-stroke return. |
| `ReleaseDelay` | `0.05 s` | Filters short inactive PWM gaps while rearming ordinary pulses promptly. |
| `ActivationThreshold` | `0.001` | Accepts PinMAME's possible `1 / 255` plain-output value while rejecting exact zero. |
| `OneShotHoldDuration` | `0.5 s` | Visible but conservative default dwell. |

### Input modes

- `FollowCoil`: a qualified rising edge moves toward 1 and a continuously inactive signal lasting `ReleaseDelay` moves toward 0. This models held solenoids, motors, and ordinary energized/de-energized diverters.
- `ToggleOnPulse`: each qualified rising edge swaps the target endpoint; release only rearms the next edge. This models bistable or mechanically toggled devices such as a bridge driven by short pulses.
- `OneShot`: a qualified rising edge moves toward 1, holds for `OneShotHoldDuration` after reaching the endpoint, then returns to 0 independently of coil release. A later qualified edge can restart it cleanly.
- `FollowValue`: the clamped normalized coil value becomes the target and is interpolated using the directional timing. This is opt-in for genuinely proportional mechanisms and is never selected automatically from a numeric coil level.

### Edge qualification and PWM behavior

`ActuatorApi` implements `IApiCoil` directly instead of wrapping `DeviceCoil`. This is required because every `DeviceCoil` implements `ISimulationThreadCoil`; one simulation-thread dispatch can latch a value-only `DeviceCoil` into simulation mode and permanently suppress its main-thread value callback. The dedicated endpoint is not an `ISimulationThreadCoil`, so actuator input always remains on the Unity main thread. It implements `OnCoil(bool)`, normalized `OnCoil(float)`, `OnChange(bool)`, and `CoilStatusChanged`; raising the status event preserves coil-sound integration.

`ActuatorComponent` owns the committed binary state: the first normalized sample above `ActivationThreshold` creates one rising edge, further nonzero samples do nothing, zero starts the release-delay timer, and a new nonzero sample before the delay expires cancels that pending release. Only a completed release rearms `ToggleOnPulse` and `OneShot` for another rising edge. A held input in `OneShot` produces exactly one outbound/return cycle and must be released before another rising edge can retrigger it.

This makes Godzilla's normalized 120 ms level-64 bridge command one pulse and prevents short off gaps in a PWM-like stream from repeatedly toggling a mechanism. `FollowValue` requires a plain coil mapping: VPE's wire path is boolean-only, and a dynamic wire on a coil mapping also loses the proportional value. `ActuatorInspector` documents that restriction, and the runtime logs it once if `OnChange(bool)` feeds an actuator configured for `FollowValue`.

### Motion and interruption

Every target change begins from the exact current position, records the destination, direction-specific curve, and duration, and scales the full-stroke duration by absolute remaining distance. A reverse command during travel therefore remains spatially continuous and preserves the configured full-stroke-time relationship; a non-linear curve may still ease again and visibly slow at the reversal. Inspector edits affect the next transition, while an in-flight transition keeps its recorded duration.

For `0 < t < 1`, motion is `position = clamp01(lerp(from, to, curve.Evaluate(t)))`. At `t >= 1`, the state snaps exactly to `to` even if the curve's last key is not 1. Null or keyless curves fall back to a linear 0→1 curve at runtime and are repaired by `OnValidate`; intentional curve overshoot is flattened by the final position clamp to protect moving colliders.

Zero-duration movement and explicit snap APIs emit one final value without NaN, but they are physics teleports: VPE may move collision through a ball without imparting a corresponding velocity. They are supported for restore, initialization, and diagnostics, not for ordinary gameplay travel.

### `ActuatorApi`

`ActuatorApi` implements `IApi`, internal `IApiCoilDevice`, public `IApiWireDeviceDest`, and the dedicated `IApiCoil` endpoint. `IApiCoilDevice.Coil` is implemented explicitly with a private validated helper because that device interface is internal. Its concrete public control surface is:

- `IApiWireDest Wire(string deviceItem)` for normal wire mappings, with coil lookup remaining on the internal device interface.
- `float Position`, `float TargetPosition`, and `bool IsMoving` for observation.
- `bool IsActive { get; set; }` for normal endpoint control using authored travel.
- `void Toggle()` for script-driven bistable mechanisms using authored travel.
- `void SnapTo(float position)` for immediate restore or diagnostics, explicitly carrying teleport semantics in its name.
- `event EventHandler Reached` when an authored transition reaches its destination.

All actuator callbacks remain on the Unity main thread because state publication ultimately drives Unity transforms. Kinematic collider motion is handed to VPE through the existing transform synchronization path. `IAnimationValueEmitter<float>.UpdateAnimationValue` is implemented explicitly and routes through the same private snap/publish method so external engine calls cannot desynchronize the exposed position from the motion state.

## Reusable transform followers

`ActuatorTransformComponent` derives from `AnimationComponent<float>` and implements `IPackable`. `AnimationComponent<T>.Awake`, `OnEnable`, and `OnDisable` become protected virtual lifecycle hooks, and its resolved emitter becomes protected, allowing a correct derived initialization without hiding Unity messages. Authors attach one follower to every independently moving pivot and assign the same `ActuatorComponent` as emitter. Each follower calls the base `Awake`, captures its authored local position and rotation as the zero pose, then immediately pulls `IAnimationValueProvider<float>.AnimationValue` and applies it before `PhysicsEngine.Start` builds colliders. `OnEnable` subscribes through the base and pulls the current value again, so late re-enabling cannot leave a stale pose.

Each follower applies:

- an optional local `PositionOffset`, and
- an optional local `RotationOffset` expressed as Euler authoring values but interpolated as a quaternion.

The follower has its own `ResponseCurve` and `Reverse` flag. This allows one actuator value to rotate linked left and right bridge halves in opposite directions, translate a plunger, and move decorative linkage with distinct geometric ratios. Null or keyless response curves use linear fallback. Children of a driven pivot inherit its motion naturally and need no follower of their own.

The follower intentionally uses local space. Local pivots survive table placement, packaging, and parent transforms predictably, whereas authored world-space endpoints become invalid when a packaged table is placed or scaled. Position uses `Vector3.LerpUnclamped`; rotation uses `Quaternion.SlerpUnclamped`; the response input is clamped before evaluation.

## Physics and hierarchy contract

VPE colliders that move with an actuator must be active and configured as kinematic on their collider component when the table loads. Kinematic registration is built during `PhysicsEngine.Awake`; activating an initially inactive collider or toggling its kinematic flag later does not add it to that scan. The follower moves the main object's transform before collider construction for the initial pose and during `Update` for gameplay; VPE's existing `IKinematicTransformComponent` path then polls the updated local-to-playfield matrix and derives linear/angular velocity for ball interaction. Script execution order can make the collider observe a follower update on the next scan, but the existing continuity window classifies that as continuous movement. The actuator does not disable collision merely because an object is moving.

Recommended bridge hierarchy:

```text
BridgeActuator (ActuatorComponent; mapped once to driver 13)
├── LeftDrivenPivot (ActuatorTransformComponent; +hinge rotation)
│   └── LeftCompliancePivot (future impact spring)
│       └── Left bridge render and kinematic collider
└── RightDrivenPivot (ActuatorTransformComponent; -hinge rotation)
    └── RightCompliancePivot (future impact spring)
        └── Right bridge render and kinematic collider
```

Decorative towers that are physically mounted to a bridge half belong below that half's moving pivots. The fixed frame, solenoid bracket, and hinge supports remain outside them. A collider and its render geometry must share the same moving branch; otherwise the visible bridge and ball route diverge. Zero-duration travel, `SnapTo`, and large pose jumps are not gameplay-safe ways to push balls because VPE deliberately treats them as teleports.

## Packaging

`ActuatorPackable` stores the mode, initial position, directional durations and curves, release delay, threshold, and one-shot dwell. `ActuatorTransformPackable` stores the enabled position/rotation channels, `PackableFloat3` offsets, response curve, and reverse flag; `PackableFloat3` avoids Newtonsoft serializing Unity `Vector3` convenience properties as noise. `ActuatorTransformReferencesPackable` stores the selected animation emitter with `PackagedRefs` only when its type has a registered `[PackAs]` name, otherwise it writes a null reference with one clear warning. Unpack resolves with `Resolve<MonoBehaviour, IAnimationValueEmitter<float>>`; a null reference deliberately leaves `_emitter` null so the base parent search remains available.

The unique pack names are `Actuator` and `ActuatorTransform`; duplicate pack names would fail package registration. `Pack()` always returns bytes and never null, because null suppresses component creation. New Unity `.meta` files are committed with the implementation. No generated package artifacts are committed. This is the first follower derived from `AnimationComponent<T>` to package its emitter reference, so reference round-trip tests are mandatory.

## Inspector behavior

`ActuatorInspector` groups coil interpretation, travel, and initial-state fields and shows only the one-shot dwell when `OneShot` is selected. It explains that binary numeric levels are strength, not position, and that `ReleaseDelay` filters short inactive gaps.

`ActuatorTransformInspector` exposes the emitter, position and rotation toggles, conditional offsets, response curve, and reverse flag. It warns when neither channel is enabled. Its `OnValidate` override remains editor-guarded to match the base signature. Documentation tells authors to set moving VPE colliders active and kinematic at load and to place the GameObject origin at the physical hinge or linkage pivot.

`ActuatorInspector` also provides a non-serialized edit-mode `Preview Position` slider. Preview records each connected follower's authored local position and rotation, applies the follower's normal offsets, curve, and reverse flag directly in the editor, and restores the recorded pose on reset, inspector disable, scene save, undo/redo, script reload, editor quit, or entry into Play Mode. It includes inactive scene followers but excludes persistent prefab assets, and it reproduces the runtime's explicit-emitter and nearest-compatible-parent resolution without firing a coil or mutating actuator runtime state.

## Tests

- A single nonzero sample followed by repeated nonzero values creates one toggle only.
- Short zero gaps below `ReleaseDelay` do not rearm a toggle, while a sustained zero does.
- Godzilla's normalized `64 / 255` 120 ms shape is treated as one binary activation and does not set position to `0.251`.
- `FollowCoil` moves both directions and respects release delay.
- `OneShot` travels, dwells, and returns; a legitimate later pulse can retrigger it.
- `FollowValue` clamps values and follows proportional targets only when explicitly selected.
- Mid-travel reversal begins at the current pose without a discontinuity and scales duration by remaining distance.
- Zero-duration transitions snap once without NaN or repeated notifications, with no gameplay impulse promised.
- `InitialPosition = 1` produces the endpoint follower pose during `Awake`, before the first frame and collider construction.
- A pending delayed release cancelled by a new nonzero input never emits a false release or rearming edge.
- Null and keyless actuator and follower curves use a linear fallback.
- `OnValidate` clamping does not move a live mechanism.
- A transform follower maps 0, 0.5, and 1 to the expected local position and quaternion rotation, including reverse mode.
- Two followers subscribed to one emitter receive the same scalar while retaining independent offsets and response curves.
- Component and reference packables round-trip all authored fields, complete curve keys/tangents, and the emitter reference.

## Documentation and migration

Add `VisualPinball.Unity/Documentation~/creators-guide/manual/mechanisms/actuators.md` and a matching entry in `VisualPinball.Unity/Documentation~/creators-guide/toc.yml` with setup, coil modes, multi-object examples, pivot/collider rules, and the Godzilla bridge pattern. Keep `EventTransformComponent` unchanged for compatibility; document `ActuatorComponent` as the supported choice for new coil-driven mechanisms. A later migration can obsolete `EventTransformComponent` after existing tables have a conversion path. Visual-scripting nodes and the separate impact-compliance component remain explicit follow-up work.

Implementation files live under `VisualPinball.Unity/VisualPinball.Unity/VPT/Actuator/`, editor files under `VisualPinball.Unity/VisualPinball.Unity.Editor/VPT/Actuator/`, and tests under `VisualPinball.Unity/VisualPinball.Unity.Test/VPT/Actuator/`. Component attributes are `[PackAs("Actuator")]`, `[AddComponentMenu("Pinball/Mechs/Actuator")]`, `[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/actuators.html")]`, `[PackAs("ActuatorTransform")]`, and `[AddComponentMenu("Pinball/Animation/Actuator Transform")]`. Authoring permits only one `ActuatorComponent` per GameObject to avoid shared `UnityObjectId` registration.

## Delivery sequence

1. Add enums and a small deterministic actuator motion state whose `Advance(float dt)` never reads `Time.deltaTime` and can be tested without a running table.
2. Make `AnimationComponent<T>` lifecycle hooks virtual/protected, expose the resolved emitter to derived classes, and add the read-only `IAnimationValueProvider<T>` contract.
3. Add `ActuatorComponent`, its direct `IApiCoil`/device API, and packables.
4. Add `ActuatorTransformComponent`, guarded reference packing, and inspectors.
5. Add edit-mode tests for state, initialization, transforms, curve fallback, and packaging.
6. Add creator documentation and TOC entry.
7. Refresh Unity once for the new scripts, compile the affected assemblies, run focused tests, inspect the complete diff, and commit only the actuator feature, required shared animation-base change, documentation, tests, `.meta` files, and this plan.
