// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using UnityEngine;

namespace VisualPinball.Unity
{
	// vpe.material v1 is the portable material interchange schema carried inside a .vpe package.
	//
	// Design goals:
	// - Describes rendering *intent* (base color, normal, mask packing, emissive, alpha mode, ...),
	//   not a shader-specific property bag. Keyword strings and HDRP-specific property names do not
	//   appear anywhere in this schema.
	// - Readable by a Player app built years later against a different Unity / SRP version. The Player
	//   registers an IVpeMaterialResolver that maps these intents onto shaders it owns at its own
	//   build time.
	// - Extensible via a `Type` discriminator. Unknown types are skipped with a warning.
	//
	// When adding a new material type, add a new sibling property on VpeMaterialProfileV1 and a new
	// Type constant. Never repurpose an existing field.

	public static class VpeMaterialTypes
	{
		public const string Lit = "vpe.lit";
		public const string Decal = "vpe.decal";
		public const string Unlit = "vpe.unlit";
		public const string Metal = "vpe.metal";
		public const string Rubber = "vpe.rubber";
		public const string Dmd = "vpe.dmd";
	}

	public static class VpeColorSpaces
	{
		public const string SRgb = "sRGB";
		public const string Linear = "Linear";
	}

	public static class VpeNormalPackings
	{
		// R,G,B store X,Y,Z as an RGB PNG. Shader reconstructs as normalize(rgb * 2 - 1).
		public const string Rgb = "rgb";
		// R,G store X,Y; Z is reconstructed. Matches Unity's tangent-space normal sampling.
		public const string Rg = "rg";
		// Dxt5nm swizzle (A,G store X,Y). Emitted when source was a compressed Unity normal map.
		public const string Dxt5nm = "dxt5nm";
	}

	public static class VpeMaskPackings
	{
		// HDRP MaskMap: R=metallic, G=AO, B=detailMask, A=smoothness.
		public const string HdrpMaskMap = "hdrpMaskMap";
		// glTF metallicRoughness + occlusion (R=occlusion, G=roughness, B=metallic).
		public const string GltfMetallicRoughness = "gltfMetallicRoughness";
	}

	public static class VpeSurfaceTypes
	{
		public const string Opaque = "opaque";
		public const string AlphaTest = "alphaTest";
		public const string Transparent = "transparent";
	}

	public static class VpeRefractionModels
	{
		public const string None = "none";
		// Flat card, refracts through a planar slab. HDRP equivalent: _REFRACTION_PLANE.
		public const string Planar = "planar";
		// Sphere-like (bumper caps, thick hard plastics). HDRP: _REFRACTION_SPHERE.
		public const string Sphere = "sphere";
		// Thin sheet (ramp plastics, lenses). HDRP: _REFRACTION_THIN.
		public const string Thin = "thin";
	}

	public static class VpeEmissiveIntensityUnits
	{
		public const string Nits = "nits";
		public const string Ev100 = "ev100";
		public const string Luminance = "luminance";
	}

	[Serializable]
	public class VpeMaterialsPayloadV1
	{
		// Schema version. Readers MUST check this before interpreting the payload.
		public int FormatVersion = 1;
		// Optional free-form identifier for the writing tool. For diagnostics only.
		public string WrittenBy;
		public VpeMaterialProfileV1[] Profiles = Array.Empty<VpeMaterialProfileV1>();
		public VpeTextureAssetV1[] Textures = Array.Empty<VpeTextureAssetV1>();
		// Per-renderer state that Unity authors but glTF does not carry. Restored after glTF import
		// so the Player sees the same shadow/lighting topology the author set up.
		public VpeRendererStateV1[] RendererStates = Array.Empty<VpeRendererStateV1>();
	}

