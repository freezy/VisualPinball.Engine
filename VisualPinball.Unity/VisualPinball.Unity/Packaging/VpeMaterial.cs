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
	// The portable material interchange schema carried in table/meta/materials.json.
	//
	// Design goals:
	// - The top level of each profile is portable rendering intent any engine can implement:
	//   PBR core, surface type, alpha, emissive, transmission/refraction. Field semantics map
	//   onto glTF PBR + KHR extensions (transmission, ior, volume, emissive_strength) wherever
	//   such a mapping exists.
	// - Everything pipeline-specific lives in a nested "Hdrp" block (more blocks may follow,
	//   e.g. "Urp"). Readers that don't know a block ignore it without losing the portable core.
	// - Enumerations are strings, not engine enum ints. The only numbers left in the portable
	//   layer are actual quantities.
	// - Readers MUST check FormatVersion before interpreting the payload and skip versions they
	//   don't know.
	//
	// When adding a new material type, add a new sibling property on VpeMaterialProfile and a
	// new VpeMaterialTypes constant. Never repurpose an existing field.

	public static class VpeMaterialSchema
	{
		public const int Version = 1;
	}

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
		// R,G,B store X,Y,Z as an RGB image. Shader reconstructs as normalize(rgb * 2 - 1).
		public const string Rgb = "rgb";
		// R,G store X,Y; Z is reconstructed. Matches Unity's tangent-space normal sampling.
		public const string Rg = "rg";
		// Dxt5nm swizzle (A,G store X,Y). Used by cooked payloads in the local cache.
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

	// HDRP BlendMode: 0 = Alpha, 1 = Additive, 4 = Premultiply.
	public static class VpeBlendModes
	{
		public const string Alpha = "alpha";
		public const string Additive = "additive";
		public const string Premultiply = "premultiply";
	}

	// UnityEngine.TextureWrapMode equivalents.
	public static class VpeWrapModes
	{
		public const string Repeat = "repeat";
		public const string Clamp = "clamp";
		public const string Mirror = "mirror";
		public const string MirrorOnce = "mirrorOnce";
	}

	// UnityEngine.FilterMode equivalents.
	public static class VpeFilterModes
	{
		public const string Point = "point";
		public const string Bilinear = "bilinear";
		public const string Trilinear = "trilinear";
	}

	// UnityEngine.Rendering.ShadowCastingMode equivalents.
	public static class VpeShadowCastingModes
	{
		public const string Off = "off";
		public const string On = "on";
		public const string TwoSided = "twoSided";
		public const string ShadowsOnly = "shadowsOnly";
	}

	[Serializable]
	public class VpeMaterialsPayload
	{
		// Schema version. Readers MUST check this before interpreting the payload.
		public int FormatVersion = VpeMaterialSchema.Version;
		// Optional free-form identifier for the writing tool. For diagnostics only.
		public string WrittenBy;
		public VpeMaterialProfile[] Profiles = Array.Empty<VpeMaterialProfile>();
		public VpeTexture[] Textures = Array.Empty<VpeTexture>();
		// Per-renderer state that glTF does not carry. Restored after glTF import so the Player
		// sees the same shadow/lighting topology the author set up.
		public VpeRendererState[] RendererStates = Array.Empty<VpeRendererState>();
	}

	[Serializable]
	public class VpeRendererState
	{
		// Stable node id of the renderer's node (glTF extras.vpeId in table.glb).
		public string NodeId;
		// See VpeShadowCastingModes.
		public string CastShadows = VpeShadowCastingModes.On;
		public bool ReceiveShadows = true;
		// Unity renderingLayerMask. Bit 0 is the default layer; authoring tables commonly add
		// bit 8 (light-layer 8) on table geometry.
		public uint RenderingLayerMask = 1;

		public VpeHdrpRendererHints Hdrp = new();
	}

	[Serializable]
	public class VpeHdrpRendererHints
	{
		// UnityEngine.Experimental.Rendering.RayTracingMode. -1 means no override.
		public int RayTracingMode = -1;
	}

	[Serializable]
	public class VpeMaterialProfile
	{
		// Name of the source material. Used to match against renderer materials at import.
		public string Name;
		// Discriminator for the payload shape. See VpeMaterialTypes. Readers skip unknown types.
		public string Type;

		// At most one of these is populated, matching Type. Shader-graph types (Metal, Rubber)
		// additionally populate Lit as the portable fallback for resolvers without the template.
		public VpeLitProfile Lit;
		public VpeDecalProfile Decal;
		public VpeUnlitProfile Unlit;
		public VpeShaderGraphProfile Metal;
		public VpeShaderGraphProfile Rubber;
		public VpeShaderGraphProfile Dmd;
	}

	[Serializable]
	public class VpeShaderGraphProfile
	{
		// Stable template key owned by the Player. Usually the source material asset name.
		public string TemplateName;
	}

	[Serializable]
	public class VpeLitProfile
	{
		#region Portable core

		public VpeColorAndTexture BaseColor = new();

		public float Metallic;
		public float Smoothness = 0.5f;
		public float OcclusionStrength = 1f;
		public float IridescenceMask = 1f;
		public float IridescenceThickness = 1f;

		// Optional packed mask. When provided, Metallic/Smoothness/OcclusionStrength are still
		// used as remap anchors against the mask channels (see *Remap fields).
		public VpeTextureRef MaskMap;
		public string MaskPacking = VpeMaskPackings.HdrpMaskMap;

		public Vector2 MetallicRemap = new(0f, 1f);
		public Vector2 SmoothnessRemap = new(0f, 1f);
		public Vector2 AoRemap = new(0f, 1f);
		public Vector2 AlphaRemap = new(0f, 1f);
		// UV channel the base maps sample (0-based).
		public int UvBase;

		public VpeNormalMapRef NormalMap;

		public VpeEmissive Emissive = new();

		// See VpeSurfaceTypes.
		public string SurfaceType = VpeSurfaceTypes.Opaque;
		public float AlphaCutoff = 0.5f;
		public bool DoubleSided;
		public bool DoubleSidedGi;
		// Blend operation for transparent surfaces. See VpeBlendModes.
		public string BlendMode = VpeBlendModes.Alpha;
		// Relative draw-order bias among transparent surfaces (higher draws later).
		public int SortPriority;

		// Translucency. See VpeRefractionModels; "none" disables refraction entirely.
		public string RefractionModel = VpeRefractionModels.None;
		public float Ior = 1f;

		// Lets light energy pass through the surface (pinball inserts and plastics pick up light
		// from the playfield behind them). Maps to HDRP's Translucent material, and semantically
		// to glTF KHR_materials_transmission + KHR_materials_volume.
		public bool HasTransmission;
		public float Thickness = 1f;
		public Vector2 ThicknessRemap = new(0f, 1f);
		public float AbsorptionDistance = 1f;
		public PackableColor TransmittanceColor = new(1f, 1f, 1f, 1f);
		public VpeTextureRef ThicknessMap;

		#endregion

		// HDRP-specific hints. Other pipelines ignore this block; visuals degrade gracefully.
		public VpeHdrpLitHints Hdrp = new();
	}

	[Serializable]
	public class VpeHdrpLitHints
	{
		// Planar/triplanar mapping parameters (HDRP _TexWorldScale/_InvTilingScale/_UVBase combo).
		public float TexWorldScale = 1f;
		public float InvTilingScale = 1f;

		public bool GeometricSpecularAa;
		public float SpecularAaScreenSpaceVariance;
		public float SpecularAaThreshold;
		public bool SupportDecals = true;

		// HDRP cull-mode property set (UnityEngine.Rendering.CullMode values; -1 = template default).
		public int CullMode = -1;
		public int CullModeForward = -1;
		public int OpaqueCullMode = -1;
		public int TransparentCullMode = -1;

		public bool EnableFogOnTransparent = true;
		public bool TransparentDepthPrepass;
		public bool TransparentDepthPostpass;
		public bool TransparentWritesMotionVectors;
		public bool TransparentBackface;

		public bool DisableSsrTransparent;
		public bool DisableSsr;
		// HDRP _RayTracing property; -1 = no override.
		public int RayTracing = -1;
		// HDRP _MaterialID; -1 = template default.
		public int MaterialId = -1;
		// Explicit Unity render queue override (-1 = inherit from surface type).
		public int RenderQueueOverride = -1;

		// HDRP translucency scalars (-1 = template default).
		public float TransmissionEnable = -1f;
		public float TransmissionMask = -1f;
		// HDRP diffusion profile binding (asset hash + GUID-as-vector). Opaque outside HDRP.
		public float DiffusionProfileHash;
		public Vector4 DiffusionProfileAsset;

		// HDRP per-material emissive exposure weighting.
		public float EmissiveExposureWeight = 1f;
	}

	[Serializable]
	public class VpeDecalProfile
	{
		public VpeColorAndTexture BaseColor = new();
		public VpeNormalMapRef NormalMap;
		public VpeTextureRef MaskMap;
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
	public class VpeUnlitProfile
	{
		public VpeColorAndTexture BaseColor = new();
		public string SurfaceType = VpeSurfaceTypes.Opaque;
		public float AlphaCutoff = 0.5f;
		public bool DoubleSided;
	}

	[Serializable]
	public class VpeColorAndTexture
	{
		public PackableColor Color = new(1f, 1f, 1f, 1f);
		public VpeTextureRef Texture;
	}

	[Serializable]
	public class VpeEmissive
	{
		public PackableColor Color = new(0f, 0f, 0f, 1f);
		public bool HasLdrColor;
		public PackableColor LdrColor = new(0f, 0f, 0f, 1f);
		public VpeTextureRef Texture;
		public bool UseIntensity;
		public float Intensity;
		// See VpeEmissiveIntensityUnits.
		public string IntensityUnit = VpeEmissiveIntensityUnits.Nits;
	}

	[Serializable]
	public class VpeTextureRef
	{
		// Id into VpeMaterialsPayload.Textures. Null/empty means "no texture" (or, on refs
		// captured from the glTF path, "read pixels from the imported material").
		public string TextureId;
		public Vector2 Offset = Vector2.zero;
		public Vector2 Scale = Vector2.one;
	}

	[Serializable]
	public class VpeNormalMapRef
	{
		public string TextureId;
		public Vector2 Offset = Vector2.zero;
		public Vector2 Scale = Vector2.one;
		public float Strength = 1f;
		// See VpeNormalPackings. Source normals in the package are plain RGB; readers re-pack
		// for their pipeline (and flip this to the cooked packing in their local cache).
		public string Packing = VpeNormalPackings.Rgb;
		// Whether a runtime that re-packs this normal should also GPU-compress the result.
		public bool RuntimeCompress = true;
	}

	/// <summary>
	/// A source texture of the package: a plain PNG/JPEG file stored as
	/// <c>table/textures/&lt;FileName&gt;</c>. This is the lossless source layer — GPU-ready
	/// payloads are derived locally by the player and cached, never shipped.
	/// </summary>
	[Serializable]
	public class VpeTexture
	{
		// Stable id referenced by VpeTextureRef.TextureId.
		public string Id;
		// File name inside table/textures/.
		public string FileName;
		// "image/png" or "image/jpeg".
		public string MimeType = "image/png";
		// "sRGB" or "Linear". See VpeColorSpaces.
		public string ColorSpace = VpeColorSpaces.SRgb;
		// See VpeWrapModes.
		public string WrapMode = VpeWrapModes.Repeat;
		// See VpeFilterModes.
		public string FilterMode = VpeFilterModes.Trilinear;
		public int AnisoLevel = 1;
		public bool GenerateMipMaps = true;
		// Whether a runtime without a cook path should GPU-compress after decoding.
		public bool RuntimeCompress = true;
		// Optional source hint for debugging.
		public string SourceName;
		// Authored dimensions (after any importer max-size clamp). Larger source files are
		// downsized to fit when cooking.
		public int Width;
		public int Height;
	}

	/// <summary>
	/// String-enum conversions between the v2 schema and Unity's enums. The schema stays free of
	/// engine enum ints; these helpers are the single place the mapping lives.
	/// </summary>
	public static class VpeMaterialEnums
	{
		public static string ToWrapMode(TextureWrapMode mode) => mode switch {
			TextureWrapMode.Clamp => VpeWrapModes.Clamp,
			TextureWrapMode.Mirror => VpeWrapModes.Mirror,
			TextureWrapMode.MirrorOnce => VpeWrapModes.MirrorOnce,
			_ => VpeWrapModes.Repeat,
		};

		public static TextureWrapMode ParseWrapMode(string mode) => mode switch {
			VpeWrapModes.Clamp => TextureWrapMode.Clamp,
			VpeWrapModes.Mirror => TextureWrapMode.Mirror,
			VpeWrapModes.MirrorOnce => TextureWrapMode.MirrorOnce,
			_ => TextureWrapMode.Repeat,
		};

		public static string ToFilterMode(FilterMode mode) => mode switch {
			FilterMode.Point => VpeFilterModes.Point,
			FilterMode.Bilinear => VpeFilterModes.Bilinear,
			_ => VpeFilterModes.Trilinear,
		};

		public static FilterMode ParseFilterMode(string mode) => mode switch {
			VpeFilterModes.Point => FilterMode.Point,
			VpeFilterModes.Bilinear => FilterMode.Bilinear,
			_ => FilterMode.Trilinear,
		};

		public static string ToShadowCastingMode(UnityEngine.Rendering.ShadowCastingMode mode) => mode switch {
			UnityEngine.Rendering.ShadowCastingMode.Off => VpeShadowCastingModes.Off,
			UnityEngine.Rendering.ShadowCastingMode.TwoSided => VpeShadowCastingModes.TwoSided,
			UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly => VpeShadowCastingModes.ShadowsOnly,
			_ => VpeShadowCastingModes.On,
		};

		public static UnityEngine.Rendering.ShadowCastingMode ParseShadowCastingMode(string mode) => mode switch {
			VpeShadowCastingModes.Off => UnityEngine.Rendering.ShadowCastingMode.Off,
			VpeShadowCastingModes.TwoSided => UnityEngine.Rendering.ShadowCastingMode.TwoSided,
			VpeShadowCastingModes.ShadowsOnly => UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly,
			_ => UnityEngine.Rendering.ShadowCastingMode.On,
		};

		// HDRP BlendMode property values: 0 = Alpha, 1 = Additive, 4 = Premultiply.
		public static string ToBlendMode(int hdrpBlendMode) => hdrpBlendMode switch {
			1 => VpeBlendModes.Additive,
			4 => VpeBlendModes.Premultiply,
			_ => VpeBlendModes.Alpha,
		};

		public static int ToHdrpBlendMode(string blendMode) => blendMode switch {
			VpeBlendModes.Additive => 1,
			VpeBlendModes.Premultiply => 4,
			_ => 0,
		};
	}
}
