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
| `GenerateMipMaps` | Whether the current runtime should generate mips for the side texture. This is honored for both `sRGB` and `Linear` side textures. |
| `RuntimeCompress` | Whether the reader should call `Texture2D.Compress(...)` after decoding the side texture. Older packages default to `true`. |
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

The schema currently defines five material types:

| VPE type | Purpose |
| --- | --- |
| `vpe.lit` | Main physically based table materials, including transparency, transmission, and mask-based surface properties. |
| `vpe.decal` | Projected decal materials. |
| `vpe.unlit` | Unlit color-based materials. |
| `vpe.metal` | VPE metal shader graph materials. The profile stores a player template key and a `vpe.lit` fallback payload. |
| `vpe.rubber` | VPE rubber shader graph materials. The profile stores a player template key and a `vpe.lit` fallback payload. |

The HDRP translator maps these authoring shaders into those types:

| Authoring shader | VPE type |
| --- | --- |
| `HDRP/Lit` | `vpe.lit` |
| `HDRP/Decal` | `vpe.decal` |
| `HDRP/Unlit` | `vpe.unlit` |
| VPE metal shader graph | `vpe.metal` |
| VPE rubber shader graph | `vpe.rubber` |

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

`vpe.lit` is a reduced HDRP Lit material. The table below is written as a translation guide: if you are implementing another renderer, read the VPE field as "this should behave like the corresponding HDRP concept", not "this must be wired to Unity property X forever".

| VPE field | HDRP counterpart | HDRP documentation |
| --- | --- | --- |
| `BaseColor.Color`, `BaseColor.Texture`, `BaseColor.Texture.Scale`, `BaseColor.Texture.Offset` | HDRP Lit `Base Map`, including base tint, texture, tiling, and offset. The base-map alpha is also the source for transparency on transparent Lit materials. | [Lit Material Inspector reference: Base Map](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/lit-material-inspector-reference.html), [Surface Type](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Surface-Type.html) |
| `Metallic`, `Smoothness` | HDRP Lit scalar `Metallic` and `Smoothness` fallback values when no packed mask texture is driving those channels. | [Lit Material Inspector reference: Metallic / Smoothness](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/lit-material-inspector-reference.html) |
| `OcclusionStrength` | Closest to HDRP ambient occlusion contribution from the mask map. This is not a clean one-to-one HDRP inspector property in the current subset, so other renderers should treat it as the scalar AO contribution knob. | [Lit Material Inspector reference: Mask Map / Ambient Occlusion Remapping](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/lit-material-inspector-reference.html), [Mask and detail maps](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Mask-Map-and-Detail-Map.html) |
| `MaskMap`, `MaskPacking` | HDRP Lit `Mask Map`. In HDRP the packed layout is `R=Metallic`, `G=Ambient Occlusion`, `B=Detail Mask`, `A=Smoothness`. VPE keeps the packing explicit because other pipelines may not share HDRP's assumptions. | [Lit Material Inspector reference: Mask Map](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/lit-material-inspector-reference.html), [Mask and detail maps](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Mask-Map-and-Detail-Map.html) |
| `MetallicRemap`, `SmoothnessRemap`, `AoRemap`, `AlphaRemap` | HDRP Lit remap sliders for packed texture channels. | [Lit Material Inspector reference: Metallic / Smoothness / Ambient Occlusion / Alpha Remapping](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/lit-material-inspector-reference.html) |
| `NormalMap.Texture`, `NormalMap.Texture.Scale`, `NormalMap.Texture.Offset`, `NormalMap.Strength`, `NormalMap.Packing` | HDRP Lit `Normal Map`, `Normal Map Space`, and normal intensity. VPE's packing enum preserves source intent so runtime can convert or reinterpret it as needed. | [Lit Material Inspector reference: Normal Map](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/lit-material-inspector-reference.html), [HDRP Glossary: tangent space normal map / object space normal map](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Glossary.html) |
| `NormalMap.RuntimeCompress` | Whether HDRP should call `Texture2D.Compress(...)` after repacking an imported/runtime-loaded normal map into HDRP's expected layout. Older packages default to `true`. | No dedicated HDRP material page. |
| `Emissive.Color`, `Emissive.Texture`, `Emissive.Texture.Scale`, `Emissive.Texture.Offset`, `Emissive.Intensity`, `Emissive.IntensityUnit`, `Emissive.ExposureWeight` | HDRP Lit `Emission Map`, emission color, emission intensity, physical light units, and exposure weight. | [Lit Material Inspector reference: Emission inputs](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/lit-material-inspector-reference.html), [Physical light units](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Physical-Light-Units.html) |
| `SurfaceType` | HDRP `Surface Type` and the extra transparent workflow it unlocks. | [Surface Type](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Surface-Type.html) |
| `AlphaCutoff` | HDRP `Alpha Clipping > Threshold`. | [Alpha Clipping](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Alpha-Clipping.html) |
| `DoubleSided`, `DoubleSidedGi` | HDRP `Double Sided` and `Double-Sided GI`. | [Double sided](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Double-Sided.html), [Lit Material Inspector reference: Double-Sided GI](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/lit-material-inspector-reference.html) |
| `TransparentBlendMode` | HDRP transparent `Blending Mode`. | [Surface Type](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Surface-Type.html) |
| `EnableFogOnTransparent`, `TransparentDepthPrepass`, `TransparentDepthPostpass`, `TransparentWritesMotionVectors` | HDRP transparent-surface options for fog participation, depth pre/post passes, and transparent motion vectors. | [Surface Type](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Surface-Type.html), [Lit Material Inspector reference: Surface Options](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/lit-material-inspector-reference.html) |
| `DisableSsr`, `DisableSsrTransparent` | HDRP `Receive SSR` and `Receive SSR Transparent`. VPE names the field from the opt-out direction, HDRP names the inspector property from the opt-in direction. | [Lit Material Inspector reference: Receive SSR / Receive SSR Transparent](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/lit-material-inspector-reference.html) |
| `RenderQueueOverride` | Closest to HDRP sorting / render-pass override behavior. This is one of the places where VPE carries an explicit renderer-facing escape hatch rather than a pure material meaning. | [Surface Type](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Surface-Type.html) |
| `RefractionModel`, `Ior` | HDRP transparent refraction settings: `Refraction Model` and `Index of Refraction`. | [Surface Type](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Surface-Type.html) |
| `HasTransmission`, `Thickness`, `ThicknessMap` | HDRP transmission / translucent workflow. In HDRP this spans `Transmission`, `Thickness Map`, `Thickness`, and the `Diffusion Profile` asset. | [Material Type](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Material-Type.html), [Diffusion Profile reference](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/diffusion-profile-reference.html) |

