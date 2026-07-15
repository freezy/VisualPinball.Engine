---
uid: developer-guide-dmd-studio-readiness
title: DMD Studio Readiness Decisions
description: Frozen contracts and prototype results for the first DMD Studio implementation.
---

# DMD Studio readiness decisions

This page records the Phase 0A decisions from the DMD Studio implementation plan. They are the implementation contract for Phases 0 through 5. The prototype code used to reach these decisions was discarded; only the documented outcomes and test evidence remain.

## Asset and color model

The v1 asset and color model in section 3 of the plan is frozen with these amendments:

- `DmdFontAsset` includes a `Notes` string for source, license, and attribution details required by the starter font pack.
- `DmdKerningPair` is a serializable value with `LeftCodepoint`, `RightCodepoint`, and `Adjustment` integer fields.
- `DmdCueParameter` is a serializable value with `Name`, `Type`, and the same tagged default-value union as `DmdParamValue`: `IntValue`, `FloatValue`, `StringValue`, and `BoolValue`.
- Direct ScriptableObject references, ScriptableObject inventory lists, and the polymorphic cue-layer list remain Unity-serialized fields. They receive Newtonsoft `JsonIgnore` attributes so the generic package serializer cannot recursively inline object graphs or attempt to deserialize abstract layers. The future DMD packer owns explicit reference IDs and layer DTOs.
- Packaged reference identity uses `UnityObjectId.Get`, matching the existing package system, rather than raw `GetInstanceID` values.
- Mono assets store I8 intensity and RGB assets store literal RGB24 values exactly as specified. Indexed color remains out of scope.

V1 font import is BMFont text plus PNG only. A TTF/OTF baker is deferred because Unity 6000.5 does not expose the required `FontEngine` glyph-to-texture rasterization calls as public API. Private reflection is not an acceptable production dependency.

## Scheduler scenario sign-off

The admission table needs no row changes after walking these scenarios:

1. An attract/base cue is configured before `Start`; game-start status or mode cues enter according to priority, and natural completion drains back to held work or the base cue.
2. Repeated multiball jackpots use a non-empty coalesce key. Replays merge parameters into the active, held, or queued instance and return its existing handle; higher-priority mode work still follows the queue/preemption table.
3. A tilt cue that must bypass a non-interruptible mode intro must use `System` priority. A `Critical` tilt intentionally queues behind a non-interruptible active cue.

`DmdCuePlayer` stays main-thread-only, with caller marshaling as the boundary. An internal command queue was rejected because `Play` must return a synchronous handle and admission/coalescing order is part of the API contract. In particular, an `IGamelogicInputThreading` implementation using `GamelogicInputDispatchMode.SimulationThread` must marshal DMD calls to Unity's main thread.

## Display bridge behavior

The current colorization source accepts Dmd2 and Dmd4 frames and silently ignores other formats, including Dmd8 and Dmd24. Phase 3 will inspect `DisplayFrameData.Format` on the main thread in `DmdBridgePlayer.HandleDisplayUpdateFrame`, before `_pipeline.Push`. When an unsupported format reaches a colorizing pipeline, the bridge latches passthrough mode for that display, logs one warning, and force-rebuilds the pipeline. Detection or rebuilding must not occur in `ColorizableVpeGleSource.Push`: that method runs on the pipeline worker, and disposing the pipeline there would join the current thread. `DmdPipeline.Matches` currently ignores converter identity, so this rebuild must either use the existing force path or extend matching to include converter identity.

The passthrough latch survives settings-driven `EnsurePipeline(force: true)` rebuilds and format-preference requests; it resets only when the selected display topology changes. Mono input continues through colorization unchanged. `ColorizableVpeGleSource` still logs one warning for any independently ignored unsupported format as a defensive diagnostic.

A delayed display re-announcement can rebuild a bridge pipeline that subscribed after the initial announcement. Avoiding in-scene flicker requires both of these safeguards:

- `DisplayPlayer` suppresses a duplicate `DisplayConfig` before calling `Clear` or resizing the component.
- `DotMatrixDisplayComponent.UpdateDimensions` returns early for unchanged dimensions and flip state, and releases replaced texture/mesh resources when a real resize occurs.

Arbitrary dimensions work in-scene only when a matching `DisplayComponent` existed when `DisplayPlayer.Awake` discovered displays. The delayed re-announcement supplies arbitrary dimensions to a late bridge subscriber; it does not dynamically create an in-scene display component.

The native DMD bridge binds exactly one display. With an explicit target it selects that ID; without one it selects the first announced ID beginning with `dmd`, so multi-project tables must configure the intended hardware target instead of relying on announcement order. Additional displays still work through the in-scene `DisplayPlayer` path.

