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
| Source textures + player-side GPU cook & cache | Validated and merged (June 2026) | Shipping architecture | T2: first load ~9.5s, cached ~1.1s; package lossless at 477 MB |
| Cooked GPU textures embedded in the package | Superseded | Replaced by the player-side cook | Proved the BC7/no-decode path (~1.7-2.2s) but broke losslessness and grew packages to 689 MB |
| Packed `textures.bin` sidecar | Superseded | Don't reintroduce; use the source layer | Improved package shape, but the lossless per-file `table/textures/` source layer replaced it entirely |
| GPU HDRP normal repack (resolver) | Superseded by the cook | Keep cook-side GPU repack | The resolver's GPU repack shader was removed; the GPU normal repack now runs once during the texture cook and is cached (the resolver only CPU-repacks the imported-GLB fallback path) |
| Per-texture mip control via the `GenerateMipMaps` flag | Validated and merged | Keep | Replaces the old hard-coded linear-mip skip; export can still skip mips on heavy linear payloads, small but measurable win |
| PNG side-channel textures | Validated | Baseline only | Correct but decode-heavy |
| `DXT5` linear + `BC7` sRGB side textures in `textures.bin` | Tested and validated as a benchmark | Superseded by the player-side cook | Best in-package size/load tradeoff found, but the cache reached the same cached-load speed losslessly |
| Raw `RGBA32` side textures | Tested and invalidated | Do not use as shipping format | Very slow export, regression risk |
| Move GLB normals to sidecar | Tested and invalidated | Do not use without deeper investigation | Smaller/faster, but caused ghosting |
| DDS/BC7 embedded directly in GLB | Tested and invalidated | Do not use | Smaller package, much slower load |
| KTX2 / `KHR_texture_basisu` for GLB normals | Tested and invalidated for startup speed | Do not prioritize for load time | Smaller package, slower load |
| Persistent raw runtime texture cache | Tested and invalidated | Do not revive in the same form | Speedups came with corruption/crash risk |

## Source textures + player-side GPU cook (shipping since June 2026)

The final architecture honors two hard format constraints — no Unity dependency, lossless
editor re-import — while keeping the no-decode load path:

- the `.vpe` carries **original asset file bytes** (PNG/JPEG, no re-encoding) for every
  captured texture; `table.glb` carries no images for captured materials (9.8 MB vs 345.8 MB
  on T2)
- the **player cooks** the sources on first load: parallel decode (StbImageSharp on workers,
  native `LoadImage` on the main thread for the few huge files), GPU mip generation, GPU BC7
  encoding (DirectXTex compute shaders, MIT), AG normal repack — then persists a per-table
  cache keyed by package size/mtime and cook settings
- cached loads upload raw BC7 straight from the cache blob

Measured on Terminator 2 (editor play mode):

- baseline (PNG everywhere, 486 MB package): **23.3 s** every load
- source + cook (477 MB package): first load **9.5 s** (7.7 s cook), cached loads **1.1 s**

An intermediate iteration embedded the cooked BC7 payload in the package itself (no cook at
load, ~1.7-2.2 s every load) — it proved the decode-free path but was dropped: BC7-only
packages are lossy one-way data (editor re-import degrades per generation) and 42% larger than
the sources. The player-side cook keeps the speed while the package stays lossless, and lets
users choose cook quality/resolution locally (see `VpeTextureCookSettings.ResolutionDivisor`).

Supporting runtime work that landed with this: uninterrupted glTFast defer agent,
worker-thread prefetch of the texture cache (or source blob) and `materials.json`,
worker-thread bulk-read of all item/ref entries, worker-thread GLB extension/tangent scans, a
cached `PackagedRefs` type scan (was 450 ms per load), time-budget yields, and GPU work/upload
budgets per frame (without them, queueing hundreds of MB of GPU work in one frame crashed the
editor with `DXGI_ERROR_DEVICE_HUNG`).

Editor re-import reconstructs texture *assets* from the source layer (normal-map type, sRGB,
sampling and max-size restored) and rebuilds materials through the same `HdrpMaterialResolver`
the player uses — restoring masks, thickness, transmission and refraction state the old
GLB-based import silently dropped.

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

