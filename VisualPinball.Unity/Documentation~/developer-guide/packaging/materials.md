---
uid: developer-guide-packaging-materials
title: Packaging Materials
description: How VPE represents material intent beyond glTF and how a renderer consumes that contract.
---

# Packaging Materials

This page covers the material side of the `.vpe` format, for two readers:

- anyone trying to understand why materials are split between glTF and VPE metadata
- developers implementing a renderer for a different pipeline

## glTF versus Custom Materials

glTF carries the scene graph, meshes, transforms, lights, images, and a subset of physically based material data. For a mesh with a base color and a normal map, it is enough on its own.

Pinball tables need more than that: alpha-bearing inserts and plastics, HDRP mask packing, decals whose albedo alpha drives where they apply, and renderer state glTF has no way to describe. A `.vpe` file also has to stay table content rather than a dependency on HDRP — it describes what a material should do, not which Unity shader property the authoring project happened to set.

VPE therefore defines its own material vocabulary:

- export translates authoring materials into VPE's schema
- runtime reads that schema and asks the active renderer to realize it
- the renderer realizes it with HDRP, URP, or anything else

In code, that vocabulary lives in:

- `VpeMaterialsPayload`
- `VpeMaterialProfile`
- `VpeTexture`
- `VpeRendererState`

The schema is versioned by a `FormatVersion` field on the payload rather than by a type-name suffix; readers check it and skip versions they don't understand.

## glTF Data

The GLB carries the scene itself, plus a material fallback for shaders VPE does not translate.

| Concern | Where it lives | Why |
| --- | --- | --- |
| Scene hierarchy | `table.glb` | Native glTF responsibility. |
| Meshes and transforms | `table.glb` | Native glTF responsibility. |
| Lights | `table.glb` | Exported through glTF/glTFast light support. |
| Materials for **unsupported** shaders | Imported GLB material (+ its textures) | VPE has no profile for them, so the imported glTF material is the fallback. These are the only materials whose textures stay in the GLB. |

Earlier the GLB also carried the pixel data for "well-behaved" maps (opaque base color, emissive, normals) while only special maps were side-channeled. That split is gone: for every material VPE captures, **all** of its textures move to the source layer (`table/textures/`) and the GLB holds no image bytes for them. A profile that reads a texture from the imported GLB material (an *imported* texture reference) now only happens for those unsupported-shader fallbacks.

## VPE Data

VPE owns the data where glTF is lossy, underspecified for the feature, or too fragile in practice.

### Texture and State

| Authoring concern | VPE field(s) | Why it is not left to glTF |
| --- | --- | --- |
| HDRP `MaskMap` | `VpeLitProfile.MaskMap`, `MaskPacking` | HDRP mask packing is renderer-specific and not lossless in plain glTF. |
| HDRP `ThicknessMap` | `VpeLitProfile.ThicknessMap` | Thickness/transmission intent is outside plain glTF's core model. |
| Lit base color (incl. transparent / alpha-tested) | `VpeLitProfile.BaseColor.Texture` with `TextureId` | The alpha channel is load-bearing for inserts and plastics; the bytes ship in the source layer, not the GLB. |
| Decal base color | `VpeDecalProfile.BaseColor.Texture` with `TextureId` | The decal albedo alpha controls where the decal applies. |
| Decal mask map | `VpeDecalProfile.MaskMap` | Same problem as HDRP mask packing: the meaning is renderer-specific. |
| Per-renderer shadow and lighting state | `VpeRendererState` | glTF does not carry Unity's shadow casting mode, receive-shadows flag, or rendering layer mask. |

### Storage

Those VPE-owned textures are serialized as `VpeTexture` entries, and their bytes ship as plain PNG/JPEG files under `table/textures/` — one zip entry per texture, stored uncompressed. There is no packed blob and no byte-range index: the entry points at its file by name and the zip central directory does the rest.

Each entry records:

