---
uid: developer-guide-accelerometer-input-design
title: Accelerometer Input Design
description: Proposed architecture for adding accelerometer-based nudging to VPE, including Open Pinball Device support and calibration.
---

# Accelerometer Input Design

This page describes the proposed design for adding accelerometer-based nudging to VPE. It covers the intended runtime architecture, where the work should land in the codebase, how Open Pinball Device support should fit into the plan, and how calibration should work for both velocity-capable and legacy-style devices.

## Summary

VPE does not yet have accelerometer or analog nudge support. The recommended design is to add it in three layers:

1. Extend `VisualPinball.Unity.NativeInput` so it can poll analog HID/gamepad/Open Pinball Device inputs instead of only button edges.
2. Add a managed sensor and calibration layer in `VisualPinball.Unity` that turns raw axis samples into calibrated cabinet motion.
3. Feed calibrated cabinet motion into the physics simulation thread and apply it as cabinet velocity deltas before the main physics step.

The key design choice is to make velocity-based nudge the primary internal model. Devices that already report integrated cabinet velocity should map directly into that model. Older devices that only report acceleration can be supported through a compatibility adapter.

## Why velocity should be the primary model

The upstream VPX accelerometer tech note argues that device-integrated velocity produces more stable and more realistic nudging than raw acceleration. The problem with raw acceleration is that the simulator only sees asynchronous USB samples, so it can over-sample, under-sample, or miss peaks entirely. A device that integrates the high-rate sensor stream locally can report the current cabinet velocity directly, which removes most of that resampling error.

For VPE, this suggests a cleaner design than a straight port of the older VPX path:

- Keep cabinet motion in physics state as the current X/Y cabinet velocity.
- On each simulation tick, compute `deltaVelocity = currentCabinetVelocity - previousCabinetVelocity`.
- Apply the opposite of that delta to each moving ball so the playfield remains the coordinate frame.

This matches the physical model described in the VPX technical note while fitting naturally into VPE's simulation-thread architecture.

## Existing integration points in VPE

The work naturally splits across the existing packages:

- `VisualPinball.Unity.NativeInput`
  Polling layer for native user input. It already runs a dedicated high-frequency polling thread, but today it only supports keyboard bindings and still leaves gamepad support as future work.
- `VisualPinball.Unity/Simulation`
  Managed bridge for the native polling library. `NativeInputManager` and `SimulationThread` already carry low-latency input into the simulation thread.
- `VisualPinball.Unity/Game`
  Simulation-thread physics loop. `PhysicsUpdate.Execute()` is the best place to apply cabinet motion to balls before collision simulation.
- `VisualPinball.Unity/VPT/Ball`
  `BallState` already stores per-ball linear velocity, so no structural change is needed to support cabinet-velocity deltas.

## Existing reference points in VPX

VPX already has most of the conceptual pieces, even though VPE has not ported them yet:

- `InputManager` owns plunger and nudge sensors and combines multiple sensor pairs.
- `PhysicsSensor` supports mapping a source axis as position, velocity, or acceleration and inserts compatibility filters as needed.
- `OpenPinDevHandler` reads Open Pinball Device reports containing both acceleration and velocity fields for nudge, as well as plunger position and speed.
- `PhysicsEngine::UpdateNudge()` applies hardware-derived nudge state into the physics step.

VPE should reuse the ideas, but it does not need to copy VPX's exact layering.

## Proposed architecture

### 1. Native analog polling

`VisualPinball.Unity.NativeInput` should be extended from button polling to analog input polling.

The current API is shaped around discrete actions:

- `VpeInputAction`
- `VpeInputBinding`
- `VpeInputEvent`

That works for flipper buttons, but it is too narrow for accelerometers and plungers. VPE should add a second API for analog channels with explicit device and axis identity.

Recommended native additions:

- Analog binding descriptor
  Defines which device and element to watch.
- Analog sample callback
  Returns timestamp, device id, element id, and float value.
- Device enumeration/metadata API
  Lets managed code identify whether a source is a generic HID/gamepad path or an Open Pinball Device path.
- Open Pinball Device fast path
  If a device exposes the OPD report format, decode the named fields directly instead of forcing the user to guess which generic axis is `RX`, `RY`, and so on.

The native layer should support two acquisition modes:

- Open Pinball Device mode
  Preferred when the device exposes named fields such as `vxNudge`, `vyNudge`, `axNudge`, `ayNudge`, `plungerPos`, and `plungerSpeed`.