**Superseded:** the packed `textures.bin` was later dropped in favor of one plain PNG/JPEG file per texture under `table/textures/`. The zip central directory already provides the offset table, so the custom blob index was redundant, and per-file source entries keep the package losslessly re-exportable. The compression/direct-upload work it enabled now lives in the player-side cook and cache.

### 3. Cache resolved materials during import

`VpeMaterialReader` now reuses already-resolved materials instead of rebuilding equivalent runtime materials repeatedly.

Result:

- measurable reduction in material-restore time
- especially helpful on tables with many repeated imported materials

### 4. GPU normal repack for HDRP

At the time, this was the most important merged optimization: instead of repacking RGB normals for HDRP on the CPU inside `HdrpMaterialResolver`, a GPU shader did the channel conversion.

Representative outcome:

- total import dropped from roughly `10s` toward the `7-8s` range
- the normal repack hotspot dropped from multiple seconds to effectively negligible

**Where it lives now:** the resolver's GPU repack shader (`VpePackNormalForHdrp`) was removed once the player-side cook took over. The cook does its own GPU normal repack (to dxt5nm) and bakes the result into the cache, so the cached load pays nothing for normals. The resolver now CPU-repacks only the normals that arrive through the non-cooked / imported-GLB fallback path.

### 5. Per-texture mip control via the `GenerateMipMaps` flag

An earlier version hard-coded a skip of runtime mip generation for linear side textures. That was generalized: each side texture now carries a `GenerateMipMaps` flag that the reader honors for both sRGB and linear payloads (`VpeMaterialReader` builds the `Texture2D` and calls `Apply` with the serialized value). Export can disable mips for the classes that do not need them — typically the heavy linear mask/thickness payloads — so the decision lives with the package rather than the reader.

Linear side textures can additionally be block-compressed at load (`Texture2D.Compress`, BC7) when their `RuntimeCompress` flag is set, trading a little load time for much lower runtime memory. (The pre-cooked cache path skips this entirely — it uploads raw BC7 with baked mips and no decode.)

Result:

- small but real improvement on the heavy linear classes
- low risk, and the choice now lives with the package author

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

Why it stayed unmerged:

- desktop-oriented GPU format choice that needs a fallback for unsupported platforms
- needs a clear editor re-import story
- and, ultimately, the player-side cook reached the same cached-load speed (~1s) without giving up lossless re-import, so the in-package route was dropped

It is kept here as the strongest *in-package* size/load result on record (≈142.5 MB, ≈6.23s); the shipped path is the cook plus cache (see [Status](#status)), not this.

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

## Status

The load-time work is **done**. The player-side cook plus per-table cache (see the shipping section above) reached the goal: a table loads in about **one second** on a cached run, while the package stays lossless.

The path this page used to list as the highest-value next step — compressed side textures with a runtime direct-upload path and an explicit format discriminator — is realized, just via a per-machine cache instead of in-package compression:

- **direct upload, no decode** — the cached path uploads raw BC7 with baked mips (`isRawPayload` in `VpeMaterialReader`)
- **explicit format discriminator** — `VpeTexturePayload.PixelFormat` (the runtime/cache descriptor; the package's `VpeTexture` source entries are always encoded PNG/JPEG)
- **safer persistent cache** — the cook cache is keyed on package size/mtime and cook settings, so it invalidates correctly
- **overlapped IO/decode/setup** — worker-thread prefetch and parallel decode, with per-frame GPU work/upload budgets

In-package compression (a smaller shipped package, at the cost of losslessness and a per-platform format choice) was measured — see [Best unmerged result](#best-unmerged-result) — but deliberately not pursued: the cache reaches the same cached-load speed without giving up lossless re-import.

### Still not worth reviving

These stayed dead ends; don't re-run them as startup-speed optimizations without new information:

- GLB-normal relocation without deeper semantic investigation (caused insert/plastic ghosting)
- DDS-in-GLB for load time
- KTX2-in-GLB as a startup-speed optimization
- raw `RGBA32` side textures as a shipping format