### `vpe.decal`

`vpe.decal` is the decal-projector/material subset that VPE currently uses. HDRP exposes most of this through the Decal Material inspector.

| VPE field | HDRP counterpart | HDRP documentation |
| --- | --- | --- |
| `BaseColor.Color`, `BaseColor.Texture`, `BaseColor.Texture.Scale`, `BaseColor.Texture.Offset` | HDRP Decal `Base Map / Opacity`. In HDRP, the base-map alpha is part of the decal effect and also feeds other opacity-related controls. | [Decal Material Inspector reference: Base Map / Opacity](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/decal-material-inspector-reference.html) |
| `NormalMap.Texture`, `NormalMap.Texture.Scale`, `NormalMap.Texture.Offset`, `NormalMap.Strength`, `NormalMap.Packing` | HDRP Decal `Normal Map` plus its opacity-source behavior. VPE preserves the normal payload and strength; HDRP's extra opacity-source choices are currently hidden behind the resolver. | [Decal Material Inspector reference: Normal Map / Normal Opacity channel](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/decal-material-inspector-reference.html) |
| `MaskMap`, `MaskPacking` | HDRP Decal `Mask Map`. In HDRP decal materials the packed layout is `R=Metallic`, `G=Ambient Occlusion`, `B=Opacity Mask`, `A=Smoothness`. | [Decal Material Inspector reference: Mask Map](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/decal-material-inspector-reference.html), [Mask and detail maps](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Mask-Map-and-Detail-Map.html) |
| `AffectAlbedo`, `AffectNormal`, `AffectMask` | HDRP Decal `Affect BaseColor`, `Affect Normal`, and the mask-derived surface toggles (`Affect Metal`, `Affect Ambient Occlusion`, `Affect Smoothness`). VPE collapses those mask-derived toggles into one semantic switch today. | [Decal Material Inspector reference: Affect BaseColor / Affect Normal / Affect Metal / Affect Ambient Occlusion / Affect Smoothness](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/decal-material-inspector-reference.html) |
| `DecalBlend`, `NormalBlendSrc`, `MaskBlendSrc` | HDRP decal blending behavior. There is no tidy single inspector property with these exact names in the docs, so these should be read as VPE's explicit representation of HDRP decal blend-source behavior. | [Decals](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/decals.html), [Decal Material Inspector reference](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/decal-material-inspector-reference.html) |
| `Smoothness`, `Metallic`, `AmbientOcclusion` | HDRP Decal scalar controls for the corresponding channels. | [Decal Material Inspector reference: Metallic / Ambient Occlusion / Smoothness](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/decal-material-inspector-reference.html) |

