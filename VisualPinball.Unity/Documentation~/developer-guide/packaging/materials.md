---
uid: developer-guide-packaging-materials
title: Packaging Materials
description: How VPE represents material intent beyond glTF and how a renderer consumes that contract.
---

# Packaging Materials

This page explains the material side of the `.vpe` format. It is written for two audiences:

- people trying to understand why materials are split between glTF and VPE metadata
- developers who want to implement a renderer for a different pipeline

## glTF versus Custom Materials

glTF already gives VPE a lot for free. It knows how to carry a scene graph, meshes, transforms, lights, images, and a useful subset of physically based material data. If we only cared about "show me a mesh with a base color and a normal map", plain glTF would be enough.

Pinball tables are messier than that. They rely on alpha-bearing inserts and plastics, HDRP-specific mask packing, decals whose albedo alpha is part of the effect, and a handful of renderer-state details that glTF does not describe. At the same time, a `.vpe` file must remain table content, not a hard dependency on HDRP. The package should describe what the material is supposed to do, not which Unity shader property happened to be used by the authoring project.

That is why VPE has its own material vocabulary:

- export translates authoring materials into VPE's schema
- runtime reads that schema and asks the active renderer to realize it
- the renderer stays free to use HDRP, URP, or something else entirely

In code, that vocabulary lives in:

- `VpeMaterialsPayloadV1`
- `VpeMaterialProfileV1`
- `VpeTextureAssetV1`
- `VpeRendererStateV1`

## glTF Data

The GLB is not just a fallback, it's a real part of the material system. We deliberately keep any data on the glTF path that round-trips well enough to be worth it.

| Concern | Where it lives | Why glTF is sufficient here |
| --- | --- | --- |
| Scene hierarchy | `table.glb` | Native glTF responsibility. |
| Meshes and transforms | `table.glb` | Native glTF responsibility. |
| Lights | `table.glb` | Exported through glTF/glTFast light support. |
| Opaque lit base color | Imported GLB material | Usually survives the standard glTF path without semantic loss. |
| Emissive textures | Imported GLB material | Standard enough to keep on the glTF path. |
| Most unlit color maps | Imported GLB material | Standard enough to keep on the glTF path. |
| Supported normal maps | Imported GLB material | Visually correct in the current shipping path, even though HDRP needs a runtime repack step. |

When a texture stays on the GLB path, the VPE profile still owns the semantic meaning of that slot. What it does not own is the pixel payload. In those cases, the profile stores tiling or strength information, and runtime reads the actual texture from the imported material.

## VPE Data

VPE takes ownership where glTF is either lossy, underspecified for the feature, or too fragile in practice.

### Texture and State

| Authoring concern | VPE field(s) | Why it is not left to glTF |
| --- | --- | --- |
| HDRP `MaskMap` | `VpeLitProfileV1.MaskMap`, `MaskPacking` | HDRP mask packing is renderer-specific and not lossless in plain glTF. |
| HDRP `ThicknessMap` | `VpeLitProfileV1.ThicknessMap` | Thickness/transmission intent is outside plain glTF's core model. |
| Transparent / alpha-tested lit base color | `VpeLitProfileV1.BaseColor.Texture` with `TextureId` | The alpha channel is load-bearing for inserts and plastics and cannot be treated as an optional detail. |
| Decal base color | `VpeDecalProfileV1.BaseColor.Texture` with `TextureId` | The decal albedo alpha controls where the decal applies. |
| Decal mask map | `VpeDecalProfileV1.MaskMap` | Same problem as HDRP mask packing: the meaning is renderer-specific. |
| Per-renderer shadow and lighting state | `VpeRendererStateV1` | glTF does not carry Unity's shadow casting mode, receive-shadows flag, or rendering layer mask. |

### Storage

Those VPE-owned textures are serialized as `VpeTextureAssetV1` entries and their bytes are packed into `meta/textures.bin`.

Each asset records:

