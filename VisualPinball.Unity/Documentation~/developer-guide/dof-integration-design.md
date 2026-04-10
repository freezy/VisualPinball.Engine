---
uid: developer-guide-dof-integration-design
title: DOF Integration Design
description: Proposed architecture for integrating DOF into VPE with a Windows-first path and a later hybrid cross-platform backend model.
---

# DOF Integration Design

This page describes the proposed design for integrating the DirectOutput Framework (DOF) into VPE. It covers the recommended runtime architecture, where the work should land in the codebase, how a future player app should own configuration, and how a later cross-platform backend should fit into the same design.

## Summary

VPE should integrate DOF in two steps:

1. Add a Windows-first, compatibility-first backend that bundles upstream `DirectOutput` with the VPE player app and hosts it through a thin VPE-owned adapter.
2. Keep the VPE-facing feedback API backend-neutral from the start, then add a second backend based on `libdof` for cross-platform support and eventual parity testing on Windows.

The key design choice is to keep DOF itself out of the Unity gameplay core. VPE should publish switch, coil, lamp, and GI state through a neutral feedback service, while the player app owns backend selection, cabinet configuration, diagnostics, and output testing.

## Why the first step should target Windows

Compatibility with existing DOF behavior matters slightly more than immediate cross-platform reach.

Classic `DirectOutput` is still the most compatibility-oriented runtime, but it is effectively Windows-only today. It is also designed around desktop .NET and Windows-era hardware assumptions, so VPE should not bake it directly into the shared runtime layer.

`libdof` is the right long-term portability direction, and VPX already uses it for the new plugin-based DOF integration, but it is still a port with some compatibility gaps compared with classic DOF. That makes it a better second step than a v1 default.

This leads to a hybrid plan:

- Windows player builds use bundled `DirectOutput` first.
- The shared VPE feedback contract stays cross-platform.
- A later `libdof` backend can be added without changing table content or game-logic integration.

## Existing integration points in VPE

The current VPE runtime already has the seams needed for a feedback host:

- `IGamelogicEngine` publishes switches, coils, lamps, GI, and displays.
- `Player` owns the game-logic engine, runtime lifecycle, and table mapping state.
- `CoilPlayer` and `LampPlayer` already maintain live output state for mapped playfield devices.
- `PinMameGamelogicEngine` already keeps shared coil, lamp, and GI state snapshots for low-latency playback.
- `MappingConfig` already represents the machine-facing identity of switches, coils, lamps, and wires.

What VPE does not yet have is:

- a backend-neutral feedback API
- a player-owned feedback coordinator
- a standalone cabinet configuration and diagnostics UX
- a portable backend boundary for `DirectOutput` and `libdof`

## Proposed architecture

### 1. Add a backend-neutral feedback service

VPE should add a runtime-facing feedback service in `VisualPinball.Unity` that is independent of any specific DOF implementation.

Recommended concepts:

- `IFeedbackHost`
  The lifecycle and capability surface exposed to the runtime.
- `FeedbackSessionDescriptor`
  Table path, table id, ROM id, game-logic engine name, display name, and other metadata used when starting a feedback session.
- `FeedbackSnapshot`
  The current normalized switch, coil, lamp, and GI state.
- `FeedbackEvent`
  Optional delta form for backends that need immediate edge-triggered updates.
- `FeedbackBackendKind`
  `None`, `DirectOutput`, or `LibDof`.
- `FeedbackHostStatus`
  Health, initialization state, detected devices, backend version, and last error.

Recommended `IFeedbackHost` shape:

- `Initialize(profile)`
- `StartSession(sessionDescriptor)`
- `PublishSnapshot(snapshot)`
- `PublishEvent(feedbackEvent)`
- `StopSession()`
- `GetStatus()`
- `RunOutputTest(testDescriptor)`

The important rule is that VPE publishes machine outputs, not DOF-specific toy instructions. The backend remains responsible for translating those outputs into hardware behavior according to its own config.

### 2. Add a player-owned feedback coordinator

The future player app should own a `FeedbackCoordinator` that sits above the Unity table runtime and below the selected backend.

Responsibilities:

- choose a backend for the current platform
- own backend startup and shutdown
- subscribe to VPE output state
- aggregate high-frequency output changes into a single state cache
- publish snapshots at a fixed host cadence
- expose logs, health, and device detection to the player UI
- reset outputs safely on table exit, reload, or backend failure

Cadence rules:

- do not call DOF directly from the 1000 Hz simulation loop
- publish snapshots at a stable host rate such as `60-120 Hz`
- allow urgent coil-edge delivery as optional immediate deltas
- keep backend calls off any latency-sensitive gameplay path

### 3. Publish output state from existing VPE seams

VPE should use the output state it already has instead of introducing a second gameplay or cabinet model.

Recommended publication sources:

- switches from `SwitchPlayer` and `IGamelogicEngine.OnSwitchChanged`
- coils from `CoilPlayer`, `IGamelogicEngine.OnCoilChanged`, and shared PinMAME coil state
- lamps and GI from `LampPlayer`, `IGamelogicEngine.OnLampChanged`, `OnLampsChanged`, and shared PinMAME lamp and GI state
- table metadata from the loaded package plus player-selected overrides

