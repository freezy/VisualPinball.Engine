---
uid: plunger_input_system
title: Plunger Input
description: How analog plunger samples travel from native input to the plunger physics.
---

# Plunger Input

The analog plunger is a port of Visual Pinball's `PlungerSensor` and its use in `HitPlunger`. The filter and the physics run on the physics thread, the mapping runs where the samples arrive.

## Units

- **Position units (p.u.)**: what a sensor reports after its mapping. 0 at rest, +1 fully retracted, forward of rest negative down to −1/3.
- **Travel units (t.u.)**: the table plunger's own range. 0 fully forward, 1 fully retracted, the park position in between. `PlungerInputState.GetPosition(rest)` converts with `saturate(lerp(rest, 1, pu))`, and a speed in p.u./s becomes t.u./s by multiplying with `1 − rest`.

## Flow

1. `PlungerSensorSystem` subscribes to `NativeInputManager.AxisInputReceived`. For every axis event matching a sensor's position or velocity mapping, it applies `SensorMapping.ProcessRawValue` and queues a sample with its native timestamp into `PhysicsEngine`.
2. Before each physics batch, `PhysicsEngineThreading.ProcessPendingPlungerSensorSamples` stages the queued samples into `PhysicsEnv.Plunger` (`PlungerInputState`), mapping host time onto the simulation clock. The first sample of a segment lands at the end of the batch; the newest sample is kept between 0 and 40 ms behind it.
3. Every 1 ms physics step calls `PlungerInputState.StepOneMillisecond`. Each sensor (`PlungerSensorState`) feeds the samples due by then into a Kalman filter (`PlungerKalmanFilter`, a port of `PlungerKalmanFilter.h`), keeps a 100 ms apex history and derives the hit velocity of a release.
4. `PlungerVelocityPhysics.UpdateVelocities` reads the active source. Mech plungers follow it with the spring model, or launch at the hit velocity when it is released. The collider uses that hit velocity for the impulse on the ball.

The Unity Input System's analog plunger action takes one extra sensor slot after the configured ones, and only feeds it while native input isn't polling.

## Sensor rules

- A sensor becomes active once it leaves rest (|p.u.| ≥ 0.01) and drops back to inactive ten seconds after it last moved. Inactive sensors read as "at rest", so a disconnected or idle plunger never holds the table plunger back.
- Joystick axes send events on change only. Without a new sample for 10 ms, the last one repeats.
- The filter restarts from the next sample, keeping the activation, when the clocks can't be trusted: physics skipped steps, the staging buffer overflowed, two staged samples are more than 100 ms apart on the host clock, or the native gameplay gate changed (focus or pause). Otherwise, a plunger moved while samples were gated would read as a release.

## Auto plungers

On an *Auto Plunger* with *Mech Plunger*, a release faster than 1 m/s starts a 101-step timer instead of moving the plunger. The physics owner publishes a press counter and whether a timer runs (`PhysicsEngine.GetAutoPlungerLaunchState`). `AutoPlungerLaunchTracker` turns that into press, release or pulse edges of a synthetic *Launch Ball* hold. The simulation thread owns it while native input polls, `Player.Update` otherwise. `InputActionHoldAggregator` combines it with the physical button, so releasing one doesn't release the other.

## Settings

`CabinetInputSettings.plunger` (`CabinetPlungerSettings`) holds the retract preference and up to four sensors with serialized `SensorMapping` strings. `SimulationThreadComponent.ApplyPlungerSettings` pushes them into the physics engine. `PlungerApi.DoRetract` falls back to `PhysicsEngine.PlungerRetract` unless a script sets it.

`PlungerSensorSetup` recognizes devices and creates their default mappings. `PlungerCalibrationMath` solves the mapping's limit and scale for a two-point calibration through the whole `ProcessRawValue` chain (center, dead zone, clamp, scale), and `PlungerSensorCalibration` collects the samples for it.

## Testing without a table

`PlungerProbe` runs one sensor through the same filter and velocity physics as the table, with a default plunger and a simplified ball. The player's Plunger tab uses it. `Enqueue` and `ResetClock` are thread-safe, `Step` and `Snapshot` belong to the owner.