| Field | Meaning |
| --- | --- |
| `Id` | Stable logical identifier used by `TextureId`. |
| `FileName` | The texture's file name inside `table/textures/`. |
| `MimeType` | Declared encoding of the stored bytes (`image/png` or `image/jpeg`). |
| `ColorSpace` | `sRGB` or `Linear`. |
| `WrapMode`, `FilterMode`, `AnisoLevel` | Texture sampling intent. |
| `GenerateMipMaps` | Whether the runtime should generate/keep mips for this texture. Honored for both `sRGB` and `Linear`. |
| `RuntimeCompress` | Whether a reader *without* a cook path should call `Texture2D.Compress(...)` after decoding. Defaults to `true`. |
| `Width`, `Height` | Source dimensions (after any importer max-size clamp). |

## Ownership Model

Each authored material becomes one logical VPE profile, keyed by normalized material name. The profile contains semantic intent; it does not try to preserve the authoring shader as data.

There are two texture-reference modes in the schema:

| Reference style | Shape | Runtime behavior |
| --- | --- | --- |
| Imported texture ref | `TextureId = null` | Read the texture from the imported GLB material. Only used for unsupported-shader fallback materials. |
| Source texture ref | `TextureId != null` | Resolve the texture through its `VpeTexture` entry and load the file from `table/textures/` (cooked through the local cache). This is the path captured materials use. |

This split is what keeps the schema portable. A profile can record "base color texture for a transparent insert" without binding that data to a particular HDRP shader property.

## Supported Material Types

The schema currently defines eight material types:

| VPE type | Purpose |
| --- | --- |
| `vpe.lit` | Main physically based table materials, including transparency, transmission, and mask-based surface properties. |
| `vpe.decal` | Projected decal materials. |
| `vpe.unlit` | Unlit color-based materials. |
| `vpe.metal` | VPE metal shader graph materials. The profile stores a player template key and a `vpe.lit` fallback payload. |
| `vpe.rubber` | VPE rubber shader graph materials. The profile stores a player template key and a `vpe.lit` fallback payload. |
| `vpe.dmd` | Dot-matrix display materials. The profile stores a player template key, with no `vpe.lit` fallback. |
| `vpe.fabric.silk` | HDRP fabric/silk materials. The profile carries a `vpe.lit` fallback plus HDRP thread/fuzz hints. |
| `vpe.insert` | Playfield-insert parts (lens and reflector). The profile carries a part role plus a full `vpe.lit` payload. |

The HDRP translator maps these authoring shaders into those types:

| Authoring shader | VPE type |
| --- | --- |
| `HDRP/Lit` | `vpe.lit` |
| `HDRP/Decal` | `vpe.decal` |
| `HDRP/Unlit` | `vpe.unlit` |
| HDRP fabric/silk shader | `vpe.fabric.silk` |
| VPE metal shader graph | `vpe.metal` |
| VPE rubber shader graph | `vpe.rubber` |
| VPE DMD shader graph | `vpe.dmd` |

`vpe.insert` is not selected by shader name. It is a *promotion* of `vpe.lit`: an `HDRP/Lit` material is re-typed as an insert when the translator's scene heuristic identifies it as one (see the `vpe.insert` spec below). The captured data is the same lit payload either way.

Unsupported shaders are not fatal: they produce no VPE profile, and runtime falls back to the imported GLB material for them.

## Runtime Resolution

The package never instantiates pipeline-specific materials directly. Instead, runtime performs a translation step:

1. `VpeMaterialReader` parses `materials.json`.
2. It resolves source textures from `table/textures/`, cooking them through the local cache.
3. It matches imported materials by normalized name.
4. It calls the active `IVpeMaterialResolver`.

The last step is the pipeline-specific one. For HDRP, `HdrpMaterialResolver` clones authored template materials, applies VPE intent onto them, and lets HDRP reconcile the resulting state.

The package stays portable; the player owns the final rendering behavior.

