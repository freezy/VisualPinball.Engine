---
uid: developer-guide-packaging-benchmarks
title: Packaging Benchmarks
description: Benchmarks, validated optimization results, and packaging experiments around .vpe loading.
---

# Packaging Benchmarks

This page is a record of the optimization work around `.vpe` load time and package size. It is here for two reasons: to make the validated wins easy to keep, and to stop future work from re-running dead-end experiments without context.

## Summary

The table below is the short version.

| Experiment | Status | Recommendation | Main result |
| --- | --- | --- | --- |
| Cooked GPU textures (BC7/DXT5, mips baked, no GLB images) | Validated and merged (June 2026) | Shipping format | Terminator 2 import: ~23.3s → ~1.7-2.2s in-editor |
| Packed `textures.bin` sidecar | Validated and merged | Keep | Better package shape for runtime loading |
| GPU HDRP normal repack | Validated and merged | Keep | Removed a major normal-repack hotspot |
| Skip mip generation for heavy linear side textures | Validated and merged | Keep | Small but measurable runtime win |
| PNG side-channel textures | Validated | Baseline only | Correct but decode-heavy |
| `DXT5` linear + `BC7` sRGB side textures in `textures.bin` | Tested and validated as a benchmark | Recommended next optimization target | Best size/load tradeoff found |
| Raw `RGBA32` side textures | Tested and invalidated | Do not use as shipping format | Very slow export, regression risk |
| Move GLB normals to sidecar | Tested and invalidated | Do not use without deeper investigation | Smaller/faster, but caused ghosting |
| DDS/BC7 embedded directly in GLB | Tested and invalidated | Do not use | Smaller package, much slower load |
| KTX2 / `KHR_texture_basisu` for GLB normals | Tested and invalidated for startup speed | Do not prioritize for load time | Smaller package, slower load |
| Persistent raw runtime texture cache | Tested and invalidated | Do not revive in the same form | Speedups came with corruption/crash risk |

## Cooked GPU textures (shipping since June 2026)

This implemented the "highest-value next step" below and went further: instead of compressing
only the side-channel textures, **every** texture captured by the material translator is cooked
at export into its final GPU format (BC7 for color/masks, DXT5 in HDRP AG packing for normals,
mips baked) and the GLB ships without image payloads for captured materials. Runtime upload is
`LoadRawTextureData` from the packed blob through a pinned pointer.

Measured on Terminator 2 (editor play mode, full keyboard flow from the table carousel):

- baseline (PNG everywhere, 486 MB package): **23.3 s** total import
  - `table.glb` (345.8 MB, 336 MB of PNGs): 10.5 s
  - material restore: 10.9 s (3.0 s sidecar PNG decode, 4.7 s CPU normal repack + compress)
- cooked format (689 MB package, `table.glb` 9.8 MB, `textures.bin` 640 MB raw BC): **1.7-2.2 s**
  - `table.glb`: 0.4-0.7 s, material restore: 0.6-0.8 s (325 raw uploads ≈ 0.2 s)

Supporting runtime work that landed with it: uninterrupted glTFast defer agent, worker-thread
prefetch of `textures.bin`/`materials.v1.json` and of all item/ref entries, worker-thread GLB
extension/tangent scans, a cached `PackagedRefs` type scan (was 450 ms per load), time-budget
yields, and a ~160 MB-per-frame upload budget (without it, queueing all uploads in one frame
crashed the editor with `DXGI_ERROR_DEVICE_HUNG`).

The package grows (BC + mips beat PNG at load time, not on disk); stored — not deflated — zip
entries for the texture blob and GLBs keep reads straight. Caveats and details live in the
Packaging README ("Cooked Texture Format").

## Baseline context

The optimization work started from a package that had two different classes of problems: too much texture data was effectively being paid for twice, and the runtime was doing expensive work that was easy to miss until proper timing logs were added.

In practical terms, the baseline package:

- carried far too much texture data across GLB and sidecar paths
- spent large amounts of runtime in material reconstruction and normal handling
- loaded the benchmark table in roughly the `10-11s` range

After the merged fixes, the stable baseline moved down substantially, which made it much easier to see where the remaining bottlenecks actually were.

## Merged and validated improvements

### 1. Remove duplicate texture storage

The export path now keeps opaque GLB textures on the glTF path and only side-channels the texture classes that actually need it.

Result:

- package size dropped materially from the original `200+ MB` range
- the remaining size largely represented real payload, not double-stored content

### 2. Pack side textures into `textures.bin`

The old one-file-per-texture side path was replaced by one packed blob plus metadata offsets.

Result:

- better package structure
- lower per-entry ZIP overhead
- better foundation for later compression work

