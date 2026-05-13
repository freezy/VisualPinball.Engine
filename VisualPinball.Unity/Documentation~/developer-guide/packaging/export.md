---
uid: developer-guide-packaging-export
title: Packaging Export
description: How a table is written into the .vpe package structure.
---

# Packaging Export

This page explains what gets written into a `.vpe` package and why each part exists. It is deliberately organized by package structure, not by the exact call order inside `PackageWriter`, because that is usually the more useful mental model when you are trying to understand or extend the format.

The export entry point is `PackageWriter`.

## Scene Payload

The visible scene is exported to `table/table.glb` through [Unity's fork of glTFast](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.18/manual/index.html).

The GLB contains:

- hierarchy
- transforms
- meshes
- lights
- imported fallback materials
- textures that remain on the glTF path

The GLB does not contain:

- component packables
- cross-reference wiring
- globals
- editor assets
- table metadata
- VPE material vocabulary
- packed VPE-only texture bytes

### Scene Preparation

The exporter does a little housekeeping before it hands the table to glTFast. The point of this step is to make sure the GLB contains the scene the player actually needs, not the slightly awkward authoring-time version of it.

- table meshes are made readable
- author-time disabled `Light` components are enabled so they flow into `KHR_lights_punctual`
- invalid mesh renderers are suppressed so glTF export does not fail
- a temporary material-sanitizing scope removes texture data that VPE already owns elsewhere

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

Table-level metadata is written to `table/table.json`. This contains things like table name, manufacturer and authors, and will be extended in the future.

## Globals, Assets, and Sounds

The remaining package content is the non-scene part of the table. These files are small compared to the GLB and texture payloads, but they are what make the table playable rather than just renderable.

- `table/global/` - switches, coils, lamps and wires
- `table/assets/` - serialized `ScriptableObject` assets plus metadata
- `table/sounds/` - sound bytes
- `table/meta/sounds.json` - sound lookup metadata

## Material Payload

If a `IVpeMaterialV1Translator` is registered and captures material data, export writes:

- `table/meta/materials.v1.json` - contains material profiles, texture metadata, per-renderer state not covered by glTF
- `table/meta/textures.bin` - contains raw concatenation of VPE-owned texture blobs.

Each `VpeTextureAssetV1` records the byte range for its payload as `ByteOffset` and `ByteLength`.

The exporter also records:

- color space
- wrap/filter/aniso settings
- mip intent
- runtime compression intent
- dimensions
- MIME type

The important detail here is that texture metadata and texture bytes are separate. `materials.v1.json` tells runtime what a texture means and where it belongs; `textures.bin` carries the bytes.

The table inspector includes two texture compression toggles:

- `Compress sidecar textures (Unity runtime compression)` writes `RuntimeCompress` into each side-channel texture asset, allowing developers to compare packages where the reader compresses decoded linear side textures against packages where it keeps those decoded textures uncompressed.
- `Compress glTF textures` controls images that remain inside `table.glb`. When disabled, the writer post-processes the GLB and replaces matching glTF image payloads with the original PNG/JPEG asset bytes.
- `Compress runtime normal maps (Unity runtime compression)` writes `RuntimeCompress` onto normal-map references, allowing developers to compare HDRP's repacked normal maps with and without Unity runtime texture compression.

### Texture Ownership

The exporter intentionally splits texture ownership. Some textures stay on the GLB path while some textures are side-channeled into `textures.bin`

Textures that typically stay on the GLB path:

- opaque lit base color
- emissive
- most unlit color maps
- supported normal maps

Textures that are side-channeled:

- HDRP `MaskMap`
- HDRP `ThicknessMap`
- alpha-bearing lit base color maps
- decal base color maps
- decal mask maps
- VPE metal shader graph mask maps
- VPE rubber shader graph base color and mask maps

The reason is not convenience but correctness. Those maps either do not fit cleanly into glTF, rely on semantics that the fallback GLB path cannot preserve reliably, or come from VPE-specific shader graph slots that plain glTF does not understand.

### VPE Shader Graph Materials

The HDRP translator has special handling for VPE's measured metal and rubber shader graphs:

- metal shader graph materials export as `vpe.metal`
- rubber shader graph materials export as `vpe.rubber`
- each profile stores a `TemplateName` so the player can clone the matching measured-material template
- each profile also carries a `vpe.lit` fallback payload derived from exposed shader graph properties

At runtime, an HDRP player should prefer the template path when it has the same measured-material library available. If the template is missing, the carried Lit fallback keeps the package renderable.