Because a resolver builds these materials at runtime, the shader variants it produces are not present on any build-time material. A player that ships standalone builds must therefore preload those variants, or runtime-resolved materials render with the wrong variant in the build even though resolution succeeds. See [Shader Variants](shader-variants.md).

## Specs

This section is the renderer-facing contract — the part to read if you are implementing a new pipeline. It is a subset of HDRP and will grow as table builds start using more attributes. The tables link to the relevant HDRP documentation.

### Inputs a renderer must consume

A resolver implementation must be able to work with:

| Input | Purpose |
| --- | --- |
| `VpeMaterialsPayload` | Top-level material payload. |
| `VpeMaterialProfile` | Per-material semantic intent. |
| `VpeTexture` | Metadata for a source texture file. |
| Imported fallback `Material` | Access point for textures on unsupported-shader fallback materials. |

### Matching

Profiles are matched to imported materials by normalized material name. A renderer should therefore treat the imported material name as the lookup key and degrade gracefully when there is no match.

### Texture-source contract

A renderer must support both texture-source modes:

| Mode | How to detect it | What to do |
| --- | --- | --- |
| Imported | `TextureId == null` | Read the texture from the imported material using aliases appropriate for the pipeline. |
| Source | `TextureId != null` | Resolve the `VpeTexture`, load its file from `table/textures/`, create a texture, and apply the stored sampling/color-space settings. |

### `vpe.lit`

`vpe.lit` is a reduced HDRP Lit material. Read the table as a translation guide: each VPE field names a behavior to reproduce, not a Unity property another renderer is required to bind to.

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

`vpe.unlit` is small: it covers only the part of HDRP Unlit that VPE tables use.

| VPE field | HDRP counterpart | HDRP documentation |
| --- | --- | --- |
| `BaseColor.Color`, `BaseColor.Texture`, `BaseColor.Texture.Scale`, `BaseColor.Texture.Offset` | HDRP Unlit `Color`, including the base texture, color multiplier, tiling, and offset. | [Unlit Material Inspector reference: Color](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/unlit-material-inspector-reference.html) |
| `SurfaceType` | HDRP Unlit `Surface type` and transparent workflow. | [Unlit Material Inspector reference: Surface type](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/unlit-material-inspector-reference.html), [Surface Type](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Surface-Type.html) |
| `AlphaCutoff` | HDRP Unlit `Alpha Clipping`. | [Unlit Material Inspector reference: Alpha Clipping](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/unlit-material-inspector-reference.html), [Alpha Clipping](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Alpha-Clipping.html) |
| `DoubleSided` | HDRP Unlit `Double-Sided`. | [Unlit Material Inspector reference: Double-Sided](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/unlit-material-inspector-reference.html), [Double sided](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Double-Sided.html) |

### `vpe.metal`

`vpe.metal` covers VPE's measured metal shader-graph materials. They are authored as VPE-owned shader graphs rather than plain HDRP Lit materials, so the profile carries both a template key and a portable `vpe.lit` fallback for players that do not ship the same templates.

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

### `vpe.dmd`

`vpe.dmd` covers dot-matrix display materials, which are VPE-owned shader graphs.

| VPE field | Meaning |
| --- | --- |
| `Dmd.TemplateName` | Stable template key owned by the player. HDRP clones the matching template when available. |

Unlike `vpe.metal`/`vpe.rubber`, `vpe.dmd` carries **no** `vpe.lit` fallback: a DMD has no meaningful Lit approximation, so if the player ships no matching template the material is skipped rather than substituted.

### `vpe.fabric.silk`

`vpe.fabric.silk` covers HDRP fabric/silk materials. The profile follows the fallback pattern but adds fabric-specific HDRP hints.

| VPE field | Meaning |
| --- | --- |
| `Fabric.Lit` | Portable `vpe.lit` fallback for readers without an HDRP fabric implementation. |
| `Fabric.Hdrp` | HDRP fabric/silk hints: optional thread map (with AO/normal/smoothness strengths and UV channel) and fuzz map (with strength and UV scale). |

