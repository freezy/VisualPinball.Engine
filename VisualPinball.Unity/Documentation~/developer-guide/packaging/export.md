---
uid: developer-guide-packaging-export
title: Packaging Export
description: How a table is written into the .vpe package structure.
---

# Packaging Export

This page explains what gets written into a `.vpe` package and why each part exists. It is deliberately organized by package structure, not by the exact call order inside `PackageWriter`, because that is usually the more useful mental model when you are trying to understand or extend the format.

The export entry point is `PackageWriter`. Export runs asynchronously (`PackageWriter.WritePackageAsync`) and is driven from the editor by `VpeExportRunner` behind a cancelable progress bar: the writer yields between stages and offloads texture byte-loading to worker threads, so the editor stays responsive and the user can cancel a long export.

## Scene Payload

The visible scene is exported to `table/table.glb` through [Unity's fork of glTFast](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.18/manual/index.html).

The GLB contains:

- hierarchy
- transforms
- meshes
- lights
- imported fallback materials (for shaders VPE does not translate)
- textures **only** for those unsupported-shader fallback materials

For materials VPE captures, the GLB carries no image bytes at all — those textures move to the source layer (see [Texture Ownership](#texture-ownership)). The GLB also does not contain:

- component packables
- cross-reference wiring
- globals
- editor assets
- table metadata
- VPE material vocabulary
- the VPE-owned texture source files

### Scene Preparation

The exporter does a little housekeeping before it hands the table to glTFast. The point of this step is to make sure the GLB contains the scene the player actually needs, not the slightly awkward authoring-time version of it.

- table meshes are made readable
- author-time disabled `Light` components are enabled so they flow into `KHR_lights_punctual`
- invalid mesh renderers are suppressed so glTF export does not fail
- a temporary material-sanitizing scope (`IVpeMaterialGltfExportPreprocessor`) strips the textures VPE captures into the source layer, so the GLB does not duplicate those bytes
- after glTFast writes the GLB, the writer swaps glTFast's re-encoded images back to the original asset bytes (`GlbImageSwap`), keeping the textures that *do* remain in the GLB lossless

## Collider Payload

Physics-only meshes are exported to `table/colliders.glb`. Related metadata is written to `table/meta/colliders.json`, including:

- prefab linkage
- whether the collider mesh was overridden
- path within the prefab

This lets editor re-import reconnect those meshes correctly.

## Packables and References

Gameplay and authoring data is split into two trees:

- `table/items/` - contains the data needed to instantiate and configure components.
- `table/refs/` - contains data that restores cross-references after the hierarchy and components already exist.

The split exists because some data can be applied immediately, while other data only makes sense after the whole object/component graph has been rebuilt.

## Table Metadata

Table-level metadata is written to `table/table.json` as a `TableMetadata` object: table name, abbreviation, primary and secondary authors, release date, original release year, and manufacturer. Captured table screenshots are not part of this file — they are written separately, under the top-level `screenshots/` folder, alongside a `table-bounds.json` crop sidecar.

## Globals, Assets, and Sounds

The remaining package content is the non-scene part of the table. These files are small compared to the GLB and texture payloads, but they are what make the table playable rather than just renderable.

- `table/global/` - switches, coils, lamps and wires
- `table/assets/` - serialized `ScriptableObject` assets plus metadata
- `table/sounds/` - sound bytes
- `table/meta/sounds.json` - sound lookup metadata

## Material Payload

If an `IVpeMaterialTranslator` is registered and captures material data, export writes:

- `table/meta/materials.json` - a `VpeMaterialsPayload` with material profiles, texture metadata, and per-renderer state not covered by glTF. It carries a `FormatVersion` so readers can skip versions they don't understand.
- `table/textures/<file>` - one plain PNG/JPEG **source file per texture**, stored (not deflated). These are the original asset bytes, shipped losslessly: no re-encode and no block compression at export time.

Each texture is described by a `VpeTexture` entry in the payload. Instead of a byte range, the entry references its file by name (`FileName`) — the zip central directory *is* the offset table. Each entry also records:

- `Id` (the value `VpeTextureRef.TextureId` points at)
- `MimeType` (`image/png` or `image/jpeg`)
- `ColorSpace` (`sRGB` or `Linear`)
- `WrapMode`, `FilterMode`, `AnisoLevel`
- `GenerateMipMaps`
- `RuntimeCompress` (a hint for readers that load the source without a cook path)
- `Width`, `Height`

The important detail is that texture metadata and texture bytes are separate. `materials.json` tells runtime what a texture means and where it belongs; the file under `table/textures/` carries the bytes. Texture byte-loading at export is deferred and run on worker threads (`VpeTextureBlobSource` / `VpeTextureBlobLoader`), so disk reads and any PNG-16→8 conversion stay off the main thread.

The table inspector exposes two texture-compression toggles. Because the package now ships lossless sources, both only affect the **fallback** runtime path — a reader that decodes the source itself instead of using the cooked cache (see [import](import.md)):

- `Compress sidecar textures (Unity runtime compression)` sets `RuntimeCompress` on every `VpeTexture`, so a non-cooking reader block-compresses decoded linear textures.
- `Compress runtime normal maps (Unity runtime compression)` sets `RuntimeCompress` on RGB-packed normal references, so a non-cooking reader block-compresses normals after repacking them.

The older `Compress glTF textures` toggle is gone: captured-material textures no longer live in the GLB, so there is nothing to compress there.

### Texture Ownership

The exporter splits texture ownership by **material**, not by individual map. For every material the active translator recognizes — HDRP/Lit, HDRP/Decal, HDRP/Unlit, and VPE's metal, rubber, DMD and fabric/silk shader graphs — **all** of its textures (base color, normal, mask, emissive, thickness, and so on) are written to the source layer (`table/textures/`) and referenced by `TextureId` from the profile. The GLB carries no image bytes for those materials.

Textures stay in the GLB only for **unsupported** shaders — materials the translator does not recognize and therefore does not capture (it calls `RegisterUnsupportedMaterial` for them). Those keep their textures on the imported glTF path as a visual fallback, with the original asset bytes swapped back in (`GlbImageSwap`) so that path stays lossless too.

This is the change that makes runtime import fast: the GLB path does no PNG decode for captured materials, and the player cooks the source textures once into GPU-ready data and caches them locally (see [import](import.md) and [benchmarks](benchmarks.md)).

### VPE Shader Graph Materials

The translator has special handling for VPE's own shader graphs, which plain glTF cannot represent:

- metal shader-graph materials export as `vpe.metal`
- rubber shader-graph materials export as `vpe.rubber`
- DMD (dot-matrix display) materials export as `vpe.dmd`
- HDRP fabric/silk materials export as `vpe.fabric.silk`

`vpe.metal`, `vpe.rubber` and `vpe.dmd` store a `TemplateName` (in their `VpeShaderGraphProfile` block) so the player can clone the matching measured-material template. `vpe.metal` and `vpe.rubber` additionally populate the profile's sibling `Lit` field with a `vpe.lit` fallback derived from the exposed shader-graph properties; `vpe.fabric.silk` carries its own `vpe.lit` fallback plus HDRP thread/fuzz hints.

At runtime, an HDRP player should prefer the template path when it ships the same measured-material library. If the template is missing, `vpe.metal` and `vpe.rubber` fall back to their carried Lit payload so the package stays renderable. `vpe.fabric.silk` also carries a Lit fallback for renderers without fabric support, but the HDRP resolver needs the fabric template specifically. `vpe.dmd` has no Lit fallback — without its template the DMD material is skipped.