	[Serializable]
	public class VpeRendererStateV1
	{
		// Path to the renderer's transform, relative to the table root, encoded via
		// TransformExtensions.GetPath so it round-trips through the same sibling-index scheme
		// the rest of the package uses.
		public string Path;
		// UnityEngine.Rendering.ShadowCastingMode: 0=Off, 1=On, 2=TwoSided, 3=ShadowsOnly.
		public int ShadowCastingMode = 1;
		public bool ReceiveShadows = true;
		// Unity renderingLayerMask. Bit 0 is the Default layer. Authoring tables commonly set
		// bit 8 (light-layer 8) on table geometry in addition to Default.
		public uint RenderingLayerMask = 1;
		// UnityEngine.Experimental.Rendering.RayTracingMode. -1 means older payload/no override.
		public int RayTracingMode = -1;
	}

	[Serializable]
	public class VpeMaterialProfileV1
	{
		// Name of the source material. Used to match against renderer materials at import.
		public string Name;
		// Discriminator for the payload shape. See VpeMaterialTypes.
		public string Type;

		// At most one of these is populated, matching Type. Others stay null so JSON stays compact.
		public VpeLitProfileV1 Lit;
		public VpeDecalProfileV1 Decal;
		public VpeUnlitProfileV1 Unlit;
		public VpeShaderGraphProfileV1 Metal;
		public VpeShaderGraphProfileV1 Rubber;
		public VpeShaderGraphProfileV1 Dmd;
	}

	[Serializable]
	public class VpeShaderGraphProfileV1
	{
		// Stable template key owned by the Player. Usually the source material asset name.
		public string TemplateName;
	}

	[Serializable]
	public class VpeLitProfileV1
	{
		public VpeColorAndTextureV1 BaseColor = new();

		public float Metallic;
		public float Smoothness = 0.5f;
		public float OcclusionStrength = 1f;
		public float IridescenceMask = 1f;
		public float IridescenceThickness = 1f;

		// Optional packed mask. When provided, Metallic/Smoothness/OcclusionStrength are still used
		// as remap anchors against the mask channels (see *Remap fields).
		public VpeTextureRefV1 MaskMap;
		public string MaskPacking = VpeMaskPackings.HdrpMaskMap;

		public Vector2 MetallicRemap = new(0f, 1f);
		public Vector2 SmoothnessRemap = new(0f, 1f);
		public Vector2 AoRemap = new(0f, 1f);
		public Vector2 AlphaRemap = new(0f, 1f);
		public int UvBase;
		public float TexWorldScale = 1f;
		public float InvTilingScale = 1f;
		public bool GeometricSpecularAa;
		public float SpecularAaScreenSpaceVariance;
		public float SpecularAaThreshold;
		public bool SupportDecals = true;

		public VpeNormalMapRefV1 NormalMap;

		public VpeEmissiveV1 Emissive = new();

		public string SurfaceType = VpeSurfaceTypes.Opaque;
		public float AlphaCutoff = 0.5f;
		public bool DoubleSided;
		public bool DoubleSidedGi;
		public int CullMode = -1;
		public int CullModeForward = -1;
		public int OpaqueCullMode = -1;
		public int TransparentCullMode = -1;

		// Transparent-surface hints. These map to per-pipeline blend/depth behavior and are
		// harmless for readers that ignore them.
		public int TransparentBlendMode;
		public int TransparentSortPriority;
		public bool EnableFogOnTransparent = true;
		public bool TransparentDepthPrepass;
		public bool TransparentDepthPostpass;
		public bool TransparentWritesMotionVectors;
		public bool TransparentBackface;

		// Hints for SRPs that support them. Safe to ignore.
		public bool DisableSsrTransparent;
		public bool DisableSsr;
		public int RayTracing = -1;
		public int MaterialId = -1;

		// Explicit render queue override (-1 = inherit from shader). Avoid using unless the author
		// really meant to deviate from the surface-type default.
		public int RenderQueueOverride = -1;

		// Translucency features. These only have effect when SurfaceType == "transparent".
		// See VpeRefractionModels. "none" disables refraction entirely.
		public string RefractionModel = VpeRefractionModels.None;
		public float Ior = 1f;

