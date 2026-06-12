# Packaging

By packaging we mean serializing a table into a `.vpe` file. A `.vpe` is a ZIP container with:

- one glTF binary scene (`table.glb`)
- one optional glTF binary for physics-only meshes (`colliders.glb`)
- JSON metadata for items, refs, globals, assets, and material interchange
- VPE-owned texture payloads, pre-cooked into GPU-ready block-compressed formats

> [!IMPORTANT]
> As of the cooked-texture format (June 2026), `table.glb` carries **no image data** for
> materials captured by the material translator. Every captured texture is encoded at export
> time into its final GPU format (BC7 for color/mask data, DXT5 in HDRP's AG packing for
> normals, RGBA32 for non-block-compressible sizes), with the full mip chain baked, and stored
> raw in `meta/textures.bin`. Runtime upload is a straight `LoadRawTextureData` — no PNG
> decode, no runtime `Texture2D.Compress`, no normal repacking. On the Terminator 2 benchmark
> table this took the in-editor import from ~23s down to ~1.7-2.2s. See "Cooked Texture
> Format" below; several sections that follow describe the legacy PNG path, which remains the
> reader fallback for old packages and for uncaptured (unsupported-shader) materials.

This document describes:

- the on-disk format
- the export/import pipeline
- the material/texture split between `table.glb` and VPE sidecar data
- measured texture-compression experiments and their outcomes
- glTFast limitations that shape the implementation

> [!NOTE]
> Originally, there were thoughts about using the same container format as VPX (the Compound Binary File), but ultimately, given the inner structure would be quite different anyway, there was no real benefit.
>
> We've also tested a more efficient packing structure than JSON, but since the metadata to which it would apply is minuscule compared to the rest, the performance advantage was quickly outweighed by its unreadability and the hassle to set up.

## Cooked Texture Format

This is the shipping texture path since June 2026. It supersedes the PNG sidecar baseline and
implements (and extends) what the benchmarks below recommended as the "best unmerged result".

### Export

`HdrpMaterialV1Translator` captures **every** texture of supported materials (base color,
emissive, normal, mask, thickness — including textures that previously stayed on the glTF
path), and `HdrpMaterialV1TextureEncoder` cooks each into a GPU-ready payload:

- sRGB color and linear mask/thickness data → `BC7` (quality: Normal), mips baked on the same
  RGBA32 data the legacy path produced
- normal maps → decoded via `x = r * a` (covers plain RGB, DXT5nm and BC5 sources), re-packed
  into HDRP's AG layout `(1, y, 1, x)` — exactly what the runtime CPU repack used to emit —
  then `DXT5` (quality: Best)
- non-multiple-of-4 dimensions → raw `RGBA32` (still uploads without decode)
- on cook failure, the legacy PNG encode is the fallback

`VpeTextureAssetV1` gained `PixelFormat` (see `VpePixelFormats`) and `MipCount`; raw payloads
are the exact `GetRawTextureData` layout (all mips concatenated). Cooked normal refs carry
`Packing = dxt5nm`, which the resolver already treats as "use as-is".

The glTF export scope (`CreateSanitizedGltfExportMaterial`) strips **all** textures from
captured materials (Lit, Decal, and the VPE metal/rubber/DMD shader graphs), so glTFast writes
no images for them. Materials with unsupported shaders keep their textures in the GLB and fall
back to the imported material at runtime, exactly as before.

`meta/textures.bin` and the two GLBs are written as **stored** zip entries
(`PackageCompression.Stored`); block-compressed data doesn't deflate meaningfully and the
inflate cost used to be pure waste at load time.

### Runtime