| Field | Meaning |
| --- | --- |
| `Id` | Stable logical identifier used by `TextureId`. |
| `ByteOffset` / `ByteLength` | Where the texture bytes live inside `textures.bin`. |
| `GlbBufferView` | Optional alternative storage location if the payload is embedded into GLB buffer views. |
| `MimeType` | Declared encoding of the stored bytes. |
| `ColorSpace` | `sRGB` or `Linear`. |
| `WrapMode`, `FilterMode`, `AnisoLevel` | Texture sampling intent. |
| `GenerateMipMaps` | Whether runtime should generate mips for the side texture. |
| `Width`, `Height` | Source dimensions. |

## Ownership Model

Each authored material becomes one logical VPE profile, keyed by normalized material name. The profile contains semantic intent; it does not try to preserve the authoring shader as data.

There are two texture-reference modes in the schema:

| Reference style | Shape | Runtime behavior |
| --- | --- | --- |
| Imported texture ref | `TextureId = null` | Read the texture from the imported GLB material. |
| Side-channel texture ref | `TextureId != null` | Resolve the texture through `VpeTextureAssetV1` and load bytes from `textures.bin` or embedded GLB data. |

This is the heart of the design. It lets VPE say, for example, "this is the base color texture for a transparent insert" without also saying "therefore you must use this HDRP shader property bag forever".

## Supported Material Types

The schema currently defines three material types:

| VPE type | Purpose |
| --- | --- |
| `vpe.lit` | Main physically based table materials, including transparency, transmission, and mask-based surface properties. |
| `vpe.decal` | Projected decal materials. |
| `vpe.unlit` | Unlit color-based materials. |

The HDRP translator maps these authoring shaders into those types:

| Authoring shader | VPE type |
| --- | --- |
| `HDRP/Lit` | `vpe.lit` |
| `HDRP/Decal` | `vpe.decal` |
| `HDRP/Unlit` | `vpe.unlit` |

Unsupported shaders are not fatal. They simply do not produce a VPE profile, and runtime falls back to the imported GLB material for them.

## Runtime Resolution

The package never instantiates pipeline-specific materials directly. Instead, runtime performs a translation step:

1. `VpeMaterialV1Reader` parses `materials.v1.json`.
2. It resolves side-channel textures from `textures.bin`.
3. It matches imported materials by normalized name.
4. It calls the active `IVpeMaterialResolver`.

That last step is where the render pipeline comes back into the picture. For HDRP, `HdrpMaterialResolver` clones authored template materials, applies VPE intent onto them, and lets HDRP reconcile the resulting state.

That separation keeps the package portable while letting the player own the final rendering behavior.

## Specs

This section is the renderer-facing contract. If you are implementing a new pipeline, this is the part to read carefully. Note that this is a subset of HDRP. We'll most likely add new attributes once we use them in a table build. In the tables below you'll see links to the HDRP documentation.

### Inputs a renderer must consume

A resolver implementation must be able to work with:

| Input | Purpose |
| --- | --- |
| `VpeMaterialsPayloadV1` | Top-level material payload. |
| `VpeMaterialProfileV1` | Per-material semantic intent. |
| `VpeTextureAssetV1` | Metadata for side-channel texture bytes. |
| Imported fallback `Material` | Access point for textures that stay on the GLB path. |

### Matching

Profiles are matched to imported materials by normalized material name. A renderer should therefore treat the imported material name as the lookup key and degrade gracefully when there is no match.

### Texture-source contract

A renderer must support both texture-source modes:

| Mode | How to detect it | What to do |
| --- | --- | --- |
| Imported | `TextureId == null` | Read the texture from the imported material using aliases appropriate for the pipeline. |
| Side-channel | `TextureId != null` | Resolve the `VpeTextureAssetV1`, load the bytes, create a texture, and apply the stored sampling/color-space settings. |

### `vpe.lit`

An implementation of `vpe.lit` should support the following fields.