The bridge checks are pinned to `VisualPinball.Engine.DMD` `origin/master` and `VisualPinball.Engine.Player.Hdrp` local `master`, both on the feature branch. Unit and EditMode tests use the Unity 6000.5 package test project under `VisualPinball.Unity.Test/TestProject~`. The authoring project's `master` is still on Unity 6000.2, so the Phase 3 sample-scene host must be upgraded or replaced by a clean Unity 6000.5 host before that scene is committed. Before running the Player host, its local package manifest must be repointed from the main engine checkout to the feature worktree (or the main checkout must be switched after its unrelated changes are resolved).

Phase 0A validated the bridge behavior by tracing the complete main-thread and worker-thread source paths rather than committing or retaining a throwaway scene. Runtime proof remains mandatory in Phase 3 E2E-2 and E2E-3.

## Packaging design

A Unity 6000.5 EditMode spike round-tripped project to cue to sprite through editor and runtime unpack paths and produced the same FNV render hash after unpack. The result was one passing test in 0.976 seconds.

The selected full-delivery design is:

- Replace type-erased use of `IPacker<T>` with a non-generic ScriptableObject packer adapter/base. The existing `PackerFactory.GetPacker(Type)` cast is not variance-safe; the same defect is reproducible with `SoundAssetPacker`.
- Add a non-generic post-load fix-up contract and invoke it only after all assets have been instantiated and registered in both editor and runtime unpack paths. Package folder visitation order is not a reference-ordering contract.
- Store DMD graph references as `MetaPackable.InstanceId` values obtained through `UnityObjectId`. Discovery starts at the game-logic root and recursively adds referenced assets with existing ID-based deduplication.
- Serialize cue layers through explicit DTOs with a versioned type discriminator. Do not ask the generic JSON packer to reconstruct the abstract `SerializeReference` list.
- Use the Visual Scripting envelope `{ magic: "VPE.DMD.VS", version: 1, graphPayload: byte[], dmdProjectRef: int }`. The magic is checked before envelope deserialization. Missing or unknown magic means the entire payload is a legacy graph payload; an explicit discriminator is necessary because permissive JSON deserialization may otherwise produce default values instead of failing.

Full packaging and the Visual Scripting envelope remain Phase 6 work. The spike only freezes their compatible field shapes and loading strategy.

## Import prototypes

A 2x2 PNG with distinct top and bottom rows proved that Unity's first decoded row is the bottom row. Importers must explicitly flip rows when converting top-origin PNG or BMFont coordinates into DMD bitmap data.

The public `UnityEngine.TextCore.LowLevel.FontEngine` surface compiles for face loading and glyph lookup, but its glyph-to-texture rasterization entry points are inaccessible in Unity 6000.5. Phase 5 therefore keeps glyph touch-up and the licensed starter font pack but removes the in-editor TTF/OTF bake feature.

## Public API contract

All public `DmdCuePlayer` calls are main-thread-only. Editor and development builds assert the constructing thread; production code remains deliberately lock-free.

- Null constructor dependencies throw `ArgumentNullException`. Constructor-time project validation records diagnostics for later one-time publication rather than throwing for malformed authored content.
- `DmdParams.Set` rejects null, empty, or invalid names and more than 256 distinct bound parameters with `ArgumentException`. `Play` and `UpdateCue` apply the same bound-parameter cap.
- Unknown cue IDs and invalid/excluded assets are authored-content failures: `Play` returns an invalid handle, boolean operations return `false`, and `SetBase` leaves the current base unchanged. Each distinct failure emits one validation diagnostic rather than throwing.
- `Start` is idempotent. Calls before `Start` may configure the base, admission state, parameters, and preferred format, but no sink method is called until `Start`. `Tick` before `Start` is a no-op.
- `Tick` rejects NaN, infinity, and negative time with `ArgumentOutOfRangeException`. A time value earlier than the previous tick resets the accumulator origin without rewinding logical cue state or emitting duplicate lifecycle events.
- `Dispose` is idempotent, clears the announced display at most once, drops buffers and subscriptions, and does not raise `OnCueFinished` during teardown. Every method other than `Dispose` throws `ObjectDisposedException` after disposal.
- Stale handles and unmatched string targets return `false`. Stop/update never throw merely because an instance ended. Existing `OnCueFinished` timing remains as specified in section 6.4 of the plan.

Utility APIs validate null inputs, dimensions, formats, and destination buffer sizes at their public boundary with standard argument exceptions. Authored asset-shape failures continue through `DmdValidation` diagnostics so malformed content cannot escape from the renderer as an indexing exception.

## Phase 2 compositor clarifications

The Phase 1 review exposed three edge cases that must be deterministic before cue-layer wiring:

- `ScrollOff` moves the outgoing surface over black and reveals the incoming surface only at completion. `Uncover` continues to move the outgoing surface while revealing the live incoming surface beneath it; the two transitions are intentionally distinct.
- `Opaque` ignores source per-pixel alpha, but the layer's global opacity still fades the complete overwrite. Full layer opacity remains an exact `dst = src` copy, including black or transparent source texels.
- A mask covers the canvas with transparent black outside its bitmap rectangle. Applying an offset or undersized mask therefore clears pixels outside the mask bounds instead of leaving them unchanged.