- Generic HID/gamepad axis mode
  Fallback for older devices that still expose accelerometer or plunger data through joystick-style axes.

### 2. Managed sensor abstraction

On the managed side, VPE should introduce a small sensor abstraction rather than feeding raw analog events straight into physics.

Recommended concepts:

- `AnalogInputSample`
  Timestamped sample from native input, with device id, element id, and value.
- `SensorInputType`
  `Acceleration`, `Velocity`, or `Position`.
- `CalibratedAxisState`
  Tracks bias, filtered value, noise window, and sample history for one axis.
- `NudgeInputSource`
  Represents one X/Y pair for cabinet motion.
- `PlungerInputSource`
  Represents plunger position or speed.
- `NudgeCalibrationProfile`
  Persisted settings for a mapped source.

This is the layer where VPE should:

- subtract bias
- apply orientation/inversion
- apply a noise window or hysteresis filter
- apply gain and clamp
- optionally adapt the center slowly while the cabinet is idle

### 3. Physics-facing nudge state

VPE should keep a dedicated nudge state in the simulation-thread physics context.

Recommended fields:

- `CurrentCabinetVelocity`
- `PreviousCabinetVelocity`
- `CurrentCabinetAcceleration`
  Optional, useful for diagnostics, tilt modeling, or compatibility mode.
- `CurrentCabinetDisplacement`
  Optional, only needed later for visual shake or debugging.

Recommended tick behavior:

1. Read the latest calibrated nudge sample for the current simulation tick.
2. Convert it to the primary internal representation:
   - velocity-capable devices: use directly
   - acceleration-only devices: integrate into a compatibility velocity estimate
3. Compute `deltaVelocity = current - previous`.
4. Apply `-deltaVelocity` to each moving ball before the normal physics step.
5. Store `previous = current`.

This should happen on the simulation thread before `PhysicsCycle.Simulate()` so collision detection and response see the updated ball velocities.

### 4. Compatibility modes

VPE should explicitly support two modes:

- `Velocity`
  Preferred mode. Uses cabinet velocity directly and should not use the old acceleration-oriented nudge filter.
- `Acceleration`
  Compatibility mode for older joystick-style accelerometer devices. VPE can integrate these samples to a cabinet velocity estimate locally.

If a source reports position instead of velocity, VPE can derive velocity in the managed sensor layer, just as VPX does for plunger and nudge compatibility paths.

## Open Pinball Device handling

Open Pinball Device is the best long-term target for VPE because it carries named pinball-specific inputs instead of pretending to be a generic joystick.

For nudge support, OPD is especially useful because it can report:

- raw nudge acceleration
- integrated nudge velocity
- plunger position
- plunger speed

That means VPE should treat OPD as a first-class native input source rather than just another generic HID device.

Recommended OPD behavior in VPE:

- Automatically discover OPD devices in the native layer.
- Surface named channels in managed code instead of raw axis numbers.
- Prefer `vxNudge` and `vyNudge` over `axNudge` and `ayNudge` when both are present.
- Allow generic HID fallback for devices that do not implement OPD.

VPE should still support mixed setups. For example:

- plunger from OPD
- nudge from a joystick-style accelerometer
- buttons from keyboard or another controller

## Calibration design

Calibration should be split into separate responsibilities rather than treated as a single setting.

The initial calibration step must be available outside the Unity editor. A future standalone player app will need to guide the user through at least the "cabinet is at rest, sample now" flow, so calibration logic should live in runtime-capable code with an editor UI layered on top, rather than being embedded only in editor inspectors.

### 1. Orientation setup

This is the structural setup step:

- which physical axis maps to cabinet X
- which physical axis maps to cabinet Y
- invert X or Y
- optional rotation angle for boards mounted at 90, 180, or arbitrary angles

This should be stored per input mapping.

### 2. Manual zero calibration

VPE should provide a calibration action equivalent to "read data now, I'm not moving."

Recommended behavior:

- user presses `Calibrate at Rest`
- VPE samples for a short fixed window, such as `0.5-2.0` seconds
- VPE computes the mean resting value for each axis
- that mean becomes the bias offset to subtract from future samples

This is required for generic HID/gamepad accelerometer sources and should also be available as a fallback for OPD devices.

This flow should be designed as a reusable runtime service:

- callable from editor UI
- callable from a future player app or in-game settings UI
- able to return progress, sample counts, and success or failure state

The editor and player-facing UIs can then share the same calibration implementation while presenting different workflows.

