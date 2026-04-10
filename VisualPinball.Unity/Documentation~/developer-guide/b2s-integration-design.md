---
uid: developer-guide-b2s-integration-design
title: B2S Integration Design
description: Proposed long-term architecture for integrating B2S into VPE by modernizing the upstream runtime into a cross-platform core, keeping a Windows COM shim for compatibility, and exposing native second-monitor and Unity texture outputs.
---

# B2S Integration Design

This page describes the proposed design for integrating B2S into VPE. It covers how `.directb2s` assets should be loaded and rendered, how a future player app should drive a native second-monitor backglass window, which third-party libraries should be used, how the same renderer should also output a texture for VR or cabinet-style backglass meshes inside Unity, and why modernizing the upstream B2S runtime is the stronger long-term solution than building a separate VPE-native rewrite.

## Summary

VPE should integrate B2S by helping evolve the existing [B2S Backglass Server](https://github.com/vpinball/b2s-backglass) runtime into a host-agnostic, cross-platform managed core, then consume that core from VPE through a dedicated wrapper layer.

The recommended long-term architecture is:

1. extract or modernize the upstream B2S runtime into a cross-platform `b2s-core`
2. keep a thin Windows-only COM compatibility shim for `B2S.Server`
3. add a cross-platform desktop host for native second-monitor rendering
4. add a VPE-specific wrapper that consumes the same core directly and uploads frames into Unity textures when needed

This is a better long-term solution than a VPE-native rewrite for three reasons:

- it keeps B2S semantics anchored to the existing runtime instead of re-discovering them in a parallel implementation
- it gives the B2S maintainer and VPE a shared upgrade path instead of creating permanent drift between two runtimes
- it still gives VPE the outputs it needs: a native second-monitor backglass window and a Unity texture path for VR or in-world cabinet views

Recommended library choices for the modernized runtime:

- `SkiaSharp`
  Shared offscreen 2D compositor, image decoding, dirty-region rendering, and frame export.
- `Avalonia`
  Cross-platform desktop host for second-monitor output, monitor enumeration, and future player app UX around B2S.
- built-in `System.Xml.Linq`
  `.directb2s` parsing. No extra XML library is needed.
- Unity `Texture2D`
  Runtime upload target for the VR and in-world backglass path. Do not use a `RenderTexture` as the primary B2S surface because that implies a camera-driven render path.

## Why this is the better long-term solution

The first instinct for VPE is to build a dedicated B2S runtime that is shaped exactly around Unity and the future player app. That sounds attractive architecturally, but it is a worse long-term maintenance tradeoff.

A VPE-native rewrite would still need to solve all of the hard problems:

- `.directb2s` parsing and resource decoding
- backglass illumination behavior
- score reels, LEDs, and score displays
- animation behavior
- native second-window output
- Unity texture output
- compatibility validation across real backglasses

And after doing all of that, VPE would still own a second implementation forever. Every upstream B2S fix, behavioral quirk, or compatibility improvement would need to be reinterpreted and ported again.

By contrast, modernizing the upstream runtime keeps the semantic center of gravity in one place:

- the B2S maintainer can continue to evolve the canonical runtime
- VPE gets a clean managed API and modern outputs without forking the behavior model
- Windows compatibility through COM can remain available without infecting the shared core

That is why this page recommends a modernization and extraction effort, not a greenfield rewrite.

## What has to change in upstream B2S

The current runtime is not portable as-is because it is still tightly coupled to Windows-specific technologies such as WinForms, `System.Drawing`, `user32.dll`, COM activation, and registry-based runtime coordination.

The good news is that the runtime is output-oriented and relatively light on interactive UI. That makes modernization much more realistic than for a full desktop application with deep user workflow dependencies.

The work should be framed as "extract and re-host the runtime", not "keep patching the current Windows host until it compiles elsewhere".

Recommended target split:

- `b2s-core`
  Cross-platform runtime containing `.directb2s` parsing, resource decoding, state model, animation logic, score and reel behavior, and a host-neutral rendering API.
- `b2s-rendering`
  Shared `SkiaSharp` compositor that produces BGRA32 frames and owns dirty-region logic.
- `b2s-host-desktop`
  Cross-platform desktop host using `Avalonia` for monitor enumeration and second-window output.
- `b2s-com-windows`
  Windows-only COM wrapper that exposes `B2S.Server` for compatibility and forwards calls into the new core.
- `vpe-b2s`
  VPE-specific wrapper or integration project that mounts B2S as a Git submodule and talks to the modern managed API directly, bypassing COM entirely.

This split gives the B2S project a cleaner product architecture and gives VPE a stable integration boundary.

## Existing integration points in VPE

VPE already has the important runtime seams:

- `DisplayPlayer` connects `IGamelogicEngine` display events to texture-backed display components.
- `DotMatrixDisplayComponent` and `SegmentDisplayComponent` already update textures from raw display frame data.
- `DisplayComponent` already renders a texture onto a mesh in Unity.
- `ScoreReelDisplayComponent` and `ScoreReelComponent` show that VPE already has EM-style score concepts in Unity.
- `Player`, `LampPlayer`, `CoilPlayer`, and `PinMameGamelogicEngine` already centralize switch, lamp, GI, coil, and display state that a B2S renderer would need.

What VPE does not yet have is:

- a B2S wrapper package or module
- a native second-window host wired into the future player app
- a Unity texture bridge for B2S frames
- player-owned backglass configuration UX

That is a much smaller gap than writing a whole new B2S runtime from scratch.

## Proposed architecture

### 1. Modernize upstream B2S into a shared core

The first phase should happen in or alongside the upstream B2S repository.

Goals:

- retarget the runtime to modern .NET
- separate runtime logic from WinForms forms and custom controls
- replace `System.Drawing` and control painting with a host-neutral compositor
- replace registry-driven runtime coordination with in-memory state and explicit host APIs
- keep Windows COM as an adapter around the new runtime rather than as the core programming model

The important principle is that the runtime logic should stop depending on forms, screens, or the registry.

### 2. Add a managed runtime API

The shared core should expose a small managed host API that VPE and a desktop host can both consume.

Recommended types:

- `B2SSceneDefinition`
  Static background art, grill art, image snippets, bulbs, reel definitions, score areas, DMD or segment cutouts, animation metadata, and layout bounds.
- `B2SSceneState`
  Current values for lamps, solenoids, GI, mechs, scores, player-up, tilt, ball-in-play, and named animation state.
- `B2SDisplayState`
  Current DMD and segment frame textures or raw frame buffers mapped by display id.
- `IB2SRuntime`
  Runtime host API for loading scenes, updating state, advancing animations, and producing frames.
- `IB2SController`
  High-level API for hosts that want to drive B2S state directly through familiar backglass concepts.

Recommended `IB2SController` shape:

- `SetData(id, value)`
- `PulseData(id)`
- `SetScore(player, value)`
- `SetCredits(value)`
- `SetBallInPlay(value)`
- `StartAnimation(name)`
- `StopAnimation(name)`
- `SetDisplayFrame(id, frame)`

This is the API VPE should use. The COM wrapper should translate `B2S.Server` calls into this same core API on Windows.

### 3. Add a shared offscreen compositor

The heart of the integration should be a `SkiaSharp`-based compositor that belongs to the shared B2S runtime, not to VPE.

Responsibilities:

- rasterize the static backglass art
- apply illuminated bulb and overlay layers
- render score reels, LEDs, and text regions
- composite DMD and segment-display inputs into cutout regions
- advance time-based animations
- track dirty rectangles so only changed regions are redrawn when possible
- export the final frame as BGRA32 pixel data

Recommended output model:

- one BGRA32 pixel buffer per rendered B2S frame
- one logical frame size per backglass scene
- one shared render scheduler that can run at `30 Hz` when mostly static and up to `60 Hz` while animations or DMD changes are active

This compositor should be the only place where B2S drawing rules live.

### 4. Keep COM, but only as a compatibility shell

Backwards compatibility still matters for the existing B2S ecosystem. The modernization path should preserve that by keeping a Windows-only COM wrapper.

Important constraints:

- COM should not be part of the shared runtime
- COM should forward into the managed runtime API
- COM should stay Windows-only
- VPE should never talk to COM directly

This gives the B2S project a compatibility bridge without forcing VPE or cross-platform hosts to inherit old Windows host assumptions.

### 5. Add a dedicated VPE integration project

VPE should add a small integration project that mounts the modernized B2S repository as a Git module and exposes VPE-specific wrappers.

Responsibilities of `vpe-b2s`:

- resolve `.directb2s` assets for the active table
- map PinMAME, MPF, or original-game state into `IB2SController`
- own the Unity texture upload path
- coordinate with the future player app for second-monitor hosting and per-table settings

This keeps the B2S runtime independent and reusable while still giving VPE a thin product-specific layer.

## Third-party libraries

### `SkiaSharp`

Use `SkiaSharp` as the shared offscreen B2S compositor.

Why it is the best fit:

- mature cross-platform 2D raster API
- good image decoding support for the embedded backglass art path
- easy offscreen rendering into a shared pixel buffer
- reusable for both native windows and Unity textures

This should replace `System.Drawing` and WinForms control painting in the runtime path.

### `Avalonia`

Use `Avalonia` for the desktop host and future player app UI around monitor selection, B2S enablement, and backglass diagnostics.

Why it is the best fit:

- cross-platform from the start
- first-class multi-window desktop UI
- good long-term fit if the future player app also wants to be cross-platform
- avoids building the backglass window around Windows-only UI frameworks

`Avalonia` should be the window host, not the only renderer. Composition rules should stay in the shared `SkiaSharp` renderer so the same runtime also feeds Unity.

### Built-in XML APIs

Use `System.Xml.Linq` and the framework base64 APIs for `.directb2s` parsing and resource decoding.

No additional XML stack is needed for v1.

### Libraries not recommended for the core path

- `WPF` or `WinUI 3`
  Acceptable Windows-only UI stacks, but the wrong core abstraction if the goal is a shared cross-platform runtime.
- `System.Drawing`
  Not suitable as the long-term renderer for a modern cross-platform runtime.
- a second Unity camera or multi-display-only path
  Explicitly not the right solution for B2S because it adds a camera-driven render pipeline for what is fundamentally a 2D composition problem.

## Second-monitor rendering

### Window host design

The second-monitor path should be owned by a `B2SMonitorHost` running in the future player app, or in a sidecar desktop host if the Unity runtime remains the main process.

Responsibilities:

- enumerate monitors
- persist the chosen backglass monitor id
- create a borderless window on the chosen monitor
- size the window to the monitor bounds or work area
- receive compositor frames and display them with minimal latency
- recover cleanly if the selected monitor disappears

Recommended window behavior:

- borderless and non-resizable by default
- optional topmost mode for cabinet setups
- one backglass window per active table
- recreate or reposition the window when monitor configuration changes

### Rendering path

The desktop host should not implement B2S drawing rules itself. It should display the latest compositor bitmap produced by the shared runtime.

Recommended frame flow:

1. `IB2SRuntime` renders through the shared `SkiaSharp` compositor into a BGRA32 buffer.
2. `B2SMonitorHost` copies or maps that buffer into an Avalonia `WriteableBitmap`.
3. The Avalonia window displays that bitmap through a lightweight image control.
4. Dirty-region redraw is handled by the compositor, not by a second scene graph.

This keeps the host simple and ensures the Unity and native-window paths always look the same.

### Data inputs for second-monitor mode

The compositor should combine:

- static `.directb2s` art and score regions
- lamp, solenoid, GI, and mech state from PinMAME or other game-logic engines
- DMD and segment display frames from existing VPE display sources
- direct `IB2SController` calls for original and EM-style tables

## VR and in-world backglass texture rendering

### Texture output design

The VR path should use the same shared B2S compositor output and upload it into a Unity `Texture2D`.

Recommended types:

- `B2SUnityTextureOutput`
  Receives the compositor frame buffer and uploads it into Unity.
- `B2SBackglassTextureComponent`
  Applies the resulting texture to the backglass mesh or material in the scene.

Recommended runtime behavior:

- allocate one `Texture2D` matching the compositor output size
- upload BGRA or RGBA pixel data when a new frame is ready
- update only on the Unity main thread
- bind the texture to the cabinet backglass material
- use an emissive or unlit material setup in HDRP so the backglass remains readable in VR

### VR priority and realism note

VR is not the primary target for B2S. The expected usage is that the native second-monitor path serves the vast majority of users, while VR only needs a compatible fallback that works reliably.

For that reason, the first VR implementation should simply embed the fully composited 2D B2S frame into the 3D backglass as a texture. It does not need to recreate the more realistic "light behind the translite" workflow used by hand-authored VPE backglasses.

This is an intentional compromise:

- `.directb2s` contains enough information to render a convincing 2D backglass
- `.directb2s` does not contain enough physical authoring data to reconstruct a truly realistic 3D backbox automatically
- using the raw B2S art and overlays as a texture in VR is acceptable because VR is a fallback use case, not the main product target

If later needed, VPE can add a premium VR-specific path that maps B2S state onto authored Unity backglass assets and real light emitters, but that is explicitly out of scope for the first B2S integration.

### Why this should use `Texture2D`, not `RenderTexture`

`RenderTexture` is primarily useful when a camera or GPU pass is producing the image. B2S is not camera-driven in this architecture. It is a CPU-composited 2D surface.

For that reason, the primary output should be a `Texture2D`. If a later HDRP effect or material workflow wants a `RenderTexture`, VPE can blit the `Texture2D` into one, but that should be an optional adapter, not the main rendering path.

## State integration with PinMAME, MPF, and original games

### PinMAME

PinMAME is the highest-value first integration path.

The B2S adapter should consume:

- lamp changes
- GI changes
- coil and solenoid changes
- mech updates where the backglass expects them
- DMD and segment display frames already emitted through `IGamelogicEngine`

This should be wired through a `B2SStatePublisher` that translates existing VPE state into `B2SSceneState` and `B2SDisplayState`.

### MPF

MPF should use the same `B2SStatePublisher` contract once it exposes equivalent lamp, segment, score, and display information.

### Original games and EM-style logic

Original games should drive B2S through `IB2SController`, not through COM emulation.

This gives VPE a clean path for:

- player-up indicators
- credits and ball-in-play
- reels and score windows
- scripted illumination and animation triggers

## Resource resolution

VPE should resolve B2S assets in this order:

1. explicit backglass asset selected by the player app
2. packaged B2S asset embedded with the table package
3. loose sidecar `.directb2s` file next to the loaded table source or package

The player app should also allow per-table overrides so cabinet owners can select alternative backglasses without changing authored content.

## Scope boundaries

V1 should include:

- a modernized shared B2S runtime core
- `.directb2s` parsing and resource decoding
- static art and illuminated overlays
- score reels and basic score displays
- DMD and segment composition into backglass cutouts
- native second-monitor window output
- Unity texture output for VR and cabinet-room use
- a Windows COM shim for compatibility with existing B2S consumers
- a VPE wrapper project that uses the managed API directly

V1 should not include:

- porting the B2S editor and tools
- preserving WinForms or registry-driven runtime behavior internally
- a Unity multi-display camera-based implementation
- a physically reconstructed VR backbox generated automatically from `.directb2s`
- every legacy quirk before the shared runtime is stable

Advanced legacy behavior can be added after the shared runtime, COM wrapper, and VPE integration layer are stable.

## Recommended rollout

### Milestone 1: Extract the shared runtime

- identify and isolate parser, state, animation, score, and reel logic from the current Windows host
- retarget the runtime to modern .NET
- define `B2SSceneDefinition`, `B2SSceneState`, `B2SDisplayState`, `IB2SRuntime`, and `IB2SController`
- replace registry-driven runtime communication with explicit host APIs

### Milestone 2: Replace the renderer

- add a `SkiaSharp` compositor for static art, bulbs, score regions, and display slots
- replace `System.Drawing` and WinForms control painting in the runtime path
- establish a shared BGRA32 frame contract

### Milestone 3: Restore desktop hosting and compatibility

- add `B2SMonitorHost` using Avalonia
- add monitor selection and persistence
- add a Windows COM shim that forwards `B2S.Server` calls into the new runtime
- validate second-monitor rendering on Windows first

### Milestone 4: Add VPE integration

- add the dedicated VPE wrapper project
- map VPE DMD and segment display outputs into `B2SDisplayState`
- publish PinMAME lamp, GI, coil, and mech state into `IB2SController`
- validate ROM-driven tables in the player app

### Milestone 5: Add Unity texture output

- add `B2SUnityTextureOutput`
- bind the compositor output to a backglass mesh material
- validate HDRP material setup in VR and cabinet-room scenes
- keep VR as a compatibility path using the 2D composed frame, not as a physically reconstructed lighting system

## Implementation estimate

For the modernization path described on this page, the work is meaningful but still quite realistic, especially because the runtime is output-oriented and does not need its interactive editor or tools ported as part of v1.

Estimated effort for a practical first version:

- runtime extraction and project split: `3-5` implementation days
- modern managed host API and state model: `2-4` implementation days
- `SkiaSharp` compositor for static art, bulbs, score regions, and display slots: `4-7` implementation days
- Avalonia second-monitor host and monitor selection plumbing: `3-5` implementation days
- Windows COM compatibility shim over the new runtime: `2-4` implementation days
- VPE wrapper project, PinMAME state publication, and display-slot wiring: `3-5` implementation days
- Unity texture output and material hookup: `2-3` implementation days
- tests, failure handling, and documentation polish: `4-6` implementation days

That puts a practical v1 at roughly:

- `2-5 engineer-weeks` of work in conventional terms
- about `1-3 calendar weeks` for a strong AI coding agent working iteratively with human review, assuming the scope stays at the v1 boundaries defined on this page

What would push the estimate up:

- broad compatibility with legacy B2S quirks
- hidden coupling between runtime logic and the current Windows host
- large numbers of unusual score or animation behaviors
- support for legacy plugins or sound behaviors in the first pass

If the goal changes from "practical first version" to "high confidence compatibility with a wide range of legacy backglasses," the effort rises to something closer to:

- `4-8 engineer-weeks`
- about `2-4 calendar weeks` for an AI-agent-driven implementation with review and repeated validation

## Why this should appeal to the B2S maintainer

This plan is not asking the B2S maintainer to accept a VPE fork or a competing runtime. It is proposing a modernization that improves the upstream project itself.

Benefits to upstream B2S:

- cleaner project boundaries between runtime, host, and compatibility layers
- a path away from WinForms and `System.Drawing` in the runtime
- preserved Windows compatibility through a COM shim instead of through old host assumptions everywhere
- a reusable cross-platform runtime that can serve more than one host
- direct adoption in VPE without fragmenting the behavior model

Benefits to VPE:

- one canonical B2S runtime to integrate against
- better long-term compatibility with upstream fixes and behavior improvements
- a native second-monitor solution and Unity texture output without inventing a second implementation

That alignment is why this modernization path is the better long-term solution.

## Validation checklist

The implementation should be considered correct when:

- the same B2S scene renders identically in the second-monitor window and on the Unity backglass texture
- rendering B2S does not require a second Unity camera or a second 3D scene
- the native backglass window can be assigned to a chosen monitor and survives table reloads
- DMD and segment displays composite into the correct B2S regions
- VR backglass presentation stays readable without introducing frame spikes from texture uploads
- the Windows COM layer can still satisfy compatibility expectations for `B2S.Server`
- missing or malformed `.directb2s` assets fail gracefully and do not break gameplay

## Open questions

- Should the first modernization phase happen directly in the upstream repository, or in a temporary companion repository that is merged back once the architecture settles?
- How much of the current `B2S.Server` surface should the Windows COM shim preserve in v1?
- Should the desktop host live in-process with the future VPE player app, or start as a sidecar process that later gets embedded without changing the runtime API?
- Should the first VPE import path package `.directb2s` assets into the table, or should v1 focus on loose sidecar files and add packaging later?

The current recommendation is:

- modernize upstream B2S into a shared runtime
- keep COM as a compatibility adapter
- let VPE consume the managed API directly
- treat second-monitor output as the primary product target
- treat VR as a functional texture-based fallback, not a premium physical backglass solution