### `vpe.unlit`

`vpe.unlit` stays deliberately small. It only covers the part of HDRP Unlit that VPE tables actually need today.

| VPE field | HDRP counterpart | HDRP documentation |
| --- | --- | --- |
| `BaseColor.Color`, `BaseColor.Texture`, `BaseColor.Texture.Scale`, `BaseColor.Texture.Offset` | HDRP Unlit `Color`, including the base texture, color multiplier, tiling, and offset. | [Unlit Material Inspector reference: Color](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/unlit-material-inspector-reference.html) |
| `SurfaceType` | HDRP Unlit `Surface type` and transparent workflow. | [Unlit Material Inspector reference: Surface type](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/unlit-material-inspector-reference.html), [Surface Type](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Surface-Type.html) |
| `AlphaCutoff` | HDRP Unlit `Alpha Clipping`. | [Unlit Material Inspector reference: Alpha Clipping](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/unlit-material-inspector-reference.html), [Alpha Clipping](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Alpha-Clipping.html) |
| `DoubleSided` | HDRP Unlit `Double-Sided`. | [Unlit Material Inspector reference: Double-Sided](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/unlit-material-inspector-reference.html), [Double sided](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Double-Sided.html) |

### `vpe.metal`

`vpe.metal` is used for VPE's measured metal shader graph materials. It exists because those materials are authored as VPE-owned shader graphs rather than plain HDRP Lit materials, while the package still needs a portable fallback for players that do not ship the same templates.

The profile contains:

| VPE field | Meaning |
| --- | --- |
| `Metal.TemplateName` | Stable template key owned by the player. HDRP resolves this through its measured-material override table and clones that template when available. |
| `Lit` | Fallback material intent built from the shader graph's exposed properties. The fallback includes color, mask map, metallic/smoothness/AO remaps, surface options, emissive settings, and renderer hints. |

For HDRP, `HdrpMaterialResolver` first tries to create the material from `Metal.TemplateName`. If no matching player template exists, it falls back to the `Lit` payload and builds a normal HDRP Lit material.

### `vpe.rubber`

`vpe.rubber` follows the same template-plus-fallback pattern as `vpe.metal`, but targets VPE's measured rubber shader graph materials.

The profile contains:

| VPE field | Meaning |
| --- | --- |
| `Rubber.TemplateName` | Stable template key owned by the player. HDRP resolves this through its measured-material override table and clones that template when available. |
| `Lit` | Fallback material intent built from the shader graph's exposed properties. The fallback includes base color, optional base color texture, normal map, mask map, smoothness/AO remaps, surface options, emissive settings, and renderer hints. |

For HDRP, `HdrpMaterialResolver` first tries to create the material from `Rubber.TemplateName`. If no matching player template exists, it falls back to the `Lit` payload and builds a normal HDRP Lit material.

### Renderer state

In addition to material properties, a renderer should restore `VpeRendererStateV1`. Only part of this has a dedicated HDRP manual page, because some of it is ordinary Unity renderer state rather than HDRP-specific shader behavior.

| VPE field | HDRP / Unity counterpart | HDRP documentation |
| --- | --- | --- |
| `ShadowCastingMode` | Unity `Renderer.shadowCastingMode`. This is renderer state, not an HDRP material feature. | No dedicated HDRP material page. |
| `ReceiveShadows` | Unity `Renderer.receiveShadows`. This is renderer state, not an HDRP material feature. | No dedicated HDRP material page. |
| `RenderingLayerMask` | HDRP rendering layers for light / decal filtering. | [Use light rendering layers](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Rendering-Layers.html) |

### Failure behavior

A package should remain loadable even if a renderer only implements part of the vocabulary. A good resolver therefore behaves conservatively:

- unknown material types should be skipped with a warning
- missing texture IDs should be skipped with a warning
- unsupported semantics should degrade gracefully rather than abort package loading
- if no resolver is registered, runtime should leave the imported GLB materials in place

That fallback behavior is not ideal visually, but it keeps the content usable while a renderer is still being brought up.
