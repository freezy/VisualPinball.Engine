# The .vpe Package Format

This is the normative specification of the `.vpe` table package format (format version 1). It
describes what a conforming writer produces and what any reader — VPE or not — needs to know to
unpack and load a table. The implementation notes live in [README.md](README.md).

Two hard constraints shape the format:

1. **Editable reimport** — a package restores the scene, generator settings, material intent, and bake maps for further editing. Supported 8-bit source images retain their original bytes; 16-bit PNGs are normalized to 8-bit PNGs on export, which is sufficient for the insert optical maps.
2. **Engine-neutral** — nothing in the format requires Unity. Scene and meshes are glTF, images
   are PNG/JPEG, audio is WAV/OGG/MP3, metadata is JSON. Where pipeline-specific data exists, it
   is isolated in clearly-marked hint blocks that other engines ignore.

Derived data (GPU texture formats, mip chains) is intentionally **not** part of the format. A
player cooks the sources into whatever its platform wants (BC7, ASTC, …) on first load and caches
the result locally.

## Container

A `.vpe` file is a standard **zip archive** (PKZIP, deflate or stored entries). Entry paths use
forward slashes. Readers must reject entry names containing `..`, `\` or `:` segments.

```
manifest.json                ← format identification, versions, requirements
table/
├── table.glb                ← scene: hierarchy, meshes, lights, fallback materials (glTF 2.0 binary)
├── colliders.glb            ← physics-only meshes (glTF 2.0 binary, optional)
├── table.json               ← table metadata (title, authors, …)
├── items/<nodeId>/…         ← per-node component data
├── refs/<nodeId>/…          ← per-node cross-references (second restore pass)
├── global/                  ← switch/coil/wire/lamp mappings
│   ├── switches.json
│   ├── coils.json
│   ├── wires.json
│   └── lamps.json
├── assets/<Type>/…          ← serialized shared assets + their meta
├── sounds/<file>            ← original audio files (wav/ogg/mp3/aiff)
├── textures/<file>          ← image sources (png/jpg; 16-bit PNGs normalized to 8-bit)
└── meta/
    ├── materials.json       ← portable material payload and texture inventory
    ├── inserts.json         ← editor-only insert generator and bake state
    ├── lights.json          ← light-source payload
    ├── sounds.json          ← audio meta (ids, import intent)
    └── colliders.json       ← collider-mesh meta
