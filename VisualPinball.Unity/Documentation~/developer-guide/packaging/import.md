---
uid: developer-guide-packaging-import
title: Packaging Import
description: How runtime loading reconstructs a playable table from a .vpe package.
---

# Packaging Import

The runtime import entry point is `RuntimePackageReader`.

Its job is to reconstruct a playable table from the package without relying on Unity editor asset import. That sounds obvious, but it is the reason the import path looks different from editor re-import: runtime cannot lean on the asset database to quietly sort things out later.

## High-level Flow

Runtime loading is built around one rule:

1. import the GLB
2. restore everything else against the imported hierarchy

That gives the importer a concrete transform tree and renderer set before packables, refs, globals, and material restoration run.

## GLB import

`RuntimePackageReader` reads `table.glb` from the ZIP storage and imports it directly from memory through `GltfImport`.

This produces:

- the scene hierarchy
- renderer components
- imported fallback materials
- imported textures that remain on the glTF path

If present, the importer also probes the optional `VPE_materials` GLB extension before scene instantiation. That code exists for experiments and future work, but normal exported packages use the sidecar path instead.

## Post-GLB Restoration

After the hierarchy exists, runtime import restores:

- sounds
- assets
- collider meshes
- packables from `items/`
- references from `refs/`
- globals
- table metadata from `table.json`
- material profiles from `materials.v1.json`

## Material Restore

`VpeMaterialV1Reader` owns the material-restore pass.

It reads:

- `meta/materials.v1.json`
- `meta/textures.bin`

It then:

- matches imported materials by normalized material name
- creates runtime materials through the active `IVpeMaterialResolver`
- reuses resolved materials aggressively to avoid rebuilding equivalent materials
- restores per-renderer state such as shadow casting and rendering layers

After this step, the table is supposed to look like authored in the editor.

## Texture Loading

For side-channel textures, `VpeMaterialV1Reader.TextureProvider`:

1. locates the `VpeTextureAssetV1`
2. slices the corresponding byte range from `textures.bin`
3. creates a `Texture2D`
4. decodes the bytes with `ImageConversion.LoadImage(...)`
5. applies wrap/filter/aniso settings
6. caches the texture for the remainder of the import

If the package uses embedded GLB texture blobs instead, runtime prefers those over `textures.bin`.

The reason this is called out explicitly is performance: texture decode and upload became one of the main bottlenecks once the more obvious duplication issues were removed.

## Mipmapping Behavior

Side-channel mip behavior is intentionally asymmetric:

- side textures currently honor the `GenerateMipMaps` flag for both `sRGB` and `Linear` payloads
- linear side textures are decoded as linear textures
- when `RuntimeCompress` is true, linear side textures are runtime-compressed with `Texture2D.Compress(highQuality: true)` before they are made non-readable

The table export inspector exposes a `Compress sidecar textures (Unity runtime compression)` toggle. It sets `VpeTextureAssetV1.RuntimeCompress` for every side-channel texture in the package, so developers can export compressed and uncompressed sidecar packages from the same table and compare visual/runtime behavior. Packages written before this field existed default to `true`.

The `Compress glTF textures` toggle is separate. It affects textures that stay on the `table.glb` path, such as opaque base color maps and normal maps. When disabled, export rewrites matching GLB image payloads back to the original PNG/JPEG asset bytes after glTFast has produced the GLB.

The `Compress runtime normal maps (Unity runtime compression)` toggle controls the HDRP resolver's normal-map repack output. The resolver must repack GLB/runtime-loaded RGB normal textures into the layout HDRP expects; this flag decides whether the repacked texture is then compressed with Unity's runtime texture compressor.

Benchmark work showed that skipping runtime mip generation for heavy linear side textures can save load time, because the heaviest linear payloads are mostly mask and thickness data. The current reader still follows the serialized flag, so package authors and renderer implementers should treat `GenerateMipMaps` as the runtime contract.

## HDRP Resolver Details

The HDRP implementation of `IVpeMaterialResolver` is `HdrpMaterialResolver`. It:

- clones pre-authored HDRP template materials
- applies VPE material intent onto those templates
- restores transmission, mask packing, decals, and renderer-specific state
- repacks RGB normal maps into the layout HDRP expects
- resolves `vpe.metal` and `vpe.rubber` by cloning player-shipped measured-material templates, falling back to their carried `vpe.lit` payload when a template is unavailable

The important performance optimization here is that normal repack uses a GPU path via `VpePackNormalForHdrp.shader`, with a CPU fallback only if that path fails.

## Shader Variants in Player Builds

Because the resolver flips HDRP/Lit `shader_feature` keywords at runtime, the variants it produces exist on no build-time material. The editor compiles them on demand, but a standalone build ships only the `shader_feature` combinations referenced by a build-time material or by a preloaded `ShaderVariantCollection`. A player therefore has to ship a captured collection, or runtime-resolved materials render with the wrong variant (broken transparency, refraction, and reflections) even though import succeeds. See [Shader Variants](shader-variants.md) for why this happens and how to keep the collection current as tables are added.

## Dependencies

Runtime import assumes:

- `table.glb` is valid and self-consistent
- `materials.v1.json` matches material names emitted into the GLB
- `textures.bin` byte ranges are valid
- a compatible `IVpeMaterialResolver` is registered by the player

If a resolver is not registered, runtime falls back to the glTF-imported materials and the table will not match authoring visuals.