`VpeMaterialV1Reader`'s texture provider creates `Texture2D(width, height, format, mipCount,
linear)` and uploads via `LoadRawTextureData` — straight out of the packed blob through a
pinned pointer, no per-texture byte copy. To keep the D3D12 queue healthy (a single frame
that queues hundreds of megabytes of uploads can trip the Windows GPU watchdog — observed as
`DXGI_ERROR_DEVICE_HUNG` crashes), the material pass yields to the player loop after every
~160 MB of uploads.

`RuntimePackageReader` additionally:

- imports the GLB with an `UninterruptedDeferAgent` (the player shows a loading screen, so
  per-frame time slicing only added wall time)
- prefetches `materials.v1.json` (parsed off-thread; Newtonsoft is thread-safe) and
  `textures.bin` on a worker with its own storage instance
- bulk-reads all item/ref entries on a second worker, so the restore loops get bytes from
  memory instead of paying per-entry zip cost
- scans the GLB for the embedded-materials extension and missing-tangent meshes on a worker,
  overlapped with `gltf.Load`
- warms the `PackagedRefs` PackAs-attribute type scan on a worker (it reflects over every
  loaded assembly and is cached per domain)
- yields by time budget instead of item count while restoring packables

### Measured result (Terminator 2, editor play mode)

| | PNG baseline | Cooked format |
| --- | --- | --- |
| package size | 486 MB | 689 MB |
| `table.glb` | 345.8 MB (336 MB PNGs) | 9.8 MB (no images) |
| `meta/textures.bin` | 140 MB PNG | 640 MB raw BC |
| `table.glb` import | ~10.5 s | ~0.4-0.7 s |
| material restore | ~10.9 s | ~0.6-0.8 s |
| sidecar texture loads | 3.0 s (PNG decode) | ~0.2 s (325 raw uploads) |
| **total import** | **~23.3 s** | **~1.7-2.2 s** |

The size increase is the price of mip chains plus the fact that BC7 doesn't compress as well
on disk as PNG. If package size becomes a concern, an LZ4-style supercompression layer over
`textures.bin` is the obvious follow-up — it decompresses an order of magnitude faster than
deflate.

### Caveats

- BC formats are desktop-oriented; `SystemInfo.SupportsTextureFormat` is checked at load and
  unsupported formats log and skip. A transcode fallback story is still open for platforms
  without BC7.
- Editor re-import of cooked packages has no path to reconstruct texture *assets* from raw BC
  payloads yet; authoring round-trips should keep using projects with original sources.
- Older PNG-sidecar packages still load through the legacy reader path unchanged.

## Design Summary

The package format is optimized around two competing goals:

- keep `table.glb` as the canonical scene graph, mesh, and standard-material payload
- keep VPE/HDRP-specific data out of glTF unless it survives import/export without visual regressions

In practice this means:

- geometry, lights, most standard textures, and the instantiated hierarchy live in `table.glb`
- VPE-only material semantics live in `meta/materials.v1.json`
- VPE-only texture bytes live in `meta/textures.bin`
- runtime imports the GLB first, then replaces or augments imported materials using the `materials.v1` payload

## File Structure

If you extract a `.vpe`, the relevant structure looks like this:

```plain
table/
|-- assets/
|   `-- ...
|-- global/
|   |-- coils.json
|   |-- lamps.json
|   |-- switches.json
|   `-- wires.json
|-- items/
|   `-- ...
|-- meta/
|   |-- colliders.json
|   |-- materials.v1.json
|   |-- sounds.json
|   `-- textures.bin
|-- refs/
|   `-- ...
|-- sounds/
|   `-- ...
|-- colliders.glb
`-- table.glb
```

Important details:

- `meta/materials.v1.json` is optional. It is only written when a `IVpeMaterialV1Translator` is registered and captures at least one profile.
- `meta/textures.bin` is the sidecar texture payload. It is a raw concatenation of VPE-only texture blobs. Offsets and lengths are stored per texture in `VpeTextureAssetV1`.
- there is no loose `meta/textures/` runtime path anymore. New development should assume side-channel textures come from `textures.bin`, or optionally from embedded GLB buffer views.
- `table.glb` may also carry a `VPE_materials` custom extension via `VpeMaterialsGltfExtension`, but export keeps that path disabled (`EmbedVpeMaterialsIntoGlb = false`) because the sidecar path is the supported one.

## Export

The main export entry point is `PackageWriter`.

### 1. Scene Preparation

For glTF export, the writer prepares the scene:

- ensures meshes are readable so glTFast can access vertex data
- temporarily enables author-time disabled `Light` components so `KHR_lights_punctual` contains the same light topology the runtime needs
- disables invalid renderers/meshes that would crash glTF export
- creates a temporary material-sanitizing scope via `VpeMaterialV1Translator.PrepareGltfExport(...)`

The sanitizing scope is important. It prevents exporting the same texture data twice when the VPE material system already owns a texture. Today it mainly strips textures that are intentionally side-channeled, while preserving the imported GLB fallback for everything else.

### 2. `table.glb`

The scene hierarchy rooted at the table is exported through Unity's fork of glTFast:

- format: binary glTF (`.glb`)
- images: standard glTFast export path, which means PNG/JPG only
- output: written to memory first, then stored as `table/table.glb`

