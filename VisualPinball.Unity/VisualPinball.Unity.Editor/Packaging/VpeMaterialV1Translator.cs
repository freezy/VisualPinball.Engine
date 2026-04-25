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
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEditor;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	// Editor-only. Translates Unity Materials on a scene's renderers into a portable
	// VpeMaterialsPayloadV1 plus a set of PNG texture blobs keyed by stable ids.
	//
	// Only HDRP-aware mappings are implemented here; if VPE adopts additional pipelines the
	// translator fans out on shader name.
	public static class VpeMaterialV1Translator
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private const string HdrpLitShaderName = "HDRP/Lit";
		private const string HdrpDecalShaderName = "HDRP/Decal";
		private const string HdrpUnlitShaderName = "HDRP/Unlit";

		public readonly struct CaptureResult
		{
			public CaptureResult(VpeMaterialsPayloadV1 payload, IReadOnlyDictionary<string, byte[]> textureBlobs)
			{
				Payload = payload;
				TextureBlobs = textureBlobs;
			}

			public VpeMaterialsPayloadV1 Payload { get; }
			// Maps texture file name (matches VpeTextureAssetV1.FileName) to its PNG bytes.
			public IReadOnlyDictionary<string, byte[]> TextureBlobs { get; }
		}

		public static CaptureResult Capture(Transform tableRoot, IEnumerable<Renderer> renderers)
		{
			var profiles = new Dictionary<string, VpeMaterialProfileV1>(StringComparer.Ordinal);
			var rendererStates = new List<VpeRendererStateV1>();
			var ctx = new CaptureContext();

			if (renderers != null) {
				foreach (var renderer in renderers) {
					if (!renderer) {
						continue;
					}

					if (tableRoot) {
						rendererStates.Add(CaptureRendererState(renderer, tableRoot));
					}

					foreach (var material in renderer.sharedMaterials) {
						if (!material) {
							continue;
						}
						var key = NormalizeMaterialName(material.name);
						if (string.IsNullOrWhiteSpace(key) || profiles.ContainsKey(key)) {
							continue;
						}

						var profile = TranslateMaterial(material, ctx);
						if (profile != null) {
							profile.Name = key;
							profiles[key] = profile;
						}
					}
				}
			}

			var payload = new VpeMaterialsPayloadV1 {
				FormatVersion = 1,
				WrittenBy = "VpeMaterialV1Translator",
				Profiles = profiles.Values.ToArray(),
				Textures = ctx.BuildTextureAssets(),
				RendererStates = rendererStates.ToArray(),
			};
			return new CaptureResult(payload, ctx.TextureBlobs);
		}

		private static VpeRendererStateV1 CaptureRendererState(Renderer renderer, Transform tableRoot)
		{
			return new VpeRendererStateV1 {
				Path = renderer.transform.GetPath(tableRoot),
				ShadowCastingMode = (int)renderer.shadowCastingMode,
				ReceiveShadows = renderer.receiveShadows,
				RenderingLayerMask = renderer.renderingLayerMask,
			};
		}

		private static VpeMaterialProfileV1 TranslateMaterial(Material material, CaptureContext ctx)
		{
			if (!material || !material.shader) {
				return null;
			}

			var shaderName = material.shader.name;
			switch (shaderName) {
				case HdrpLitShaderName:
					return TranslateHdrpLit(material, ctx);
				case HdrpDecalShaderName:
					return TranslateHdrpDecal(material, ctx);
				case HdrpUnlitShaderName:
					return TranslateHdrpUnlit(material, ctx);
				default:
					Logger.Warn(
						$"Material '{material.name}' uses shader '{shaderName}' which has no v1 translation. " +
						"It will fall back to the glTF-imported material at runtime.");
					return null;
			}
		}

		private static VpeMaterialProfileV1 TranslateHdrpLit(Material material, CaptureContext ctx)
		{
			// For alpha-tested and transparent surfaces, the base color texture's alpha channel is
			// load-bearing (alpha-test discards pixels below cutoff; transparent blends by alpha).
			// gltFast's glTF round-trip does not preserve the alpha channel reliably for HDRP
			// alphaMode=MASK materials, so we side-channel the full RGBA PNG for those. Plain opaque
			// materials keep the leaner glb-only path where we only record tiling.
			var baseColorNeedsAlpha =
				SafeGetFloat(material, "_SurfaceType", 0f) > 0.5f /* Transparent */
				|| SafeGetFloat(material, "_AlphaCutoffEnable", 0f) > 0.5f /* AlphaTest */;
			var baseColorTexture = baseColorNeedsAlpha
				? ctx.CaptureSideChannelTextureRef(material, "_BaseColorMap", VpeColorSpaces.SRgb)
				: ctx.CaptureImportedTextureRef(material, "_BaseColorMap");

			var lit = new VpeLitProfileV1 {
				BaseColor = {
					Color = SafeGetColor(material, "_BaseColor", Color.white),
					Texture = baseColorTexture,
				},
				Metallic = SafeGetFloat(material, "_Metallic", 0f),
				Smoothness = SafeGetFloat(material, "_Smoothness", 0.5f),
				OcclusionStrength = 1f,
				// MaskMap packs HDRP-specific channels (R=metal, G=AO, B=detail, A=smooth). glTF
				// has no lossless equivalent, so this is the one texture that gets side-channeled.
				MaskMap = ctx.CaptureSideChannelTextureRef(material, "_MaskMap", VpeColorSpaces.Linear),
				MaskPacking = VpeMaskPackings.HdrpMaskMap,
				MetallicRemap = new Vector2(
					SafeGetFloat(material, "_MetallicRemapMin", 0f),
					SafeGetFloat(material, "_MetallicRemapMax", 1f)),
				SmoothnessRemap = new Vector2(
					SafeGetFloat(material, "_SmoothnessRemapMin", 0f),
					SafeGetFloat(material, "_SmoothnessRemapMax", 1f)),
				AoRemap = new Vector2(
					SafeGetFloat(material, "_AORemapMin", 0f),
					SafeGetFloat(material, "_AORemapMax", 1f)),
				AlphaRemap = new Vector2(
					SafeGetFloat(material, "_AlphaRemapMin", 0f),
					SafeGetFloat(material, "_AlphaRemapMax", 1f)),
				NormalMap = ctx.CaptureImportedNormalMapRef(material, "_NormalMap",
					strength: SafeGetFloat(material, "_NormalScale", 1f)),
				Emissive = new VpeEmissiveV1 {
					Color = SafeGetColor(material, "_EmissiveColor", Color.black),
					Texture = ctx.CaptureImportedTextureRef(material, "_EmissiveColorMap"),
					Intensity = SafeGetFloat(material, "_EmissiveIntensity", 0f),
					IntensityUnit = HdrpEmissiveIntensityUnitToString(
						SafeGetFloat(material, "_EmissiveIntensityUnit", 0f)),
					ExposureWeight = SafeGetFloat(material, "_EmissiveExposureWeight", 1f),
				},
				SurfaceType = HdrpSurfaceTypeToString(
					SafeGetFloat(material, "_SurfaceType", 0f),
					SafeGetFloat(material, "_AlphaCutoffEnable", 0f)),
				AlphaCutoff = SafeGetFloat(material, "_AlphaCutoff", 0.5f),
				DoubleSided = SafeGetFloat(material, "_DoubleSidedEnable", 0f) > 0.5f,
				DoubleSidedGi = material.doubleSidedGI,
				TransparentBlendMode = Mathf.RoundToInt(SafeGetFloat(material, "_BlendMode", 0f)),
				EnableFogOnTransparent = SafeGetFloat(material, "_EnableFogOnTransparent", 1f) > 0.5f
					|| material.IsKeywordEnabled("_ENABLE_FOG_ON_TRANSPARENT"),
				TransparentDepthPrepass = SafeGetFloat(material, "_TransparentDepthPrepassEnable", 0f) > 0.5f,
				TransparentDepthPostpass = SafeGetFloat(material, "_TransparentDepthPostpassEnable", 0f) > 0.5f,
				TransparentWritesMotionVectors = (SafeGetFloat(material, "_TransparentWritingMotionVec", 0f) > 0.5f
						|| material.IsKeywordEnabled("_TRANSPARENT_WRITES_MOTION_VEC"))
					&& (material.GetShaderPassEnabled("MOTIONVECTORS") || material.GetShaderPassEnabled("MotionVectors")),
				DisableSsrTransparent = material.IsKeywordEnabled("_DISABLE_SSR_TRANSPARENT")
					|| SafeGetFloat(material, "_ReceivesSSRTransparent", 0f) < 0.5f,
				DisableSsr = material.IsKeywordEnabled("_DISABLE_SSR")
					|| SafeGetFloat(material, "_ReceivesSSR", 1f) < 0.5f,
				RenderQueueOverride = -1,

				RefractionModel = HdrpRefractionModelToString(
					SafeGetFloat(material, "_RefractionModel", 0f),
					material),
				Ior = SafeGetFloat(material, "_Ior", 1f),
				// Authoring intent is encoded by the explicit HDRP translucent signals:
				// MaterialID==5 or the transmission keyword. Do not infer from _TransmissionEnable;
				// HDRP keeps that float at 1 on many non-translucent materials.
				HasTransmission = material.IsKeywordEnabled("_MATERIAL_FEATURE_TRANSMISSION")
					|| Mathf.Approximately(SafeGetFloat(material, "_MaterialID", 1f), 5f),
				Thickness = SafeGetFloat(material, "_Thickness", 1f),
				ThicknessMap = ctx.CaptureSideChannelTextureRef(material, "_ThicknessMap", VpeColorSpaces.Linear),
			};

			return new VpeMaterialProfileV1 {
				Type = VpeMaterialTypes.Lit,
				Lit = lit,
			};
		}

		private static VpeMaterialProfileV1 TranslateHdrpDecal(Material material, CaptureContext ctx)
		{
			var decal = new VpeDecalProfileV1 {
				BaseColor = {
					Color = SafeGetColor(material, "_BaseColor", Color.white),
					// Decal albedo alpha is load-bearing (where the decal applies). Exporting through
					// glTF can convert this map to JPEG and drop alpha, so always side-channel it.
					Texture = ctx.CaptureSideChannelTextureRef(material, "_BaseColorMap", VpeColorSpaces.SRgb),
				},
				NormalMap = ctx.CaptureImportedNormalMapRef(material, "_NormalMap",
					strength: SafeGetFloat(material, "_NormalScale", 1f)),
				MaskMap = ctx.CaptureSideChannelTextureRef(material, "_MaskMap", VpeColorSpaces.Linear),
				MaskPacking = VpeMaskPackings.HdrpMaskMap,
				AffectAlbedo = material.IsKeywordEnabled("_MATERIAL_AFFECTS_ALBEDO")
					|| SafeGetFloat(material, "_AffectAlbedo", 1f) > 0.5f,
				AffectNormal = material.IsKeywordEnabled("_MATERIAL_AFFECTS_NORMAL")
					|| SafeGetFloat(material, "_AffectNormal", 1f) > 0.5f,
				AffectMask = material.IsKeywordEnabled("_MATERIAL_AFFECTS_MASKMAP")
					|| SafeGetFloat(material, "_AffectMaskmap", 0f) > 0.5f,
				DecalBlend = SafeGetFloat(material, "_DecalBlend", 1f),
				NormalBlendSrc = SafeGetFloat(material, "_NormalBlendSrc", 1f),
				MaskBlendSrc = SafeGetFloat(material, "_MaskBlendSrc", 1f),
				Smoothness = SafeGetFloat(material, "_DecalSmoothness", 0.5f),
				Metallic = SafeGetFloat(material, "_DecalMetallic", 0f),
				AmbientOcclusion = SafeGetFloat(material, "_DecalAO", 1f),
			};

			return new VpeMaterialProfileV1 {
				Type = VpeMaterialTypes.Decal,
				Decal = decal,
			};
		}

		private static VpeMaterialProfileV1 TranslateHdrpUnlit(Material material, CaptureContext ctx)
		{
			var unlit = new VpeUnlitProfileV1 {
				BaseColor = {
					Color = SafeGetColor(material, "_UnlitColor", SafeGetColor(material, "_BaseColor", Color.white)),
					Texture = ctx.CaptureImportedTextureRef(material, "_UnlitColorMap")
						?? ctx.CaptureImportedTextureRef(material, "_BaseColorMap"),
				},
				SurfaceType = HdrpSurfaceTypeToString(
					SafeGetFloat(material, "_SurfaceType", 0f),
					SafeGetFloat(material, "_AlphaCutoffEnable", 0f)),
				AlphaCutoff = SafeGetFloat(material, "_AlphaCutoff", 0.5f),
				DoubleSided = SafeGetFloat(material, "_DoubleSidedEnable", 0f) > 0.5f,
			};
			return new VpeMaterialProfileV1 {
				Type = VpeMaterialTypes.Unlit,
				Unlit = unlit,
			};
		}

		private static string HdrpSurfaceTypeToString(float surfaceType, float alphaCutoffEnable)
		{
			if (surfaceType > 0.5f) {
				return VpeSurfaceTypes.Transparent;
			}
			return alphaCutoffEnable > 0.5f ? VpeSurfaceTypes.AlphaTest : VpeSurfaceTypes.Opaque;
		}

		private static string HdrpEmissiveIntensityUnitToString(float value)
		{
			// HDRP: 0 = Nits, 1 = EV100.
			return value > 0.5f ? VpeEmissiveIntensityUnits.Ev100 : VpeEmissiveIntensityUnits.Nits;
		}

		// HDRP _RefractionModel float: 0=None, 1=Plane, 2=Sphere, 3=Thin. We also check keywords
		// since the float is sometimes left at a stale value while the keyword tells the real story.
		private static string HdrpRefractionModelToString(float value, Material material)
		{
			if (material.IsKeywordEnabled("_REFRACTION_PLANE")) {
				return VpeRefractionModels.Planar;
			}
			if (material.IsKeywordEnabled("_REFRACTION_SPHERE")) {
				return VpeRefractionModels.Sphere;
			}
			if (material.IsKeywordEnabled("_REFRACTION_THIN")) {
				return VpeRefractionModels.Thin;
			}
			var mode = Mathf.RoundToInt(value);
			return mode switch {
				1 => VpeRefractionModels.Planar,
				2 => VpeRefractionModels.Sphere,
				3 => VpeRefractionModels.Thin,
				_ => VpeRefractionModels.None,
			};
		}

		private static float SafeGetFloat(Material material, string property, float fallback)
		{
			return material.HasProperty(property) ? material.GetFloat(property) : fallback;
		}

		private static Color SafeGetColor(Material material, string property, Color fallback)
		{
			return material.HasProperty(property) ? material.GetColor(property) : fallback;
		}

		public static string NormalizeMaterialName(string materialName)
			=> VpeMaterialNameUtil.NormalizeMaterialName(materialName);

		private sealed class CaptureContext
		{
			private readonly Dictionary<Texture2D, VpeTextureAssetV1> _assetsByTexture = new();
			private readonly Dictionary<string, byte[]> _textureBlobs = new(StringComparer.Ordinal);
			private int _nextIndex;

			public IReadOnlyDictionary<string, byte[]> TextureBlobs => _textureBlobs;

			public VpeTextureAssetV1[] BuildTextureAssets()
			{
				var assets = new VpeTextureAssetV1[_assetsByTexture.Count];
				var i = 0;
				foreach (var asset in _assetsByTexture.Values) {
					assets[i++] = asset;
				}
				return assets;
			}

			// Captures a texture reference whose pixel data must be shipped in the side-channel
			// (i.e. is not losslessly reproduced by the glb). Use for HDRP-specific packings like
			// MaskMap where channel semantics differ from glTF's PBR textures.
			public VpeTextureRefV1 CaptureSideChannelTextureRef(Material material, string property, string colorSpace, VpeTextureRefV1 fallback = null)
			{
				if (!material.HasProperty(property)) {
					return fallback;
				}
				var texture = material.GetTexture(property) as Texture2D;
				if (!texture) {
					return fallback;
				}

				var asset = GetOrCaptureAsset(texture, colorSpace);
				if (asset == null) {
					return fallback;
				}

				return new VpeTextureRefV1 {
					TextureId = asset.Id,
					Offset = material.GetTextureOffset(property),
					Scale = material.GetTextureScale(property),
				};
			}

			// Captures tiling only — no TextureId, no side-channel bytes. Pixel data is read at
			// runtime from the gltFast-imported material by matching property-name aliases.
			public VpeTextureRefV1 CaptureImportedTextureRef(Material material, string property)
			{
				if (!material.HasProperty(property)) {
					return null;
				}
				var texture = material.GetTexture(property);
				if (!texture) {
					return null;
				}
				return new VpeTextureRefV1 {
					TextureId = null,
					Offset = material.GetTextureOffset(property),
					Scale = material.GetTextureScale(property),
				};
			}

			public VpeNormalMapRefV1 CaptureImportedNormalMapRef(Material material, string property, float strength)
			{
				if (!material.HasProperty(property)) {
					return null;
				}
				var texture = material.GetTexture(property);
				if (!texture) {
					return null;
				}
				return new VpeNormalMapRefV1 {
					TextureId = null,
					Offset = material.GetTextureOffset(property),
					Scale = material.GetTextureScale(property),
					Strength = strength,
					// Runtime imports may arrive as plain RGB (glTFast doesn't carry Unity's normal
					// map import flag). The resolver re-packs as needed.
					Packing = VpeNormalPackings.Rgb,
				};
			}

			private VpeTextureAssetV1 GetOrCaptureAsset(Texture2D texture, string colorSpace)
			{
				if (_assetsByTexture.TryGetValue(texture, out var existing)) {
					return existing;
				}

				var linear = colorSpace == VpeColorSpaces.Linear;
				if (!VpeMaterialV1TextureEncoder.TryEncode(texture, linear, out var pngData)) {
					return null;
				}

				var id = BuildId(texture);
				var fileName = $"tex_{_nextIndex++:D4}.png";
				var asset = new VpeTextureAssetV1 {
					Id = id,
					FileName = fileName,
					ColorSpace = linear ? VpeColorSpaces.Linear : VpeColorSpaces.SRgb,
					WrapMode = (int)texture.wrapMode,
					FilterMode = (int)texture.filterMode,
					AnisoLevel = Mathf.Max(1, texture.anisoLevel),
					GenerateMipMaps = true,
					SourceName = texture.name,
					Width = texture.width,
					Height = texture.height,
				};

				// Ask the Editor TextureImporter for canonical settings when available. This lets us
				// preserve the author's intent (sRGB, wrap mode, aniso) instead of reading from a
				// Texture instance that may have been mutated at runtime.
				var assetPath = AssetDatabase.GetAssetPath(texture);
				if (!string.IsNullOrEmpty(assetPath) && AssetImporter.GetAtPath(assetPath) is TextureImporter importer) {
					asset.ColorSpace = importer.sRGBTexture ? VpeColorSpaces.SRgb : VpeColorSpaces.Linear;
					asset.GenerateMipMaps = importer.mipmapEnabled;
					asset.AnisoLevel = Mathf.Max(asset.AnisoLevel, importer.anisoLevel);
					asset.WrapMode = (int)importer.wrapMode;
					asset.FilterMode = (int)importer.filterMode;
				}

				_assetsByTexture[texture] = asset;
				_textureBlobs[fileName] = pngData;
				return asset;
			}

			private string BuildId(Texture2D texture)
			{
				var raw = string.IsNullOrWhiteSpace(texture.name) ? $"tex{_nextIndex}" : texture.name;
				// Normalize so the id is stable across exports regardless of editor instance suffixes.
				return VpeMaterialNameUtil.NormalizeTextureName(raw);
			}
		}
	}

}