### 3. Noise measurement and dead-zone setup

Bias removal is not enough on its own because consumer accelerometers still jitter around zero.

VPE should measure quiet-time noise during calibration and derive an initial noise window from it. Instead of a hard dead zone by default, the preferred behavior is a small hysteresis or jitter window because it avoids sudden jumps when the signal crosses the threshold.

Recommended stored values:

- `NoiseSigma` or another robust noise estimate
- `DeadZone` or `HysteresisWindow`
- optional `Clamp`

Suggested behavior:

- velocity-capable OPD sources: prefer a small hysteresis window
- generic acceleration sources: allow a slightly stronger dead zone or hysteresis filter

### 4. Idle-time drift correction

VPE should not rely only on a one-time calibration. Sensor centers drift over time because of temperature, cabinet settling, and mounting flex.

Recommended behavior:

- while the signal is inside a stillness window for some sustained interval, slowly adapt the stored zero point toward the live mean
- freeze that adaptation immediately when real motion starts

This gives VPE both:

- manual baseline calibration
- slow automatic recentering during idle periods

For OPD velocity sources, this should be optional and conservative because the device may already be doing its own drift correction. For generic HID acceleration sources, VPE should enable it by default.

### 5. Gain and clamp

After centering and noise handling, VPE still needs:

- X gain
- Y gain
- optional maximum output clamp

These should stay explicit user settings. They control feel rather than sensor correctness.

## Configuration model

Recommended persisted settings for each nudge mapping:

- input backend
  `OpenPinDev` or `GenericHid`
- source X id
- source Y id
- input type
  `Velocity` or `Acceleration`
- orientation angle
- invert X
- invert Y
- bias X
- bias Y
- idle recenter enabled
- idle recenter rate
- dead zone or hysteresis window
- X gain
- Y gain
- clamp

If multiple nudge sources are allowed, VPE should average compatible active sources only when there is a clear reason to do so. Otherwise it should prefer one configured source pair.

## Editor and player UX split

The calibration system should be implemented in two layers:

- runtime calibration service
  Owns sample collection, bias estimation, noise estimation, stillness detection, and profile persistence
- host-specific UI
  Editor inspectors, setup wizards, or a future player app can call into the runtime service

Recommended responsibility split:

- Unity editor
  Full mapping UI, orientation setup, advanced gain and filter tuning, diagnostics
- future player app
  Initial device discovery, initial rest calibration, simple gain adjustment, recalibration when cabinet conditions change

This keeps setup possible for end users who never open the Unity project while still giving developers richer tooling inside the editor.

## Recommended rollout

### Milestone 1: Velocity path on Windows

- extend `VisualPinball.Unity.NativeInput` with analog polling
- add managed analog sample handling in `VisualPinball.Unity`
- support one nudge X/Y source pair
- add calibration profile storage
- apply cabinet velocity deltas in the simulation thread

This is the highest-value slice because it proves the architecture and covers modern velocity-capable devices.

### Milestone 2: Generic acceleration compatibility

- add acceleration-mode source type
- integrate acceleration into cabinet velocity in managed code
- add calibration UI for bias and dead zone or hysteresis
- document when to disable acceleration-style filtering for real velocity inputs

### Milestone 3: OPD-first experience and tilt polish

- add OPD device discovery and named channel mapping
- add plunger position and speed through the same analog stack
- add diagnostics UI
- add optional plumb tilt and visual cabinet displacement built on the same nudge state

## Validation checklist

The implementation should be considered correct when the following hold true:

- repeated USB polls of the same velocity sample do not amplify a nudge
- missed USB polls do not weaken a velocity-based nudge beyond the actual cabinet motion represented by the latest sample
- the table remains stable at rest, with no gradual drift in ball motion
- left/right and front/back nudges move the ball in the expected directions after orientation setup
- idle-time recentering corrects slow drift without fighting real nudges
- velocity-based devices do not require the old acceleration-style nudge filter to feel stable

## Open questions

- Should VPE expose advanced calibration controls in both the editor and a future player app, or reserve the player app for initial setup and simple recalibration?
- Should OPD support live entirely in `VisualPinball.Unity.NativeInput`, or should VPE also keep a managed fallback parser for environments where a raw HID path is easier to ship?
- Should keyboard nudges be rewritten later to use the same cabinet-motion state so that all nudge sources share one physics path?

The current recommendation is yes for the last question: VPE should eventually have one cabinet-motion model for keyboard, script, and hardware nudging, even if hardware support is implemented first.