The GLB contains:

- hierarchy
- transforms
- meshes
- lights
- imported fallback materials
- textures that stay on the glTF path

The GLB does not contain:

- component packables
- refs wiring
- globals
- assets
- VPE material profiles
- packed sidecar texture bytes

### 3. `colliders.glb` and `meta/colliders.json`

Physics-only meshes are not always part of the visible hierarchy that glTF export sees, so collider meshes are exported separately:

- mesh bytes go to `table/colliders.glb`
- authoring metadata goes to `table/meta/colliders.json`

`colliders.json` stores prefab linkage and override data so editor re-import can reconnect collider meshes correctly.

### 4. Items, Refs, Globals, Assets, Sounds

The rest of the package is exported in this shape:

- `items/` contains component/item data
- `refs/` contains cross-reference data restored in a second pass
- `global/` contains switches, coils, lamps, wires
- `assets/` contains serialized `ScriptableObject` assets plus their metadata
- `sounds/` contains wave data
- `meta/sounds.json` contains sound metadata

### 5. `materials.v1` Capture

HDRP material capture is performed by `HdrpMaterialV1Translator`.

The translator converts supported HDRP materials into a portable `VpeMaterialsPayloadV1`:

- supported authoring shaders:
  - `HDRP/Lit`
  - `HDRP/Decal`
  - `HDRP/Unlit`
  - VPE HDRP metal shader graph
  - VPE HDRP rubber shader graph
- unsupported shaders are not fatal
  - they fall back to the imported GLB material at runtime
  - the translator aggregates them into a summary instead of spamming one warning per material

The payload contains:

- `Profiles`
  - one logical material profile per normalized material name
- `Textures`
  - one `VpeTextureAssetV1` per side-channeled texture blob
- `RendererStates`
  - shadow casting mode, receive shadows, rendering layer mask

### 6. Texture Ownership Rules

This is the most important part of the format.

The translator intentionally splits texture ownership between glTF and the VPE sidecar.

#### Textures that stay on the imported GLB path

These are captured as "imported texture refs", meaning the profile stores tiling/semantic intent, but runtime reads pixels from the material that glTFast already imported:

- opaque lit base color maps
- emissive maps
- most unlit color maps
- all supported normal maps

Normals stay on the GLB path. Several benchmarked alternatives were faster or smaller, but caused visual regressions such as insert/plastic ghosting.

#### Textures that are side-channeled into `textures.bin`

These are captured as explicit `TextureId` references backed by `VpeTextureAssetV1` entries:

- HDRP `MaskMap`
- HDRP `ThicknessMap`
- alpha-bearing lit base color maps
  - transparent
  - alpha-tested
- decal base color maps
- decal mask maps
- VPE metal shader graph mask maps
- VPE rubber shader graph base color and mask maps

Why these are side-channeled:

- glTF cannot represent HDRP `MaskMap` semantics losslessly
- albedo alpha is load-bearing for inserts, plastics, and decals
- shader graph materials can use VPE-specific texture slots that plain glTF does not understand
- glTF/JPG conversion can silently drop or alter alpha when relying on fallback material export

### 7. `textures.bin`

The exporter does not write one file per side texture. It writes one packed blob:

- `meta/textures.bin`

`BuildPackedMaterialTextureData(...)` simply concatenates each side texture byte array and records:

- `ByteOffset`
- `ByteLength`
- `FileName` (export-side blob key only)
- `MimeType`
- `ColorSpace`
- filter/wrap/aniso/mipmap hints
- runtime compression hint
- width/height

Writer behavior:

- side-channel textures are PNG-encoded
- `MimeType` defaults to `image/png`
- `ByteOffset` / `ByteLength` are filled in
- `GlbBufferView` remains unused in the shipping path

### 8. Optional GLB Embedding Path

`VpeMaterialsGltfExtension` exists as a post-process helper that can:

- inject a `VPE_materials` custom extension into `table.glb`
- append side texture blobs into GLB buffer views
- read the payload back at runtime

Export keeps that path disabled:

- `PackageWriter.EmbedVpeMaterialsIntoGlb = false`

Reason:

- the sidecar path is stable
- the GLB-embedding path is useful as infrastructure and for future work
- but it is not the format used by exported packages

## Runtime Import

The runtime import entry point is `RuntimePackageReader`.

### Import Order

Runtime import is intentionally ordered:

