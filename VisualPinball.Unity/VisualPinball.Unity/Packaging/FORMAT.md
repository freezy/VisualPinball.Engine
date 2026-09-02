# The .vpe Package Format

This is the normative specification of the `.vpe` table package format (format version 1). It
describes what a conforming writer produces and what any reader — VPE or not — needs to know to
unpack and load a table. The implementation notes live in [README.md](README.md).

Two hard constraints shape the format:

1. **Lossless** — a package re-imports into the editor without quality loss. Sources are stored
   as original bytes; nothing is re-encoded on the way in.
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
├── textures/<file>          ← original image files (png/jpg), the lossless source layer
└── meta/
    ├── materials.json       ← portable material payload
    ├── lights.json          ← light-source payload
    ├── sounds.json          ← audio meta (ids, import intent)
    └── colliders.json       ← collider-mesh meta
screenshots/                 ← preview images (jpg) + crop bounds (json, optional)
```

Already-compressed payloads (the two GLBs, everything under `textures/`) are written as *stored*
zip entries; JSON entries are deflated. Entry timestamps are fixed (DOS epoch) so identical
content produces byte-identical packages.

Optional inert consumer content is stored below `table/content/<content-id>/` with a
`manifest.json` and a `files/` directory. The normative layout and security rules are specified
in [content bundles](#content----inert-directory-bundles).

## manifest.json

The root-level manifest identifies the file and declares versions. It is the first thing a reader
parses; **a file without a manifest is not a valid .vpe package** and must be refused.

```json
{
  "FormatVersion": 1,
  "WrittenBy": "VPE 0.16.0 (Unity 6000.5.0f1)",
  "RootNodeId": "9f2c6c1e6a414b1f8d3a2b7c5e4f0a91",
  "Schemas": { "items": 1, "materials": 1, "lights": 1, "sounds": 1 },
  "ComponentTypes": [ "Flipper", "KickerCoil", "Surface", "…" ]
}
```

| Field | Meaning |
| --- | --- |
| `FormatVersion` | Container layout version. Readers must refuse versions greater than they implement. |
| `WrittenBy` | Free-form writer identification, diagnostics only. |
| `RootNodeId` | Node id of the table root in `table.glb`. Readers anchor the restored component hierarchy here. |
| `Schemas` | Schema version per payload, keyed by payload name. Readers must check a payload's schema version before parsing it and skip (with a warning) versions they don't know. |
| `ComponentTypes` | All component/asset type names used by `items/`, `refs/` and `assets/`. Lets a reader warn about missing plugins up front. |

**Versioning policy.** `FormatVersion` changes only on incompatible container-layout changes
(folder structure, node addressing). Payload schemas evolve independently via `Schemas`. Within a
schema version, adding optional JSON fields is allowed; removing or re-typing a field requires a
version bump. Fields are never repurposed.

## Node identity

Every exported GameObject becomes a glTF node in `table.glb`, and every such node carries a
stable id in the standard glTF per-node extension point:

```json
{ "name": "LeftFlipper", "mesh": 12, "extras": { "vpeId": "c0ffee00deadbeef0123456789abcdef" } }
```

- Ids are 32-char lowercase hex GUIDs, unique within the package, generated at export.
- Everything else in the package references nodes **by this id**: item folders, cross-references,
  mapping devices, renderer states, light profiles.
- The id of the table root is repeated in the manifest as `RootNodeId`.
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
- `item.json`: `{ "Name", "IsActive", "IsStatic", "PrefabGuid" }`. `PrefabGuid` is an editor hint
  (asset-library re-link) and is meaningless outside Unity.
- Component JSON shapes are owned by each component (schema `items` covers the folder layout and
  `item.json`). Unknown fields must be ignored; unknown `<TypeName>` folders must be skipped with
  a warning, not fail the import.
- References inside component JSON use the shape `{ "Id": "<nodeId>", "Type": "<TypeName>" }`;
  a null `Id` means "no reference".
- Only active objects are exported. The cabinet and backbox are authored inactive but force-
  activated for export; a package always contains them active.

## global/

The logical wiring of the table: lists of switch, coil, wire and lamp mappings. Devices are
referenced by node id (`_deviceId`). These files mirror VPE's `MappingConfig` and are restored
after items and refs.

## meta/materials.json

The portable material interchange. The top level of each profile is rendering *intent* any engine
can implement; everything pipeline-specific sits in a nested `Hdrp` block (other blocks may be
added later) that readers are free to ignore.

```json
{
  "FormatVersion": 1,
  "WrittenBy": "HdrpMaterialTranslator",
  "Profiles":        [ { "Name", "Type", "Lit|Decal|Unlit|Metal|Rubber|Dmd": { … } } ],
  "Textures":        [ { "Id", "FileName", "MimeType", "ColorSpace", "WrapMode", "FilterMode",
                         "AnisoLevel", "GenerateMipMaps", "RuntimeCompress", "SourceName",
                         "Width", "Height" } ],
  "RendererStates":  [ { "NodeId", "CastShadows", "ReceiveShadows", "RenderingLayerMask",
                         "Hdrp": { "RayTracingMode" } } ]
}
```

**Profile types** (`Type` discriminator; unknown types are skipped):

| Type | Payload | Notes |
| --- | --- | --- |
| `vpe.lit` | `Lit` | Standard PBR surface. |
| `vpe.decal` | `Decal` | Projected decal. |
| `vpe.unlit` | `Unlit` | Unlit color/texture. |
| `vpe.metal`, `vpe.rubber` | `Metal`/`Rubber` + `Lit` | Player-owned template (by `TemplateName`); the `Lit` profile is the portable fallback. |
| `vpe.fabric.silk` | `Fabric` | HDRP fabric/silk surface; contains a `Lit` portable fallback plus fabric-specific HDRP controls. |
| `vpe.dmd` | `Dmd` | Player-owned DMD template. |

**`Lit` portable core** — semantics map onto glTF PBR and its KHR extensions where they exist:

- Base: `BaseColor` (color + texture ref), `Metallic`, `Smoothness` (= 1 − roughness),
  `OcclusionStrength`, `IridescenceMask`/`IridescenceThickness`.
- Mask: `MaskMap` + `MaskPacking` (`hdrpMaskMap`: R=metal G=AO B=detail A=smooth, or
  `gltfMetallicRoughness`: R=occlusion G=roughness B=metal) + `MetallicRemap`/`SmoothnessRemap`/
  `AoRemap`/`AlphaRemap` (vec2 min/max).
- `NormalMap`: texture ref + `Strength` + `Packing` (`rgb` | `rg` | `dxt5nm`) — packages always
  ship `rgb` sources.
- `Emissive`: HDR color, optional LDR color, texture, `UseIntensity` + `Intensity` +
  `IntensityUnit` (`nits` | `ev100` | `luminance`).
- Surface: `SurfaceType` (`opaque` | `alphaTest` | `transparent`), `AlphaCutoff`, `DoubleSided`,
  `DoubleSidedGi`, `BlendMode` (`alpha` | `additive` | `premultiply`), `SortPriority`, `UvBase`.
- Transmission/refraction (≈ `KHR_materials_transmission` + `KHR_materials_volume` +
  `KHR_materials_ior`): `HasTransmission`, `RefractionModel` (`none` | `planar` | `sphere` |
  `thin`), `Ior`, `Thickness` + `ThicknessRemap` + `ThicknessMap`, `AbsorptionDistance`,
  `TransmittanceColor`.

**`Lit.Hdrp` hints** (ignored by non-HDRP readers, visuals degrade gracefully): planar-mapping
scales, specular AA/occlusion/color, clear-coat mask, decal support, cull-mode overrides,
transparent depth pre/post-pass, motion vectors, transparent preserve-specular blending, Z-test
overrides, fog, SSR flags, ray tracing, HDRP material id, render-queue override, transmission
scalars, diffusion-profile binding, emissive exposure weight.

**`Fabric`** stores `{ "Lit": { ... }, "Hdrp": { ... } }`. `Lit` is the portable fallback above.
`Fabric.Hdrp` carries HDRP/Fabric silk controls: `UseThreadMap`, `ThreadMap`,
`ThreadAOStrength01`, `ThreadNormalStrength`, `ThreadSmoothnessScale`, `ThreadUvChannel`,
`FuzzMap`, `FuzzStrength`, and `FuzzMapUvScale`.

**Texture refs** are `{ "TextureId", "Offset": [u,v], "Scale": [u,v] }`. A null/empty `TextureId`
on a captured profile means "no texture"; on glTF-path refs it means "read pixels from the
imported glTF material".

**Enumerations** are strings, defined in this spec; the only ints left are quantities and the
explicitly Unity-valued fields inside `Hdrp` blocks.

## table/textures/ — the source layer

One plain image file per captured texture, stored, never re-encoded:

- `FileName` in the payload's texture table is the entry name; `Id` is the stable reference key.
- Allowed types: `image/png`, `image/jpeg`. Sources that have no usable file (runtime-generated)
  are stored as lossless PNG re-encodes of their pixels.
- `Width`/`Height` carry the *authored* size (after any import clamp); larger source files are
  downsized to this when cooking.
- `ColorSpace` (`sRGB` | `Linear`) is decided by the semantic slot (mask/thickness/normal data is
  linear), independent of the file's own metadata.
- GPU-ready payloads (block-compressed, mip-baked) are never stored in a package.

## meta/lights.json

`{ "Version": 1, "Lights": [ … ] }` — one entry per light source: `NodeId`, enabled state, type,
shape, color, intensity, range, spot angles, area size, shadow parameters, plus an `Hdrp` block
(volumetrics, dimmers, ray-traced shadows). This is the authored truth for lights; the GLB's
`KHR_lights_punctual` is the portable approximation.

## sounds/ and meta/sounds.json

Original audio files plus a meta map keyed by file name:
`{ "Guid", "ForceToMono", "Ambisonic", "LoadInBackground", "LoadType", "CompressionFormat",
"Quality" }`. `Guid` is an opaque identity used to de-duplicate against existing project assets
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
Orphan temporary directories are removed on resolver startup. Completed entries are retained by
directory modification time under a configurable LRU cap (4 GiB by default).

## screenshots/

Generated preview renders (`*.jpg`), the user-supplied `backglass.jpg`, and `bounds.json` (table
crop bounds). Informational; not needed to load the table.

## Reader requirements

1. Parse `manifest.json`; refuse files without one, and refuse `FormatVersion` greater than
   supported.
2. Check each payload's schema version (manifest `Schemas` and the payload's own version field)
   before parsing; skip unknown versions with a warning.
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