For HDRP, `HdrpMaterialResolver` clones the fabric/silk template and applies the hints; if no fabric template is configured it returns no material (the imported fallback then stays in place). Other pipelines can render the carried `vpe.lit` fallback.

### `vpe.insert`

`vpe.insert` marks the two physical parts of a playfield insert: the *lens* flush with the playfield surface, and the faceted *reflector* body below it that shapes the lamp light. In HDRP both parts are ordinary translucent Lit materials, so the profile carries no data beyond a plain `vpe.lit` payload — the semantic type exists for pipelines **without** HDRP's translucency, diffusion profiles, and refraction. Knowing "this is an insert lens" lets such a pipeline substitute a purpose-built approximation instead of a generic (and hopeless) translation of a 10%-alpha transmissive surface.

| VPE field | Meaning |
| --- | --- |
| `Insert.Part` | `lens` or `reflector`. |
| `Insert.Lit` | Full portable material intent, identical in shape to a `vpe.lit` payload (transmittance color, thickness map, IOR, refraction model, normal map, surface state, HDRP hints). |

**Classification** happens at export time, by heuristic rather than by shader: a transparent, transmissive `HDRP/Lit` material used by a renderer that sits under a `LightComponent` is an insert part. The part with a refraction model is the reflector; the one without is the lens. Opaque emissive materials under the same light (faux bulbs) are unaffected and stay `vpe.lit`.

**Color identity caveat for readers:** in authored tables, a *reflector's* saturated color is its transmittance tint, while a *lens* usually carries its color in the base color — lens transmittance is often a generic value shared across all insert colors. A renderer approximating inserts should pick its tint source per part accordingly.

**Realization:**

- HDRP resolves `Insert.Lit` through exactly the same path as `vpe.lit` (translucent template plus hints); output is identical to the pre-`vpe.insert` behavior.
- URP builds both parts as alpha-blended URP/Lit materials: the tint becomes the visible body color (with a per-part opacity floor), and the lamp glow becomes emission whose baseline the runtime lamp system (`LightComponent`) reads at startup, zeroes, and then drives with the live lamp value. Because the parts stay in the transparent render queue, the lamp system treats them as lamp-lit *surfaces* (emission drive only) rather than faux bulbs (which are hidden when the lamp is off).
- A resolver that does not implement `vpe.insert` should skip it; runtime then keeps the imported GLB material, like any unsupported type.

### Renderer state

In addition to material properties, a renderer should restore `VpeRendererState`. Only part of this has a dedicated HDRP manual page, because some of it is ordinary Unity renderer state rather than HDRP-specific shader behavior.

| VPE field | HDRP / Unity counterpart | HDRP documentation |
| --- | --- | --- |
| `CastShadows` | Unity `Renderer.shadowCastingMode`. This is renderer state, not an HDRP material feature. | No dedicated HDRP material page. |
| `ReceiveShadows` | Unity `Renderer.receiveShadows`. This is renderer state, not an HDRP material feature. | No dedicated HDRP material page. |
| `RenderingLayerMask` | HDRP rendering layers for light / decal filtering. | [Use light rendering layers](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.6/manual/Rendering-Layers.html) |
| `Hdrp.RayTracingMode` | HDRP per-renderer ray-tracing mode override (`-1` = no override). | No dedicated HDRP material page. |

### Failure behavior

A package should stay loadable even if a renderer implements only part of the vocabulary, so a resolver should behave conservatively:

- unknown material types should be skipped with a warning
- missing texture IDs should be skipped with a warning
- unsupported semantics should degrade gracefully rather than abort package loading
- if no resolver is registered, runtime should leave the imported GLB materials in place

That degrades the visuals, but keeps the content loadable while a renderer is still being brought up.