1. Load `table.glb`
2. Unpack sounds
3. Unpack assets
4. Unpack collider meshes
5. Restore packables from `items/`
6. Restore refs from `refs/`
7. Restore globals
8. Read table metadata
9. Restore material profiles from `materials.v1`

`table.glb` must be first because every later step depends on the imported hierarchy already existing.

### Runtime GLB Import

At runtime, the GLB is imported directly from bytes in memory:

- `sceneFile.GetData()`
- `new GltfImport(...)`
- `gltf.Load(sceneData, uri, cancellationToken: ...)`
- `InstantiateMainSceneAsync(...)`

The reader also checks the optional `VPE_materials` extension before import:

- `VpeMaterialsGltfExtension.TryReadPayload(...)`
- `VpeMaterialsGltfExtension.TryReadEmbeddedTextureBlobs(...)`

That code path is dormant for normal exports, because the writer keeps extension embedding disabled. It exists so GLB-only experiments do not need a separate schema.

### Runtime Material Restore

`VpeMaterialV1Reader` owns the runtime restore pass.

Important behavior:

- it reads `meta/materials.v1.json`
- it prefers embedded GLB texture blobs if present
- otherwise it reads `textures.bin`

`TextureProvider.Get(...)` currently:

- slices bytes from `textures.bin` using `ByteOffset` / `ByteLength`
- creates `Texture2D`
- uses `ImageConversion.LoadImage(...)`
- applies wrap/filter/aniso
- caches the resulting `Texture2D` per import

Mip behavior:

- side textures currently respect `GenerateMipMaps` for both `sRGB` and `Linear` payloads
- linear side textures are decoded as linear textures
- when `RuntimeCompress` is true, linear side textures are runtime-compressed with `Texture2D.Compress(highQuality: true)` before they are made non-readable

The table export inspector exposes a `Compress sidecar textures (Unity runtime compression)` toggle. It sets `VpeTextureAssetV1.RuntimeCompress` for every side-channel texture in the package. Existing packages that do not carry the field default to `true`, preserving the original reader behavior.

The inspector also exposes a `Compress glTF textures` toggle. When enabled, glTFast keeps its normal image export behavior, including JPEG for opaque base color textures. When disabled, the writer post-processes `table.glb` and replaces matching embedded glTF image payloads with the original PNG/JPEG asset bytes.

The `Compress runtime normal maps (Unity runtime compression)` toggle controls the HDRP resolver's normal-map repack output. Imported GLB/runtime-loaded RGB normals still need to be repacked for HDRP; this flag decides whether that repacked texture is then compressed with Unity's runtime texture compressor.

Earlier benchmark work showed that skipping runtime mip generation for heavy linear side textures can save load time, because the linear payload is dominated by mask and thickness data. That optimization is documented as a target, but the current reader still follows the serialized `GenerateMipMaps` flag for linear textures.

### Runtime Resolver

`VpeMaterialV1Reader` itself is SRP-agnostic. It delegates actual material creation to `IVpeMaterialResolver`.

For HDRP, `HdrpMaterialResolver`:

- clones pre-authored template materials
- restores base color, mask map, emissive, transmission, and so on
- restores renderer states after the material pass
- caches resolved materials aggressively
- reuses one resolved material for repeated imported-material instances

It also contains the most important merged runtime optimization so far:

- RGB normal maps are repacked for HDRP on the GPU with `VpePackNormalForHdrp.shader`
- fallback remains CPU-based if the shader path fails

This optimization reduced the normal repack cost from multiple seconds to effectively negligible on the benchmark table.

## Editor Import

Editor import differs from runtime import in one important way:

- runtime loads GLB directly from memory
- editor writes GLB back into the Unity project and re-imports it through the asset pipeline

The rest of the logical order is similar:

1. import GLB
2. unpack assets and colliders
3. restore items
4. restore refs
5. restore globals
6. restore authoring metadata

## Texture Compression

This section documents the shipping texture path and the benchmarked alternatives.

### Shipping Texture Path

Mainline behavior is:

- `table.glb` images are whatever glTFast exports
  - PNG or JPG
- side-channel VPE-only textures are PNG
- side-channel texture bytes are stored in `meta/textures.bin`
- runtime loads side-channel textures via `ImageConversion.LoadImage(...)`
- `RuntimeCompress` controls whether the reader calls `Texture2D.Compress(...)` after decoding linear side textures
- glTF texture compression is controlled separately by the export-time `Compress glTF textures` toggle
- normal-map repack compression is controlled by `VpeNormalMapRefV1.RuntimeCompress`