		// Lets light energy pass through the material surface (HDRP's Translucent material ID).
		// Needed for pinball inserts and plastics to pick up light from the playfield behind them.
		public bool HasTransmission;
		public float TransmissionEnable = -1f;
		public float TransmissionMask = -1f;
		public float DiffusionProfileHash;
		public Vector4 DiffusionProfileAsset;
		public float Thickness = 1f;
		public Vector2 ThicknessRemap = new(0f, 1f);
		public float AbsorptionDistance = 1f;
		public PackableColor TransmittanceColor = new(1f, 1f, 1f, 1f);
		public VpeTextureRefV1 ThicknessMap;
	}

	[Serializable]
	public class VpeDecalProfileV1
	{
		public VpeColorAndTextureV1 BaseColor = new();
		public VpeNormalMapRefV1 NormalMap;
		public VpeTextureRefV1 MaskMap;
		public string MaskPacking = VpeMaskPackings.HdrpMaskMap;

		public bool AffectAlbedo = true;
		public bool AffectNormal = true;
		public bool AffectMask;

		public float DecalBlend = 1f;
		public float NormalBlendSrc = 1f;
		public float MaskBlendSrc = 1f;
		public float Smoothness = 0.5f;
		public float Metallic;
		public float AmbientOcclusion = 1f;
	}

	[Serializable]
	public class VpeUnlitProfileV1
	{
		public VpeColorAndTextureV1 BaseColor = new();
		public string SurfaceType = VpeSurfaceTypes.Opaque;
		public float AlphaCutoff = 0.5f;
		public bool DoubleSided;
	}

	[Serializable]
	public class VpeColorAndTextureV1
	{
		public PackableColor Color = new(1f, 1f, 1f, 1f);
		public VpeTextureRefV1 Texture;
	}

	[Serializable]
	public class VpeEmissiveV1
	{
		public PackableColor Color = new(0f, 0f, 0f, 1f);
		public bool HasLdrColor;
		public PackableColor LdrColor = new(0f, 0f, 0f, 1f);
		public VpeTextureRefV1 Texture;
		public bool UseIntensity;
		public float Intensity;
		// See VpeEmissiveIntensityUnits.
		public string IntensityUnit = VpeEmissiveIntensityUnits.Nits;
		// HDRP ships with per-material exposure weighting. Harmless for SRPs that don't model it.
		public float ExposureWeight = 1f;
	}

	[Serializable]
	public class VpeTextureRefV1
	{
		// Id into VpeMaterialsPayloadV1.Textures. Null/empty means "no texture".
		public string TextureId;
		public Vector2 Offset = Vector2.zero;
		public Vector2 Scale = Vector2.one;
	}

	[Serializable]
	public class VpeNormalMapRefV1
	{
		public string TextureId;
		public Vector2 Offset = Vector2.zero;
		public Vector2 Scale = Vector2.one;
		public float Strength = 1f;
		// See VpeNormalPackings. Defaults to rgb since that's what PNG round-tripping produces.
		public string Packing = VpeNormalPackings.Rgb;
	}

	[Serializable]
	public class VpeTextureAssetV1
	{
		// Stable id referenced by VpeTextureRefV1.TextureId.
		public string Id;
		// Export-side blob key. The current writer uses this to match texture metadata with the
		// captured byte payload before packing everything into textures.bin.
		public string FileName;
		// Byte range inside table/meta/textures.bin. This is the normal runtime path for VPE-only
		// textures when they are not embedded into the GLB.
		public int ByteOffset = -1;
		public int ByteLength;
		// Optional GLB bufferView index for packages that embed VPE-only texture bytes directly in
		// table.glb. When set, runtime should prefer this over textures.bin.
		public int GlbBufferView = -1;
		// MIME type for the embedded bytes. Current writer emits PNG side-channel textures.
		public string MimeType = "image/png";
		// "sRGB" or "Linear". See VpeColorSpaces.
		public string ColorSpace = VpeColorSpaces.SRgb;
		public int WrapMode;      // UnityEngine.TextureWrapMode
		public int FilterMode = 2; // Trilinear
		public int AnisoLevel = 1;
		public bool GenerateMipMaps = true;
		// Optional source hint for debugging.
		public string SourceName;
		public int Width;
		public int Height;
	}
}