Recommended publication behavior:

1. Maintain one in-memory `FeedbackSnapshot` cache in the coordinator.
2. Update that cache from engine and player events.
3. Publish full snapshots on a fixed cadence.
4. Optionally send immediate deltas for pulse-sensitive coil edges.
5. Send a full baseline snapshot when a session starts.
6. Send a backend-native reset or an all-off state when a session stops.

### 4. Keep table mappings and cabinet config separate

`MappingConfig` should remain table-side mapping data. It should not grow DOF toy names, output controller assignments, or cabinet wiring.

Recommended split:

- `MappingConfig`
  Table-side identity and playfield device mapping only.
- `FeedbackProfile`
  Player-owned settings such as backend choice, enabled flag, config root, logging level, and optional ROM or table overrides.
- backend-native config
  The actual `DirectOutput` or `libdof` configuration files and generated artifacts.

This keeps authored table content portable and prevents one cabinet's hardware layout from leaking into reusable packages.

## Windows-first implementation

### Runtime model

The Windows-first backend should bundle upstream `DirectOutput` with the VPE player app and host it through a thin `WindowsDirectOutputHost`.

Recommended boundaries:

- upstream `DirectOutput` stays as close to unmodified as possible
- VPE integrates it as a pinned git submodule
- the player app owns config editing, output testing, and diagnostics
- the Unity runtime talks only to `IFeedbackHost`

This allows VPE to reuse the most compatible DOF implementation without requiring users to install DOF separately.

### Configuration model

The player app should fully own DOF setup.

Required player features:

- enable or disable DOF
- choose the active backend
- choose, create, import, or reset the config root
- edit global config and per-table or per-ROM overrides
- detect available controllers and surface backend health
- run output tests without launching a table
- expose backend logs and validation errors

Compatibility-first rule:

- use the real DOF config files as the canonical storage
- do not invent a separate VPE-only DOF format
- store only a small VPE-owned profile around backend selection and install paths

### Upstream changes

No upstream DOF changes are strictly required for the first working Windows integration if VPE bundles the runtime and calls the lower-level setup APIs directly.

Additive upstream changes that would still be useful later:

- explicit host and config-path options
- structured logging callbacks
- a cleaner headless packaging split between runtime and tools

These should remain optional and must not change current DOF behavior for existing users.

### Acceptance criteria

The Windows-first path is complete when:

- the VPE player can start DOF without any separate installation
- a bundled config root can be created from scratch or imported from an existing setup
- PinMAME tables can drive coils, lamps, and GI through DOF
- outputs reset safely on table exit, reload, or crash
- the player app can run output tests and expose actionable diagnostics

## Hybrid second step with libdof

The second step is to add a `LibDofFeedbackHost` that implements the same `IFeedbackHost` interface.

Recommended selection policy:

- Windows defaults to `DirectOutput` until parity is proven
- Linux and macOS use `libdof` when available
- unsupported platforms use `NullFeedbackHost`
- Windows can later expose `libdof` as an advanced backend option for comparison and validation

Cross-platform rules:

- the shared feedback API must stay free of Windows-only types
- the player-owned settings stay backend-neutral except for backend-specific option bags
- health, logs, and device detection flow through `FeedbackHostStatus`
- table content and `MappingConfig` remain unchanged regardless of backend

The `libdof` step should be treated as an explicit parity project, not as a drop-in assumption. It needs platform-by-platform controller validation and side-by-side comparison with the Windows `DirectOutput` path on real tables.

## Recommended rollout

### Milestone 1: Shared feedback contract

- add `IFeedbackHost`, `FeedbackSnapshot`, `FeedbackSessionDescriptor`, and `FeedbackHostStatus`
- add a runtime-owned state cache and publication path
- add `NullFeedbackHost`

### Milestone 2: Windows-first DirectOutput backend

- bundle `DirectOutput` with the player app
- add `WindowsDirectOutputHost`
- wire player-owned configuration, logs, and output testing
- validate PinMAME-driven coils, lamps, and GI

### Milestone 3: Hybrid backend support

- add `LibDofFeedbackHost`
- keep Windows on `DirectOutput` by default
- validate Linux and macOS controller support
- add parity testing and optional Windows backend selection

## Validation checklist

The implementation should be considered correct when:

- feedback publication never blocks the simulation thread
- switching tables repeatedly does not leave outputs active
- missing or malformed config files produce actionable diagnostics
- the player app can test outputs without entering gameplay
- Windows `DirectOutput` remains the compatibility reference backend
- `libdof` can be added later without changing the shared runtime contract

## Open questions

- Should the future player app expose all backend-specific advanced settings directly, or keep the UI focused on the most common cabinet workflows and leave advanced file edits to external tools?
- Should VPE publish only snapshots to the backend, or preserve a mixed model with snapshots plus immediate coil-edge deltas for pulse-sensitive toys?
- Once `libdof` is integrated, should Windows keep `DirectOutput` as the long-term default, or only until parity is measured across the most common hardware configurations?

The current recommendation is to answer yes to the second question. VPE should keep snapshots as the primary synchronization model, but it should preserve an immediate path for short-lived coil pulses that would be easy to miss at a pure fixed publish cadence.