This path is stable and visually correct, but not the fastest possible one.

### Why Texture Compression Matters

The benchmarks showed that once:

- duplicate textures were removed
- material reuse caching was added
- GPU normal repack was added

the next dominant cost was texture decode, not ZIP IO itself.

The biggest speedups came from removing `LoadImage(...)` and moving side textures onto direct GPU-uploadable formats.

### Formats and Outcomes

The measurements below were taken on the Terminator 2 table during the optimization pass. Treat them as benchmark data, not guarantees for every table.

#### 1. PNG Sidecar in `textures.bin`

Status:

- shipping baseline

Behavior:

- side textures are exported as PNG
- runtime decodes them with `LoadImage(...)`

Representative result after other merged optimizations:

- package around `186 MB`
- total import around `7.3s` to `7.7s`

Notes:

- stable
- visually correct
- pays a large PNG decode cost

#### 2. Raw `RGBA32` Sidecar

Status:

- benchmark only
- rejected

Intent:

- eliminate PNG decode cost entirely
- store raw pixels and upload via `LoadRawTextureData(...)`

Observed result:

- package around `178 MB`
- export time ballooned to roughly `3 minutes`
- visual regressions were observed during the experiment

Conclusion:

- useful as a proof that decode avoidance matters
- not viable as-is
- export cost and regression risk were too high

#### 3. `DXT5` for Linear Side Textures

Status:

- benchmark only
- successful for speed and size

Applied to:

- linear VPE side textures
- primarily mask/thickness-style data

Observed result:

- package around `158 MB`
- total import around `6.57s`
- side-texture load time dropped substantially
- visuals looked acceptable on the benchmark table

Conclusion:

- strong improvement
- lossy, but tolerable for those linear texture classes

#### 4. `DXT5` for Linear + `BC7` for sRGB Side Textures

Status:

- benchmark only
- best sidecar result

Applied to:

- `DXT5` for linear side textures
- `BC7` for remaining sRGB side textures

Observed result:

- package around `142.5 MB`
- total import around `6.23s`
- side-texture load time dropped to roughly `80 ms`
- `compressedTextureLoads=97`
- `encodedTextureLoads=0`
- visuals looked fine on the benchmark table

Conclusion:

- this was the strongest sidecar compression result
- the main win was format/direct-upload, not just file size
- this is the most promising future reimplementation path for side textures

#### 5. Move GLB Normals to Sidecar

Status:

- benchmark only
- rejected

Observed result:

- package could shrink dramatically, down near `119 MB`
- but visual regressions returned
- the main symptom was ghosting on inserts/plastics

Conclusion:

- the path was faster and smaller
- but semantically wrong
- do not reimplement blindly without first understanding what imported glTF normals preserve that the sidecar path currently does not

#### 6. DDS / BC7 Embedded Directly in GLB

Status:

- benchmark only
- rejected

Observed result:

- package shrank to about `116.8 MB`
- but `table.glb` import time exploded
- representative timings:
  - `table.glb`: about `7.9s`
  - total import: about `11.3s`

Why it failed:

- DDS/BC blocks are poor ZIP citizens in this setup
- compressed-on-disk GLB got smaller, but the uncompressed GLB payload got much larger
- runtime paid that inflated payload cost during GLB import

Conclusion:

- good for disk footprint
- bad for uncached load time in this `.vpe` container

#### 7. KTX2 / `KHR_texture_basisu` for GLB Normals

Status:

- benchmark only
- not kept

Implementation notes:

- required patching local glTFast
- normals were exported through `toktx`
- textures were serialized through `KHR_texture_basisu`

Observed result:

- package around `130.6 MB`
- GLB really contained `87` `image/ktx2` images
- but runtime was slower than the PNG/JPG GLB baseline
- representative timings:
  - total import: about `6.17s`
  - `table.glb`: about `2.81s`
- this was slower than the best PNG/JPG GLB + compressed-sidecar baseline

Why it failed:

- KTX2 reduced on-disk size
- but the runtime transcode path added enough overhead to lose on startup time

Conclusion:

- useful for footprint
- not a load-time win for this table in the tested setup

### Re-Implementation Notes for the Best Unmerged Sidecar Path

If a future agent wants to re-implement the successful sidecar compression branch, the important points are:

- keep the logical split between imported GLB textures and side-channel textures
- only change the side-channel encoding and runtime upload path
- do not change normal ownership yet