This was not the biggest speed win by itself, but it changed the shape of the format in an important way. Once the side textures lived in one blob, experiments around compression and direct upload became much easier to reason about.

### 3. Cache resolved materials during import

`VpeMaterialV1Reader` now reuses already-resolved materials instead of rebuilding equivalent runtime materials repeatedly.

Result:

- measurable reduction in material-restore time
- especially helpful on tables with many repeated imported materials

### 4. GPU normal repack for HDRP

This was the most important merged optimization.

Instead of repacking RGB normals for HDRP on the CPU, `HdrpMaterialResolver` uses `VpePackNormalForHdrp.shader` to do the channel conversion on the GPU.

Representative outcome:

- total import dropped from roughly `10s` toward the `7-8s` range
- the normal repack hotspot dropped from multiple seconds to effectively negligible

### 5. Skip runtime mip generation for heavy linear side textures

Linear side textures, especially mask/thickness payloads, no longer generate mips at runtime.

Result:

- small but real improvement
- low risk for the relevant texture classes

## Best unmerged result

The best benchmark result came from compressing side-channel textures into GPU-native formats while leaving GLB textures on the normal glTF path.

Format split:

- `DXT5` for linear side textures
- `BC7` for sRGB side textures

Representative result on the benchmark table:

- package around `142.5 MB`
- total import around `6.23s`
- side-texture load time around `80 ms`
- `compressedTextureLoads=97`
- `encodedTextureLoads=0`

Why it worked:

- it avoided PNG decode for VPE-owned textures
- it preserved the stable GLB semantics
- it aligned the side-channel path with direct GPU upload

Why it is not merged:

- desktop-oriented GPU format choice
- needs a fallback strategy for unsupported platforms
- needs a clear editor re-import story

If someone wants one benchmark result to keep in their head, this is the one. It was the strongest combination of smaller package, faster load, and correct visuals.

## Invalidated experiments

### Raw `RGBA32` side textures

Intent:

- eliminate decode entirely by storing raw pixels

Observed behavior:

- package around `178 MB`
- export time ballooned to about `3 minutes`
- visual regressions were observed

Conclusion:

- useful as a proof that decode avoidance matters
- not suitable as a shipping format

### Move GLB normals to sidecar

Intent:

- remove the heaviest remaining GLB image class
- reuse the side-channel path for normals

Observed behavior:

- package could shrink dramatically, down near `119 MB`
- visual regressions returned
- the main symptom was insert/plastic ghosting

Conclusion:

- do not revive this path without first understanding why imported GLB normals preserve behavior that the sidecar path did not

### DDS / BC7 inside GLB

Intent:

- store precompressed GPU blocks directly inside GLB

Observed behavior:

- package shrank to roughly `116.8 MB`
- `table.glb` import became far slower
- total import climbed to roughly `11.3s`

Conclusion:

- smaller file on disk
- much worse uncached load time in this container format

### KTX2 / `KHR_texture_basisu` for GLB normals

Intent:

- reduce GLB size while keeping payload compressed in a standard way

Observed behavior:

- package around `130.6 MB`
- GLB really contained KTX2 normals
- runtime was slower than the PNG/JPG GLB baseline

Conclusion:

- useful if package footprint is the goal
- not the right optimization if startup time is the goal

### Persistent runtime texture cache

Intent:

- get close to editor-like warm-load behavior

Observed behavior:

- promising speedups
- but corrupted textures and second-run crashes appeared

Conclusion:

- caching has potential
- the tested implementation was not safe

## glTFast-related findings

Several experiments were constrained by glTFast:

- export remains fundamentally PNG/JPG-oriented
- KTX2 import exists, but export is not a supported stock path
- custom GLB material extension export is easier as a local rewrite than as a glTFast-native feature
- smaller GLB image payloads do not automatically mean faster import

These findings are why sidecar compression outperformed the GLB-focused experiments.

## Recommended next targets

### Highest-value next step

Re-implement compressed `textures.bin` with platform-aware fallbacks:

- `DXT5` for linear side textures
- `BC7` for sRGB side textures
- explicit format discriminator in `VpeTextureAssetV1`
- runtime direct upload path instead of `LoadImage(...)`

This is the strongest measured path that did not break visuals.

### Promising, but secondary

- overlap IO/decode/setup work more aggressively once the sidecar format is stable
- revisit persistent caching with a safer cache contract

### Low-priority or not recommended

- GLB-normal relocation without deeper semantic investigation
- DDS-in-GLB for load time
- KTX2-in-GLB as a startup-speed optimization
- raw `RGBA32` side textures as a shipping format
