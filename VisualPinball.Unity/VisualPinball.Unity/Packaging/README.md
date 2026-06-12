# Packaging

By packaging we mean serializing a table into a `.vpe` file. A `.vpe` is a ZIP container with:

- a root `manifest.json` (format version, schema versions, requirements)
- one glTF binary scene (`table.glb`) whose nodes carry stable ids in their `extras`
- one optional glTF binary for physics-only meshes (`colliders.glb`)
- JSON payloads for items, refs, globals, assets, materials and lights
- the lossless source layers: original image files under `table/textures/`, original audio
  files under `table/sounds/`

The **normative format specification** lives in [FORMAT.md](FORMAT.md) — container layout,
manifest, node identity, all payload schemas, enum tables and reader requirements. This document
covers the implementation: how the writer and the readers work, and the measured history that
shaped the design.

> [!IMPORTANT]
> The format is designed around two hard constraints — it must not depend on Unity, and a `.vpe`
> must re-import into the editor without quality loss — plus two priorities: use existing
> standards wherever one exists, and keep the format expandable (versioned manifest, schema
> versions per payload, skip-unknown rules).
>
> The texture architecture is **source + local cook**: the package carries lossless,
> engine-neutral sources (plain PNG/JPEG files, one zip entry each); the player cooks them into
> GPU-ready payloads (BC7 today, ASTC on a future mobile player) on first load and persists them
> in a local per-table cache. Cached loads upload raw bytes — no decode of any kind.
>
> Terminator 2 benchmark (editor play mode): first load ~9.5s (includes the cook), every load
> after ~1.1s, package 477 MB (lossless).

> [!NOTE]
> Originally, there were thoughts about using the same container format as VPX (the Compound
> Binary File), but ultimately, given the inner structure would be quite different anyway, there
> was no real benefit. We've also tested a more efficient packing structure than JSON, but since
> the metadata to which it would apply is minuscule compared to the rest, the performance
> advantage was quickly outweighed by its unreadability and the hassle to set up.

## Design rationale

Decisions worth knowing the "why" of:

- **Root `manifest.json`.** Format version, writer id, schema version per payload, root node id,
  and the list of component types the package uses (readers warn about missing plugins up front
  instead of failing mid-restore). A file without a manifest is refused.
