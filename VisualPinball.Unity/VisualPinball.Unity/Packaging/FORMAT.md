# The .vpe Package Format

This is the normative specification of the `.vpe` table package format (format version 3). It
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
integrity.json               ← length and SHA-256 of every other entry
table/
├── table.glb                ← scene: hierarchy, meshes, lights, fallback materials (glTF 2.0 binary)
├── colliders.glb            ← physics-only meshes (glTF 2.0 binary, optional)
├── table.json               ← table metadata (title, authors, …)
├── provenance.json          ← table identity and the credits of the tables it derives from
├── items.json               ← state, component data and cross-references of every node
├── global/                  ← switch/coil/wire/lamp mappings
│   ├── switches.json
│   ├── coils.json
│   ├── wires.json
│   └── lamps.json
├── assets/<Type>/…          ← serialized shared assets + their meta
├── sounds/<file>            ← original audio files (wav/ogg/mp3/aiff)
├── rolling/graph.json       ← rolling-sound materials and what they are bound to (optional)
├── textures/<file>          ← image sources (png/jpg; 16-bit PNGs normalized to 8-bit)
│   └── _generated/<file>    ← textures the export made, like the baked ambient occlusion
└── meta/
    ├── materials.json       ← portable material payload and texture inventory
    ├── inserts.json         ← editor-only insert generator and bake state
    ├── inserts.bin          ← per-vertex arrays of the insert bake state (optional)
    ├── lights.json          ← light-source payload
    ├── sounds.json          ← audio meta (ids, import intent)
    └── colliders.json       ← collider-mesh meta