screenshots/                 ← preview images (jpg) + crop bounds (json, optional)
└── thumbs/                  ← small cropped copies of the preview images (jpg, optional)
```

Already-compressed payloads (the two GLBs, everything under `textures/`) are written as *stored*
zip entries; JSON entries are deflated. Entry timestamps are fixed (DOS epoch) so identical
content produces byte-identical packages.

Every key in the package's JSON is camelCase (`formatVersion`, `isActive`, `nodeId`), and private serialized fields lose their leading underscore (`_deviceId` is written `deviceId`). Keys of maps keyed by data (node ids, file names, payload names) are that data and keep their spelling. Packages of `items` schema 2 and older used PascalCase.

Optional inert consumer content is stored below `table/content/<content-id>/` with a
`manifest.json` and a `files/` directory. The normative layout and security rules are specified
in [content bundles](#content----inert-directory-bundles).

## manifest.json

The root-level manifest identifies the file and declares versions. It is the first thing a reader
parses; **a file without a manifest is not a valid .vpe package** and must be refused.

```json
{
  "formatVersion": 1,
  "writtenBy": "VPE 0.16.0 (Unity 6000.5.0f1)",
  "rootNodeId": "9f2c6c1e6a414b1f8d3a2b7c5e4f0a91",
  "schemas": { "items": 3, "itemsReader": 3, "materials": 2, "inserts": 1, "lights": 1, "sounds": 1 },
  "componentTypes": [ "Flipper", "KickerCoil", "Surface", "…" ]
}
```

| Field | Meaning |
| --- | --- |
| `formatVersion` | Container layout version. Readers must refuse versions greater than they implement. |
| `writtenBy` | Free-form writer identification, diagnostics only. |
| `rootNodeId` | Node id of the table root in `table.glb`. Readers anchor the restored component hierarchy here. |
| `schemas` | Schema version per payload, keyed by payload name. Readers must check a payload's schema version before parsing it and skip (with a warning) versions they don't know. `itemsReader` is not a payload: it is the oldest `items` version a reader must implement to load the package (see below). |
| `componentTypes` | All component/asset type names used by `items/`, `refs/` and `assets/`. Lets a reader warn about missing plugins up front. |

**Versioning policy.** `formatVersion` changes only on incompatible container-layout changes
(folder structure, node addressing). Payload schemas evolve independently via `Schemas`. Within a
schema version, adding optional JSON fields is allowed; removing or re-typing a field requires a
version bump. Fields are never repurposed.

### Versioning the items schema

The `items` schema covers `items/` and `refs/`: the component JSON, and what the runtime makes of
it. Three constants in `VpePackageSchemas` (`VpePackageManifest.cs`) govern it, and whoever changes
how components are exported or restored keeps them right:

| Constant | Meaning | Written as |
| --- | --- | --- |
| `ItemsVersion` | The version this build writes. | `Schemas["items"]` |
| `ItemsReaderVersion` | The oldest version a reader must implement to load what this build writes. | `Schemas["itemsReader"]` |
| `MinItemsVersion` | The oldest version this build still loads. | – |

The player loads a package when its `items` is at least `MinItemsVersion` and its `itemsReader`
(or, when missing, its `items`) is at most `ItemsVersion`. Otherwise it refuses the table and says
why, using `ItemsChanges` for packages that are too old.

Bump them by what the change does to tables and players that already exist:

1. **The change needs no reader to know about it** (a new optional field the restore doesn't depend
   on, a fix that doesn't alter what's stored): bump nothing.
2. **The change is compatible both ways**: older players load the new tables correctly (they ignore
   the new data and lose nothing that matters), and this build loads the old tables. Raise
   `ItemsVersion` and add a line to `ItemsChanges`. Leave `ItemsReaderVersion` and `MinItemsVersion`
   alone.
3. **Older players would load the new tables wrongly** (renamed, removed or re-typed fields, changed
   meaning, data the restore now depends on): raise `ItemsVersion`, add a line to `ItemsChanges`,
   and raise `ItemsReaderVersion` to the new `ItemsVersion`. Older players then tell the user to
   update VPE.
4. **This build would load the old tables wrongly** (a removed migration, a default that changed
   meaning): during alpha and beta, raise `ItemsVersion`, add a line to `ItemsChanges`, and raise
   `MinItemsVersion` (and normally `ItemsReaderVersion`) to the new `ItemsVersion`, so old tables
   are refused with a request to export them again. After the beta, keep `MinItemsVersion` and
   migrate the old versions in the reader instead.

An `ItemsChanges` line says what changed, in words a player understands, because it ends up in
the refusal message. When unsure between two cases, take the stricter one: a refused table with a
clear message is better than one that loads half-working. `InvalidPackageTests` checks that the
constants stay consistent.

## Node identity

Every exported GameObject becomes a glTF node in `table.glb`, and every such node carries a
stable id in the standard glTF per-node extension point:

```json
{ "name": "LeftFlipper", "mesh": 12, "extras": { "vpeId": "c0ffee00deadbeef0123456789abcdef" } }
```

- Ids are 32-char lowercase hex GUIDs, unique within the package, generated at export.
- Everything else in the package references nodes **by this id**: item folders, cross-references,
  mapping devices, renderer states, light profiles.
- The id of the table root is repeated in the manifest as `rootNodeId`.
- glTF exporters/importers may add synthetic nodes (e.g. `*_Orientation` helpers for lights,
  per-primitive mesh children). These carry no `vpeId` and are not referenced by the package.
- Reordering nodes in the GLB does not break a package; ids are the identity, not positions.

## table.glb

Binary glTF 2.0. Carries the scene hierarchy, transforms, render meshes, lights
(`KHR_lights_punctual`) and PBR fallback materials. Standard glTF conventions apply: Y-up,
right-handed, meters.

- **Lossless images.** Images embedded in the GLB are the original asset bytes (PNG/JPEG); the
  writer swaps glTFast's re-encoded images back to the originals after export. Materials captured
  into `meta/materials.json` have their textures stripped from the GLB entirely (the source layer
  carries them); only materials *not* captured (unsupported shaders) keep images in the GLB, as
  the portable fallback.
- **Light intensities** are multiplied by 100 on export (glTF's lumen conversion swallows most of
  it). Readers divide by 100 after import; `meta/lights.json` is the authored source of truth and
  is applied on top.
- Cameras and animations are not exported.

## items/ and refs/

Component data, keyed by node id:

```
table/items/<nodeId>/item.json            ← GameObject-level state
table/items/<nodeId>/<TypeName>/0.json    ← one file per component instance (ordinal index)
table/refs/<nodeId>/<TypeName>/0.json     ← cross-references, restored in a second pass
```

- `<TypeName>` is the component's registered pack name (PackAs attribute), decoupled from C#
  type names.
- `item.json`: `{ "name", "isActive", "isStatic", "prefabGuid" }`. `prefabGuid` is an editor hint
  (asset-library re-link) and is meaningless outside Unity.
- Component JSON shapes are owned by each component (schema `items` covers the folder layout and
  `item.json`). Unknown fields must be ignored; unknown `<TypeName>` folders must be skipped with
  a warning, not fail the import.
- References inside component JSON use the shape `{ "id": "<nodeId>", "type": "<TypeName>" }`;
  a null `id` means "no reference".
- Only active objects are exported. The cabinet and backbox are authored inactive but force-
  activated for export; a package always contains them active.

## global/

The logical wiring of the table: lists of switch, coil, wire and lamp mappings. Devices are
referenced by node id (`deviceId`). These files mirror VPE's `MappingConfig` and are restored
after items and refs.

## meta/materials.json

The portable material interchange. The top level of each profile is rendering *intent* any engine
can implement; everything pipeline-specific sits in a nested `hdrp` block (other blocks may be
added later) that readers are free to ignore.

```json
{
  "formatVersion": 2,
  "writtenBy": "HdrpMaterialTranslator",
  "profiles":        [ { "id", "name", "type", "lit|decal|unlit|metal|rubber|dmd|insertHybrid|insertCover": { … } } ],
  "textures":        [ { "id", "fileName", "mimeType", "colorSpace", "wrapMode", "filterMode",
                         "anisoLevel", "generateMipMaps", "runtimeCompress", "forceUncompressed", "editorOnly", "sourceName",
                         "width", "height" } ],
  "bindings":        [ { "nodeId", "slot", "profileId" } ],
  "rendererStates":  [ { "nodeId", "castShadows", "receiveShadows", "renderingLayerMask",
                         "hdrp": { "rayTracingMode" } } ]
}
```

**Profile types** (`type` discriminator; unknown types are skipped):

| Type | Payload | Notes |
| --- | --- | --- |
| `vpe.lit` | `lit` | Standard PBR surface. |
| `vpe.decal` | `decal` | Projected decal. |
| `vpe.unlit` | `unlit` | Unlit color/texture. |
| `vpe.metal`, `vpe.rubber` | `metal`/`rubber` + `lit` | Player-owned template (by `templateName`); the `lit` profile is the portable fallback. |
| `vpe.fabric.silk` | `fabric` | HDRP fabric/silk surface; contains a `lit` portable fallback plus fabric-specific HDRP controls. |
| `vpe.dmd` | `dmd` | Player-owned DMD template. |
| `vpe.insert.hybrid` | `insertHybrid` | Opaque insert body Shader Graph properties; the player supplies the pipeline shader. |
| `vpe.insert.cover` | `insertCover` | Transparent cover Shader Graph properties; the player supplies the pipeline shader. |

`bindings` identify a renderer node and material slot, so independently baked inserts can share a material name without sharing a profile. `editorOnly` textures are used only by insert authoring data and are skipped by the player before source loading and cooking. Optical response and optical normal maps remain 8-bit, retain their exported resolution, and are not block-compressed by the player cook.

**`lit` portable core** — semantics map onto glTF PBR and its KHR extensions where they exist:

- Base: `baseColor` (color + texture ref), `metallic`, `smoothness` (= 1 − roughness),
  `occlusionStrength`, `iridescenceMask`/`iridescenceThickness`.
- Mask: `maskMap` + `maskPacking` (`hdrpMaskMap`: R=metal G=AO B=detail A=smooth, or
  `gltfMetallicRoughness`: R=occlusion G=roughness B=metal) + `metallicRemap`/`smoothnessRemap`/
  `aoRemap`/`alphaRemap` (vec2 min/max).
- `normalMap`: texture ref + `strength` + `packing` (`rgb` | `rg` | `dxt5nm`) — packages always
  ship `rgb` sources.
- `emissive`: HDR color, optional LDR color, texture, `useIntensity` + `intensity` +
  `intensityUnit` (`nits` | `ev100` | `luminance`).
- Surface: `surfaceType` (`opaque` | `alphaTest` | `transparent`), `alphaCutoff`, `doubleSided`,
  `doubleSidedGi`, `blendMode` (`alpha` | `additive` | `premultiply`), `sortPriority`, `uvBase`.
- Transmission/refraction (≈ `KHR_materials_transmission` + `KHR_materials_volume` +
  `KHR_materials_ior`): `hasTransmission`, `refractionModel` (`none` | `planar` | `sphere` |
  `thin`), `ior`, `thickness` + `thicknessRemap` + `thicknessMap`, `absorptionDistance`,
  `transmittanceColor`.

**`lit.hdrp` hints** (ignored by non-HDRP readers, visuals degrade gracefully): planar-mapping
scales, specular AA/occlusion/color, clear-coat mask, decal support, cull-mode overrides,
transparent depth pre/post-pass, motion vectors, transparent preserve-specular blending, Z-test
overrides, fog, SSR flags, ray tracing, HDRP material id, render-queue override, transmission
scalars, diffusion-profile binding, emissive exposure weight.

**`fabric`** stores `{ "lit": { ... }, "hdrp": { ... } }`. `lit` is the portable fallback above.
`fabric.hdrp` carries HDRP/Fabric silk controls: `useThreadMap`, `threadMap`,
`threadAOStrength01`, `threadNormalStrength`, `threadSmoothnessScale`, `threadUvChannel`,
`fuzzMap`, `fuzzStrength`, and `fuzzMapUvScale`.

**Texture refs** are `{ "textureId", "offset": [u,v], "scale": [u,v] }`. A null/empty `textureId`
on a captured profile means "no texture"; on glTF-path refs it means "read pixels from the
imported glTF material".

**Enumerations** are strings, defined in this spec; the only ints left are quantities and the
explicitly Unity-valued fields inside `Hdrp` blocks.

## meta/inserts.json

This editor-only sidecar has `formatVersion: 1` and an `inserts` array keyed by `nodeId`. Each record stores the serialized generator fields as `generatorJson`, the pre-bake material profile id, and optional bake data: hash, owner, UVs, tangents, map texture ids, optical parameters, bake material profile id, packer, and UV fill. The editor imports these references as project assets and regenerates an editable insert mesh. Runtime readers never open this file; the player renders the finished insert meshes in `table.glb` and restores only runtime insert and lamp component state from `items/` and `refs/`.

## table/textures/ — the source layer

One plain image file per captured texture, usually the source bytes:

- `fileName` in the payload's texture table is the entry name; `id` is the stable reference key.
- Allowed types: `image/png`, `image/jpeg`. Sources that have no usable file (runtime-generated) are stored as PNG re-encodes of their pixels. A 16-bit PNG source is converted to 8-bit PNG on export.
- `width`/`height` carry the *authored* size (after any import clamp); larger source files are
  downsized to this when cooking.
- `colorSpace` (`sRGB` | `Linear`) is decided by the semantic slot (mask/thickness/normal data is
  linear), independent of the file's own metadata.
- GPU-ready payloads (block-compressed, mip-baked) are never stored in a package.

## meta/lights.json

`{ "version": 1, "lights": [ … ] }` — one entry per light source: `nodeId`, enabled state, type,
shape, color, intensity, range, spot angles, area size, shadow parameters, plus an `hdrp` block
(volumetrics, dimmers, ray-traced shadows). This is the authored truth for lights; the GLB's
`KHR_lights_punctual` is the portable approximation.

## sounds/ and meta/sounds.json

Original audio files plus a meta map keyed by file name:
`{ "guid", "forceToMono", "ambisonic", "loadInBackground", "loadType", "compressionFormat",
"quality" }`. `guid` is an opaque identity used to de-duplicate against existing project assets
on editor re-import; the import-intent fields are editor hints.

## colliders.glb and meta/colliders.json

Physics-only meshes that are not part of the visible scene, exported as a second GLB whose root
nodes are named `<guid>-<index>`. `colliders.json` maps each guid to its owning component and
prefab linkage.

## assets/

Shared `ScriptableObject`-style assets, grouped by registered type name, one JSON per asset plus
a `<name>.meta.json` carrying the instance id used by component references.

## content/ — inert directory bundles

Consumer-defined directory trees (for example a web application or an MPF machine) are stored as
bytes under `table/content/<content-id>/files/**`. VPE never executes a bundle. `<content-id>` is
the first 16 lowercase hexadecimal characters of the bundle's canonical SHA-256. The canonical
input is each file, sorted by ordinal forward-slash path, encoded as UTF-8 path + NUL + the raw
32-byte file SHA-256 + LF. Identical trees therefore deduplicate independent of source location,
timestamps, platform, or enumeration order. Empty directories are not represented.

`table/content/<content-id>/manifest.json` is normative:

```json
{
  "format": "vpe-content",
  "version": 1,
  "kind": "consumer-defined",
  "entryPoint": "relative/start.file",
  "fileCount": 2,
  "totalBytes": 1234,
  "contentHash": "<64 lowercase hex characters>",
  "files": [
    { "path": "relative/start.file", "size": 12, "sha256": "<64 lowercase hex characters>" }
  ]
}
```

`entryPoint` may be null. `files` is in ordinal path order and supplies the per-file size and hash
needed for streaming extraction verification. Writers reject absolute paths, drive prefixes,
empty/`.`/`..` segments, backslashes, colons (including NTFS alternate data streams), and paths
that differ only by case. Symbolic links and junctions are skipped with a warning.

Readers extract bundles to `<persistent-data>/ContentCache/<contentHash>/`. Every output path is
re-normalized and checked to remain under the temporary cache root, every byte count and SHA-256
is verified, and cancellation removes the temporary tree. Extraction happens in a sibling
`<contentHash>.tmp-<random>` directory; `.complete` is written last and the directory is then
renamed atomically. Only a final directory with a matching `.complete` marker may be reused.
Orphan temporary directories older than one hour are removed on resolver startup; younger ones are
left untouched so a concurrent resolver's in-progress extraction is never deleted. Completed entries are retained by
directory modification time under a configurable LRU cap (4 GiB by default).

## screenshots/

Generated preview renders (`*.jpg`), the user-supplied `backglass.jpg`, and `table-bounds.json`
(table crop bounds). Informational; not needed to load the table.

`thumbs/` holds small copies for table browsers, which show many tables at once and would
otherwise decode every 4K render: each preview render cropped to the table bounds and downscaled
to 768 px wide, and `backglass.jpg` downscaled to 1024 px wide when it is wider than that. They
carry the same file names as the full images. A reader falls back to the full image when a
thumbnail is missing (packages written before thumbnails, a small backglass) or narrower than it
needs, cropping it with `table-bounds.json` itself.

## Reader requirements

1. Parse `manifest.json`; refuse files without one, and refuse `FormatVersion` greater than
   supported.
2. Check each payload's schema version (manifest `Schemas` and the payload's own version field)
   before parsing; skip unknown versions with a warning. The runtime reader refuses a package whose
   `items` version is outside the range it loads, as described under *Versioning the items schema*.
3. Skip unknown component-type folders and unknown profile types; never fail the import for them.
4. Ignore unknown JSON fields everywhere.
5. Resolve all node references through the `vpeId` extras of `table.glb`.
6. Sanitize zip entry names before writing anything to disk.
7. Validate every content manifest, reject missing/oversized/duplicate/unsafe entries, and verify
   every content file's declared size and SHA-256 before publishing it to the content cache.

## Content-bundle linting and migration

`PackagedContentValidator.ValidatePackage()` is the normative integrity gate. It verifies the v1
manifest, canonical hash, declared and actual file sizes, per-file hashes, entry point, ordering,
path safety, file count, and total size without publishing an extraction. The resolver repeats the
size/hash/path checks while streaming into its temporary cache directory.

Authors can call `PackagedContentValidator.LintManifest()` before writing. In addition to manifest
validity it reports stable issue codes for a required-but-missing entry point, forbidden executable
extensions, duplicate byte payloads, and a configurable per-file size ceiling. Bundle consumers
must select policy explicitly: a browser or machine bundle normally requires an entry point and
forbids packaged executables because runtimes and native dependencies are player-owned. Python,
HTML, JavaScript, fonts, media, and WASM are data under this policy; executable native formats are
not. Warnings about duplicate bytes should be resolved or documented before release.

Readers accept exactly `vpe-content` version 1. Unknown content-manifest versions are rejected before
extraction. Optional manifest fields may be added within v1 only when old readers can ignore them
without changing integrity or path semantics. Any change to canonical hashing, path normalization,
content identity, required fields, or extraction rules requires a new version.

There is no older public content-bundle version to migrate. Future migration creates a new canonical
tree and manifest, which necessarily produces a new `contentHash` and cache location; readers never
rewrite a package or reuse the old identity. Migration tooling must validate source bytes, write the
new version into a separate package, run package validation and consumer-specific linting, then
resolve it into a clean cache as an end-to-end check.