| Field | Meaning | What the renderer should do |
| --- | --- | --- |
| `BaseColor.Color` | Base albedo tint | Apply as the base color multiplier. |
| `BaseColor.Texture` | Base albedo texture | Use imported or side-channel source depending on `TextureId`. |
| `Metallic` | Scalar metallic fallback/remap anchor | Apply directly or use as part of mask remap behavior. |
| `Smoothness` | Scalar smoothness fallback/remap anchor | Apply directly or use as part of mask remap behavior. |
| `OcclusionStrength` | Occlusion contribution | Apply if the renderer supports AO strength. |
| `MaskMap` | Packed surface-property texture | Interpret according to `MaskPacking`. |
| `MaskPacking` | Declares channel meaning | Respect the declared packing; do not guess. |
| `MetallicRemap`, `SmoothnessRemap`, `AoRemap`, `AlphaRemap` | Channel remap ranges | Apply when unpacking the mask map. |
| `NormalMap.TextureId` / transform / `Strength` / `Packing` | Normal map intent | Support the declared normal packing and apply strength. |
| `Emissive.Color`, `Emissive.Texture`, `Intensity`, `IntensityUnit`, `ExposureWeight` | Emissive behavior | Map into the renderer's emissive model as closely as possible. |
| `SurfaceType` | Opaque, alpha test, or transparent | Choose the right material family and pass state. |
| `AlphaCutoff` | Alpha-test threshold | Apply when `SurfaceType` is alpha test. |
| `DoubleSided`, `DoubleSidedGi` | Double-sided behavior | Enable the renderer's double-sided handling. |
| `TransparentBlendMode` | Transparent blend intent | Map to the renderer's blend mode if supported. |
| `EnableFogOnTransparent`, `TransparentDepthPrepass`, `TransparentDepthPostpass`, `TransparentWritesMotionVectors` | Transparent rendering hints | Apply where the pipeline exposes equivalent behavior. |
| `DisableSsrTransparent`, `DisableSsr` | SSR hints | Respect if the pipeline exposes equivalent toggles. |
| `RenderQueueOverride` | Explicit queue override | Apply only when non-negative. |
| `RefractionModel`, `Ior` | Refraction behavior | Map to the pipeline's refraction model if available. |
| `HasTransmission`, `Thickness`, `ThicknessMap` | Transmission and thickness | Support if the pipeline can represent it; otherwise degrade predictably. |

### `vpe.decal`

| Field | Meaning | What the renderer should do |
| --- | --- | --- |
| `BaseColor.Color` and `BaseColor.Texture` | Decal color and alpha coverage | Apply as the projected decal albedo. |
| `NormalMap` | Decal normal contribution | Support if the renderer exposes decal normals. |
| `MaskMap` and `MaskPacking` | Packed decal surface properties | Interpret according to the declared packing. |
| `AffectAlbedo`, `AffectNormal`, `AffectMask` | Feature toggles | Respect which surface contributions are enabled. |
| `DecalBlend`, `NormalBlendSrc`, `MaskBlendSrc` | Blend controls | Map to the renderer's decal blending model. |
| `Smoothness`, `Metallic`, `AmbientOcclusion` | Scalar decal properties | Apply if the renderer supports them. |

### `vpe.unlit`

| Field | Meaning | What the renderer should do |
| --- | --- | --- |
| `BaseColor.Color` and `BaseColor.Texture` | Unlit color and texture | Use as the final color source. |
| `SurfaceType` | Opaque, alpha test, or transparent | Choose the right material family or pass state. |
| `AlphaCutoff` | Alpha-test threshold | Apply when relevant. |
| `DoubleSided` | Double-sided rendering | Enable if supported. |

### Renderer state

In addition to material properties, a renderer should restore `VpeRendererStateV1`:

| Field | Meaning |
| --- | --- |
| `ShadowCastingMode` | Whether the renderer casts no shadows, normal shadows, two-sided shadows, or shadows only. |
| `ReceiveShadows` | Whether the renderer receives shadows. |
| `RenderingLayerMask` | Unity rendering-layer mask used for light-layer-style filtering. |

### Failure behavior

A package should remain loadable even if a renderer only implements part of the vocabulary. A good resolver therefore behaves conservatively:

- unknown material types should be skipped with a warning
- missing texture IDs should be skipped with a warning
- unsupported semantics should degrade gracefully rather than abort package loading
- if no resolver is registered, runtime should leave the imported GLB materials in place

That fallback behavior is not ideal visually, but it keeps the content usable while a renderer is still being brought up.