screenshots/                 ← preview images (jpg) + crop bounds (json, optional)
└── thumbs/                  ← small cropped copies of the preview images (jpg, optional)
```

Already-compressed payloads (the two GLBs, everything under `textures/`) are written as *stored*
zip entries; JSON entries are deflated.

The archive has no directory entries: a folder exists through the files in it, and an empty folder is not represented. Readers must not expect one and must accept archives that have them.

Entry timestamps are fixed (DOS epoch), so the archive adds nothing of its own to what is stored. Two exports of the same table are still not byte-identical: node ids and collider mesh ids are generated at each export, and `table.json` carries the export date.

JSON is written without indentation or line breaks, except that `items.json` has one node per line. No reader may depend on either.

Every key in the package's JSON is camelCase (`formatVersion`, `isActive`, `nodeId`), and private serialized fields lose their leading underscore (`_deviceId` is written `deviceId`). Keys of maps keyed by data (node ids, file names, payload names) are that data and keep their spelling. Packages of `items` schema 2 and older used PascalCase.

Optional inert consumer content is stored below `table/content/<content-id>/` with a
`manifest.json` and a `files/` directory. The normative layout and security rules are specified
in [content bundles](#content----inert-directory-bundles).

## manifest.json

The root-level manifest identifies the file and declares versions. It is the first thing a reader
parses; **a file without a manifest is not a valid .vpe package** and must be refused.

```json
{
  "formatVersion": 3,
  "writtenBy": "VPE 0.16.0 (Unity 6000.5.0f1)",
  "rootNodeId": "9f2c6c1e6a414b1f8d3a2b7c5e4f0a91",
  "schemas": { "items": 4, "itemsReader": 4, "materials": 2, "inserts": 4, "lights": 1, "sounds": 1, "rolling": 1 },
  "componentTypes": [ "Flipper", "KickerCoil", "Surface", "…" ]
}
```

| Field | Meaning |
| --- | --- |
| `formatVersion` | Container layout version. Readers must refuse versions other than the ones they implement. Version 3 moved the data of all nodes from one file per component (`items/`, `refs/`) into `items.json`. |
| `writtenBy` | Free-form writer identification, diagnostics only. |
| `rootNodeId` | Node id of the table root in `table.glb`. Readers anchor the restored component hierarchy here. |
| `schemas` | Schema version per payload, keyed by payload name. Readers must check a payload's schema version before parsing it and skip (with a warning) versions they don't know. `itemsReader` is not a payload: it is the oldest `items` version a reader must implement to load the package (see below). |
| `componentTypes` | All component/asset type names used by `items.json` and `assets/`. Lets a reader warn about missing plugins up front. |

**Versioning policy.** `formatVersion` changes only on incompatible container-layout changes
(folder structure, node addressing). Payload schemas evolve independently via `Schemas`. Within a
schema version, adding optional JSON fields is allowed; removing or re-typing a field requires a
version bump. Fields are never repurposed.

### Versioning the items schema

The `items` schema covers `items.json`: the component JSON, and what the runtime makes of
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
- Everything else in the package references nodes **by this id**: the nodes of `items.json`,
  cross-references, mapping devices, renderer states, light profiles.
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

## items.json

The state of every node and the data of its components, in one file:

```json
{"nodes":[
{"id":"c0ffee00deadbeef0123456789abcdef","item":{"name":"LeftFlipper","isActive":true,"isStatic":false,"prefabGuid":"…"},"components":[{"type":"Flipper","data":{…},"refs":{…}},{"type":"FlipperCollider"},{"type":"CoilSound","data":{…}},{"type":"CoilSound","data":{…},"refs":{…}}]},
{"id":"…","components":[{"type":"LightGroup","refs":{…}}]}
]}
```

A table has thousands of these records and most are under 200 bytes. As zip entries of their own, each cost more in zip headers than it held, and opening the package meant walking them all; in one file they are read and inflated once.

- `nodes` lists the nodes the package has something to say about, in the order they are restored: a parent before its children. `id` is the node id and occurs once; a file that names a node twice is invalid.
- `item` is the GameObject's state: `{ "name", "isActive", "isStatic", "prefabGuid", "prefabObject" }`. `prefabGuid` and `prefabObject` are editor hints for linking the asset library's prefabs again and are meaningless outside Unity: `prefabGuid` is set on the root of a prefab instance (the outermost one, or one added to another instance) and names its prefab, `prefabObject` is set on the other GameObjects of the instance and is the local file id, in that prefab, of the GameObject they are an instance of. The editor import matches them by this id, since an instance can rename its GameObjects. A node without `item` is active, not static and has no prefab.
- `components` lists the node's components in the order they are created. `type` is the component's registered pack name (PackAs attribute), decoupled from C# type names. **The n-th entry of a type on a node is the n-th component of exactly that type**: this is how two components of one type are told apart, and why a component that stores nothing still has an entry.
- `data` is what the component stores about itself. It is applied when the component is created, and stands for an empty object when left out.
- `refs` is the component's cross-references to other nodes, components and assets. All of them are applied in a second pass, after every component of every node exists. `data` and `refs` of one entry belong to the same component.
- `null` for `item`, `data` or `refs` means the same as leaving it out.
- The file is strict JSON (RFC 8259): no comments, no single quotes, no trailing commas, also inside `data` and `refs`. The writer refuses a payload that isn't, because what a lenient parser accepts, a reader that goes by the brackets may cut differently.
- The shapes of `data` and `refs` are owned by each component (schema `items` covers the file's structure and `item`). Unknown fields must be ignored. An entry of an unknown `type` must be skipped with a warning, not fail the import; it takes no place among the entries of the types the reader knows.
- References inside component JSON use the shape `{ "id": "<nodeId>", "type": "<TypeName>" }`; a null `id` means "no reference".
- Only active objects are exported. The cabinet and backbox are authored inactive but force-activated for export; a package always contains them active.

A reader can cut `item`, `data` and `refs` out of the file by their brackets and hand each to its component unparsed, so reading the file costs one pass over its bytes however many components it holds. VPE's reader (`PackagedItems`) does; it refuses a file whose structure is broken, and a component then parses, and may refuse, only its own payload.

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
- `occlusionMap` (≈ glTF `occlusionTexture`, optional): ambient occlusion the writer baked for this table, a grayscale texture whose red channel is the occlusion, in the material's UV space (UV channel `uvBase`, after the base map's tiling and offset, covering the unit square once). Readers sample it, like the mask it goes into, with the base map's tiling and offset (as HDRP and URP Lit sample the mask); the ref's own `scale` and `offset` are not used. Readers multiply it into the mask's occlusion channel, at the mask's authored size or the occlusion's, whichever has more texels; a profile without a mask takes it as its occlusion, with metallic and smoothness still from the scalars. It is derived data, made again by every export: editor imports drop it, so the authored mask stays editable.
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

This editor-only sidecar has `formatVersion: 4` and an `inserts` array keyed by `nodeId`. Each record stores the serialized generator fields as `generatorJson`, the pre-bake material profile id, and optional bake data: hash, owner, UVs, tangents, map texture ids, optical parameters, bake material profile id, packer, and UV fill. A custom-shaped insert also stores its source mesh as `customMesh`: its name, its vertices, and the topology and indices of each submesh. The editor imports these references as project assets and regenerates an editable insert mesh. Runtime readers never open this file; the player renders the finished insert meshes in `table.glb` and restores only runtime insert and lamp component state from `items.json`.

`generatorJson` is a string because it is what Unity's serializer wrote and reads back: its keys are the component's field names, which don't follow the naming of the package's JSON, and no other reader has a use for them.

The per-vertex arrays are not in the JSON. `uvs`, `tangents`, `customMesh.vertices` and each submesh's `indices` are `{ "byteOffset", "count" }`, the way a glTF accessor points into its buffer: `count` elements starting `byteOffset` bytes into `meta/inserts.bin`. The numbers there are little-endian and 32 bits wide: two floats per UV, four per tangent, three per vertex, one integer per index. A field that is left out has no array, and a package whose inserts have no arrays has no `inserts.bin`. As JSON, these numbers were nine tenths of the sidecar and took twice the room in the package.

## table/textures/ — the source layer

One plain image file per captured texture, usually the source bytes:

- `fileName` in the payload's texture table is the entry's path below `table/textures/`; `id` is the stable reference key. Authored textures are directly in the folder. What the export makes itself, like the baked `occlusionMap`, is in `_generated/` (`_generated/baked-occlusion.png`), so its names never collide with the authored ones. A path is made of `/`-separated names, none of them empty, `.` or `..`, and none with a `\`; the writer refuses to export a texture whose path isn't, and a package with one is malformed.
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

## integrity.json

`{ "version": 1, "entries": [ { "path", "length", "sha256" } ] }` lists every other entry of the archive, ordered by path, with its uncompressed length and the lowercase hexadecimal SHA-256 of its content. A package without it, or whose entries and their lengths don't match the archive's directory exactly, is refused when it is opened.

Opening a package checks the directory only: paths, lengths and that nothing is listed twice or missing. The hashes are checked where content is used for something that outlives the load (the texture and content caches key on them), and for the whole package on request (`VpeIntegrity.VerifyFile`), since hashing every entry reads the whole file. They detect change; they are not signatures.

## table/provenance.json

`{ "schemaVersion": 1, "tableId", "kind", "sourceFingerprint", "metadataHash", "creditIndexHash", "rootHash", "sources": [ … ], "credits": [ … ] }` identifies the table across its versions (`tableId`, a GUID kept from export to export) and carries the credits of the tables it was derived from. `sources` are the root hashes of those tables; each entry of `credits` names one of them with its `tableId`, `version`, authors and `kind`. The hashes commit to the table's metadata and credits, so that a derived table can't drop or rewrite where it came from without changing its own identity. A package without valid provenance is refused.

Table versions use `major.minor.patch` with an optional lowercase `v` prefix, SemVer prerelease identifiers after `-`, and build identifiers after `+` (for example `v1.0.0-alpha1+build.1`). Versions are limited to 128 characters and preserved exactly in metadata, ancestor credits and hashes; prefixes and suffixes are not normalized.

## sounds/ and meta/sounds.json

Original audio files plus a meta map keyed by file name:
`{ "guid", "forceToMono", "ambisonic", "loadInBackground", "loadType", "compressionFormat",
"quality" }`. `guid` is an opaque identity used to de-duplicate against existing project assets
on editor re-import; the import-intent fields are editor hints.

## rolling/graph.json

`{ "schema": 1, "balls": [ … ], "surfaces": [ … ], "bindings": [ … ] }`: the materials that give a rolling ball its sound. `balls` and `surfaces` are named materials with ids local to this file; a surface lists its responses per ball material, each pointing at the sound assets it loops and plays on entering and leaving. `bindings` attach a surface to a physics material under `assets/`. The file is optional, and a reader that doesn't know its schema (manifest `schemas.rolling`) loads the table without rolling sounds.

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

1. Parse `manifest.json`; refuse files without one, and refuse a `formatVersion` other than the
   ones implemented.
2. Check each payload's schema version (manifest `Schemas` and the payload's own version field)
   before parsing; skip unknown versions with a warning. The runtime reader refuses a package whose
   `items` version is outside the range it loads, as described under *Versioning the items schema*.
3. Skip entries of unknown component types and unknown profile types; never fail the import for them.
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