Recommended shape:

1. Keep `meta/textures.bin` as the carrier.
2. Extend `VpeTextureAssetV1` with an explicit texture-data-format discriminator.
3. On export:
   - make a readable copy of the texture
   - compress linear side textures to `DXT5`
   - compress sRGB side textures to `BC7`
   - store raw compressed bytes in `textures.bin`
4. On runtime import:
   - create `Texture2D(width, height, format, mipChain, linear)`
   - call `LoadRawTextureData(...)`
   - `Apply(...)`

Critical constraint:

- keep a fallback path for platforms that do not support the chosen GPU format
- keep a fallback path for editor re-import if those packages need to round-trip on unsupported hardware

## glTFast Issues and Constraints

These findings shape the implementation.

### 1. Export Is PNG/JPG-Oriented

glTFast export currently assumes standard encoded images:

- PNG
- JPG

There is no clean public export hook for "use this custom image payload/extension format instead".

Practical consequence:

- standard GLB texture experiments required patching or forking glTFast
- sidecar experiments were much cheaper to iterate on than GLB texture experiments

### 2. KTX2 Import Exists, Export Does Not

glTFast can import `KHR_texture_basisu`, but export is not wired for it.

Practical consequence:

- KTX2 GLB experiments required local fork work
- they were not one-line package upgrades

### 3. Custom Extension Support Is Incomplete for This Use Case

`VpeMaterialsGltfExtension` exists and works as a local post-process helper, but glTFast does not provide a clean end-to-end public API for exporting arbitrary VPE material extensions.

Practical consequence:

- a `VPE_materials`-inside-GLB format is technically possible
- but today it is more practical as a local GLB rewrite step than as a stock glTFast export feature

Related import-side issue:

- the fast runtime parser path is optimized for known schema
- arbitrary custom extension payloads are not something the stock runtime path exposes conveniently
- this is one reason `VpeMaterialsGltfExtension` reads and writes raw GLB JSON/chunks itself instead of relying on glTFast to surface custom payload data

### 4. Runtime Performance Depends on Payload Shape, Not Just File Size

The DDS and KTX2 experiments were the clearest proof:

- smaller `.vpe` files do not automatically load faster
- transcoding cost, ZIP behavior, and uncompressed in-memory payload size matter just as much

This is why the sidecar compression experiments outperformed the GLB compression experiments.

## Performance Summary

### Merged Improvements

The following changes are in the format/runtime path:

- duplicate texture storage between GLB and sidecar was reduced substantially
- VPE-only textures are packed into `meta/textures.bin`
  - no more one-file-per-texture export on the normal path
- unsupported HDRP shaders do not flood export with warnings
- runtime material replacement caches resolved materials aggressively
- runtime normal repack uses a GPU path via `VpePackNormalForHdrp.shader`
- linear side textures skip runtime mip generation
- import timings and resolver diagnostics are logged in detail

### Measured Effect of the Merged Work

On the benchmark table, the merged work took import from roughly the original `~10-11s` range down to roughly `~7.3-7.7s`, depending on the exact branch state and rerun.

The biggest merged win was the GPU normal repack:

- it reduced the normal repack cost from multiple seconds to effectively negligible

### Best Additional Gains Found During Experiments

The best unmerged result came from compressing side-channel textures into GPU-native formats while leaving GLB textures alone:

- `DXT5` for linear side textures
- `BC7` for sRGB side textures

Representative result:

- package around `142.5 MB`
- total import around `6.23s`

### Future Gains Worth Pursuing

Most promising:

- re-implement compressed sidecar textures with a platform-aware fallback story
- add persistent caching only after the raw format is stable
- overlap IO/decode/setup work more aggressively once the payload format is fixed

Possible but lower-confidence:

- GLB-side compressed texture work
  - DDS and KTX2 experiments did not beat the sidecar approach on load time
- deeper glTFast fork work
  - likely only worth it if the project decides that GLB must become the canonical home for more textures again

Not recommended without new evidence:

- moving GLB normals into the sidecar again without first solving the ghosting issue
- raw `RGBA32` sidecar export as a shipping format

## Recommended Next Steps

If a future agent picks this up, the most practical order is:

1. Keep the sidecar/material split.
2. Re-implement compressed `textures.bin` first.
3. Add a format discriminator plus platform fallback handling.
4. Re-measure cold-load import.
5. Only then decide whether GLB texture work is worth the complexity.

That sequence gave the best empirical results during this optimization pass.
