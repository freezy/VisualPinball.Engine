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
- imported fallback materials (for unsupported shaders)
- imported textures for those fallback materials only — captured materials carry no images in the GLB

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
- material profiles from `materials.json` (restored last, against the fully built hierarchy)

## Material Restore

`VpeMaterialReader` owns the material-restore pass.

It reads:

- `meta/materials.json`
- the source textures under `table/textures/`, cooked through the local cache (see [Texture Loading](#texture-loading))

It then:

- matches imported materials by normalized material name
- creates runtime materials through the active `IVpeMaterialResolver`
- reuses resolved materials aggressively to avoid rebuilding equivalent materials
- restores per-renderer state such as shadow casting and rendering layers

After this step, the table is supposed to look like authored in the editor.

## Texture Loading

Textures are the heaviest part of the load, so this is where most of the runtime work goes.

The package ships **lossless source files** (`table/textures/`), not GPU-ready data. On first load the player *cooks* the sources and writes the result to a per-table on-disk cache; later loads read straight from that cache.

**First (uncached) load** — `VpeTextureSources` reads the PNG/JPEG entries from `table/textures/` into a transient in-memory blob, then `VpeTextureCook` turns them into GPU-ready payloads:

1. decode on worker threads with StbImageSharp (very large files decode on the main thread via `ImageConversion.LoadImage`)
2. generate mips on the GPU
3. encode to BC7 on the GPU (DirectXTex compute shaders)
4. repack normal maps into the dxt5nm layout HDRP expects
5. persist the cooked payloads to the cache

**Cached load** — the cache is validated, then the cooked BC7 payloads are uploaded directly, with no decode, mip generation, or encode.

The cache lives under `Application.persistentDataPath/TextureCache/`, one file per package, keyed on the package path. Its header records the package file size and last-write time plus a hash of the cook settings (`VpeTextureCookSettings`: `ResolutionDivisor`, `CompressTextures`, format version), so it invalidates automatically when the package or the cook parameters change. Payloads are zstd-compressed in independent chunks for parallel decompression, and the format discriminator is `VpeTexturePayload.PixelFormat` (BC7 / DXT5 / RGBA32) — a payload already holding GPU-ready blocks is uploaded raw (`isRawPayload`).

To keep a heavy table from stalling or crashing the GPU, the cook and upload run under per-frame budgets (a pixel budget for BC7 encode, a byte budget for uploads) and yield between batches.

If a platform cannot cook (no GPU BC7 support), the reader falls back to decoding the source itself: it builds a `Texture2D` via `ImageConversion.LoadImage`, honoring `GenerateMipMaps`, and — for linear textures whose `RuntimeCompress` flag is set — block-compresses with `Texture2D.Compress(highQuality: true)` before making the texture non-readable. This fallback path is what the export compression toggles target.

## Mipmapping Behavior

`GenerateMipMaps` is part of the package contract: it says whether a given texture should have mips at runtime. The cook bakes mips into the cached payload accordingly, and the non-cooking fallback path honors the same flag when it calls `Apply`. Export can disable mips for the classes that don't need them — typically the heavy linear mask/thickness payloads — so the decision lives with the package rather than the reader.

`RuntimeCompress` only matters on the fallback path: the cook already produces block-compressed (BC7) payloads when `CompressTextures` is on. Treat `GenerateMipMaps` as the runtime contract regardless of which path a reader takes.

## HDRP Resolver Details

The HDRP implementation of `IVpeMaterialResolver` is `HdrpMaterialResolver`. It:

- clones pre-authored HDRP template materials
- applies VPE material intent onto those templates
- restores transmission, mask packing, decals, and renderer-specific state
- repacks RGB normal maps (the source packing) into the dxt5nm layout HDRP expects, optionally block-compressing the result when the normal's `RuntimeCompress` flag is set
- resolves `vpe.metal`, `vpe.rubber` and `vpe.dmd` by cloning player-shipped measured-material templates; `vpe.metal`/`vpe.rubber` fall back to their carried `vpe.lit` payload when a template is unavailable, and `vpe.fabric.silk` resolves a fabric template with a `vpe.lit` fallback
- consumes already-cooked textures from the reader — it does not decode or cook anything itself

The resolver's own normal repack currently runs on the **CPU**. An earlier GPU repack shader (`VpePackNormalForHdrp`) was removed; the heavy normal work now happens once during the texture cook (which has its own GPU repack path) and is cached, so the resolver only repacks normals that arrive through the non-cooked or imported-GLB path.

## Shader Variants in Player Builds

Because the resolver flips HDRP/Lit `shader_feature` keywords at runtime, the variants it produces exist on no build-time material. The editor compiles them on demand, but a standalone build ships only the `shader_feature` combinations referenced by a build-time material or by a preloaded `ShaderVariantCollection`. A player therefore has to ship a captured collection, or runtime-resolved materials render with the wrong variant (broken transparency, refraction, and reflections) even though import succeeds. See [Shader Variants](shader-variants.md) for why this happens and how to keep the collection current as tables are added.

## Dependencies

Runtime import assumes:

- `table.glb` is valid and self-consistent
- `materials.json` matches material names emitted into the GLB
- every `VpeTexture.FileName` resolves to an entry under `table/textures/`
- a compatible `IVpeMaterialResolver` is registered by the player

If a resolver is not registered, runtime falls back to the glTF-imported materials and the table will not match authoring visuals.