- **Stable node ids** (`extras.vpeId` in table.glb). Items, refs, mapping devices, renderer
  states and light profiles all reference nodes by id; the glTF node tree is the single source of
  truth for hierarchy. Ids survive node reordering and importer-side hierarchy additions
  (orientation helpers, multi-primitive children) — positional schemes (sibling-index paths)
  proved fragile against exactly those. The runtime binds ids exactly via the glTFast
  instantiator's `NodeCreated` hook; the editor binds structurally against the imported prefab
  (reproducing glTFast's `OriginalUnique` name de-duplication).
- **Plain texture files** under `table/textures/`. The zip central directory is the index — no
  custom offset bookkeeping, and a package is inspectable with any zip tool. At load, the reader
  packs the files into one transient blob so the cook/cache/upload pipeline downstream works off
  one contiguous buffer; on cache hits the source files aren't read at all.
- **Portable material core + pipeline hint blocks** (`meta/materials.json`): the top of each
  profile is PBR intent mapping onto glTF PBR + KHR extension semantics; everything HDRP-specific
  sits in a nested `Hdrp` block other pipelines ignore. Enumerations are strings, not engine enum
  ints.
- **Lossless GLB images, always.** The writer swaps glTFast's re-encoded images back to the
  original asset bytes (with collision detection on duplicate file stems). This covers materials
  that are *not* captured into the material payload — captured materials have their textures
  stripped from the GLB entirely.
- **Reproducible exports**: fixed zip entry timestamps, so identical content produces
  byte-identical packages.
- **Robustness**: hostile zip entry names are rejected (zip-slip), unknown component types are
  skipped with a warning instead of failing the import.

## Export

The entry point is `PackageWriter`. Order of operations:

1. **Activate** the (authored-inactive) cabinet and backbox via their marker components; they are
   restored at the end. The package always contains them active.
2. **Assign node ids** — one GUID per active transform (`VpeNodeIds.AssignIds`); these key
   everything written below.
3. **Prepare the scene export** (glTFast, binary glTF): make meshes readable, temporarily enable
   author-disabled lights (so `KHR_lights_punctual` carries the full light topology), disable
   invalid renderers, and swap captured materials for texture-free clones
   (`PrepareGltfExport`) so the GLB carries no duplicate image data.
4. **Write items and refs**, keyed by node id: `items/<id>/<Type>/<n>.json` via each component's
   `IPackable`, refs in a second pass.
5. **Write globals** (mapping config, devices referenced by node id), **table metadata**,
   **materials** (HDRP translator → portable payload + source texture blobs) and **lights**
   (per-light profiles by node id).
6. **Write assets and sounds** (with audio import intent).
7. **Finish the GLB**: glTFast writes to memory; the writer replaces re-encoded images with the
   original asset bytes and injects the node ids into the glTF `extras`
   (`VpeNodeIds.InjectIds` — fails the export loudly if the trees can't be reconciled).
8. **Write the source texture layer**: one stored zip entry per captured texture under
   `table/textures/`.
9. **Write screenshots** and finally the **manifest**.

## Runtime Import

The entry point is `RuntimePackageReader`. Notable mechanics:

- The manifest is read first; missing or too-new manifests are refused with a clear error.
- The scene GLB is imported from memory; a `GameObjectInstantiator` records the exact
  glTF-node-index → GameObject mapping (`NodeCreated`), which together with the parsed
  `extras.vpeId`s yields the id → transform map. The table root is `manifest.RootNodeId`.
- Worker threads prefetch in parallel with the scene import: the material payload + source
  textures (or the cooked cache), all item/ref bytes in bulk, and the PackAs type scan.
- **Texture cook**: on first load, source images are decoded in parallel (StbImageSharp on
  workers, Unity's native decoder for the few huge files), normals are AG-repacked, mips
  generated and BC7-encoded on the GPU, and the result is persisted per-table under
  `persistentDataPath/TextureCache`. Cache hits skip the source layer entirely and upload raw
  bytes through a pinned pointer. Platforms without compute fall back to decode-at-load.
- Restore order: sounds → assets → collider meshes → items → refs → globals → metadata →
  materials. Light intensities are normalized and `meta/lights.json` applied right after
  instantiation.
- To keep the D3D12 queue healthy, the material pass yields after every ~160 MB of uploads and
  the cook yields by dispatched-pixel budget (a single frame queueing hundreds of MB of GPU work
  can trip the Windows GPU watchdog).

## Editor Import

The entry point is `PackageReader`. Differences from runtime:

- The GLB is written into the project and imported through the asset pipeline (meshes become
  assets); node ids are then bound structurally against the instantiated prefab
  (`VpeNodeIds.BindInstantiated`), reproducing glTFast's sibling-unique naming.
- The source texture layer is written back as real texture assets, with importer settings
  (normal-map type, sRGB flag, wrap/filter/aniso, max size) restored from the payload via
  `VpeTextureImportPreprocessor` — each texture runs through the asset pipeline exactly once.
- Material reconstruction goes through the registered `IVpeMaterialEditorImporter`; the HDRP
  implementation drives the same `HdrpMaterialResolver` the player uses and saves `.mat` assets.
- Lights are restored the same way as at runtime (`VpeLightRestore`), so a re-imported table's
  lights match the authored ones.
- Sounds are de-duplicated against existing project assets by guid; new files get their import
  intent applied via `VpeSoundImportPreprocessor`.

## Texture-Compression History

The shipping texture path is *source + local cook* (see above). The sections below document the
benchmarked alternatives that led there — they describe **historical experiments**, kept because
they answer "why not X?" questions with data. All numbers are from the Terminator 2 table during
the optimization pass.

### Why the cook exists

Once duplicate textures were removed, material reuse caching was added and the normal repack
moved to the GPU, the dominant load cost was *texture decode*, not zip IO. The biggest speedups
came from removing `LoadImage(...)` and moving textures onto direct GPU-uploadable formats — but
shipping GPU formats in the package would have been lossy and platform-specific, so the cook
derives them locally instead.

### Benchmarked alternatives (historical)

| Approach | Package | Import | Verdict |
| --- | --- | --- | --- |
| PNG sidecar, decode at load (old baseline) | 186 MB | ~7.3–7.7 s | stable but pays full PNG decode every load |
| Raw RGBA32 sidecar | 178 MB | — | rejected: ~3 min export, visual regressions |
| DXT5 linear sidecar | 158 MB | ~6.6 s | good, but lossy in-package |
| DXT5 linear + BC7 sRGB sidecar | 142.5 MB | ~6.2 s | best sidecar result; superseded by the local cook (lossless in-package, ~1.1 s cached) |
| GLB normals moved to sidecar | 119 MB | — | rejected: insert/plastic ghosting |
| DDS/BC7 embedded in GLB | 116.8 MB | ~11.3 s | rejected: inflated in-memory GLB payload, slow import |
| KTX2 / `KHR_texture_basisu` GLB normals | 130.6 MB | ~6.2 s | rejected: transcode overhead lost to PNG/JPG baseline; also re-encodes (not lossless) |

Key lesson: **smaller files do not load faster** — transcoding cost, zip behavior and the
uncompressed in-memory payload size matter as much as bytes on disk. And for the *source layer*,
anything that re-encodes (KTX2/BasisU included) violates the lossless constraint; standard GPU
containers only make sense for the local cache, which is private and regenerable.

### glTFast constraints (still true)

- Export is PNG/JPG-oriented; there is no public hook for custom image payloads — which is why
  the lossless-image guarantee is implemented as a GLB post-process (image swap, see
  `GlbImageSwap`) rather than an exporter feature.
- KTX2 import exists, export does not.
- There is no end-to-end public API for custom material extensions; the node-id injection is raw
  GLB chunk/JSON processing (`GlbJsonUtil`).

## Caveats

- BC7 cooking needs compute support; platforms without it use the decode-at-load fallback. A
  mobile player would cook ASTC from the same sources — no transcoding needed by design.
- The local texture-cache directory grows unbounded (one file per table); eviction is a
  follow-up.
- The transient source blob a reader packs at load time uses 32-bit offsets, capping the *loaded*
  source-texture set at 2 GB. This is a current reader limitation, not a format one.
