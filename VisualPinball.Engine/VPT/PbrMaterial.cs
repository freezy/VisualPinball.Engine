using System;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT
{
	public class PbrMaterial
	{
		public const float ColorOpacityThreshold = 0.9f;

		public readonly Texture Map;
		public readonly Texture NormalMap;
		public readonly Texture EnvMap;

		public bool HasMap => Map != null;
		public bool HasNormalMap => NormalMap != null;
		public BlendMode MapBlendMode => GetBlendMode();

		public Color Color => _material?.BaseColor ?? new Color(0xff00ff, ColorFormat.Bgr);
		public bool IsMetal => _material?.IsMetal ?? false;
		public bool IsOpacityActive => _material?.IsOpacityActive ?? false;
		public float Opacity => MathF.Min(1, MathF.Max(0, _material?.Opacity ?? 1f));
		public float Roughness => _material?.Roughness ?? 0.5f;
		public float Edge => _material?.Edge ?? 0f;

		public const string NameNoMaterial = "__std";
		private const string NameNoMap = "__no_map";
		private const string NameNoNormalMap = "__no_normal_map";
		private const string NameNoEnvMap = "__no_env_map";

		/// <summary>
		/// A unique ID based on the material and its maps.
		/// </summary>
		public readonly string Id;

		private readonly Material _material;

		public PbrMaterial(Material material = null, Texture map = null, Texture normalMap = null, Texture envMap = null)
		{
			_material = material;
			Map = map;
			NormalMap = normalMap;
			EnvMap = envMap;
			Id = string.Join("-", new[] {
					_material?.Name.ToNormalizedName() ?? NameNoMaterial,
					Map?.Name.ToNormalizedName() ?? NameNoMap,
					NormalMap?.Name.ToNormalizedName() ?? NameNoNormalMap,
					EnvMap?.Name.ToNormalizedName() ?? NameNoEnvMap
				}
				.Reverse()
				.SkipWhile(s => s.StartsWith("__no_"))
				.Reverse()
			);
			if (NormalMap != null) {
				NormalMap.UsageNormalMap = true;
			}
		}

		public void AnalyzeMap()
		{
			if (!HasMap) {
				return;
			}
			Map.Analyze(IsOpacityActive);
		}

		private BlendMode GetBlendMode()
		{
			// if there is no map, let's look at the transparency settings of the material.
			if (!HasMap) {
				return IsOpacityActive && Opacity < ColorOpacityThreshold
					? BlendMode.Translucent
					: BlendMode.Opaque;
			}

			// no transparent pixels: easy
			if (Map.IsOpaque) {
				return BlendMode.Opaque;
			}

			// opacity not active is difficult. if the map has transparency, it
			// might be be cut-out, so let's look at the edge (might be worth
			// looking at the stats, in which case AnalyzeMap() needs to be adapted)
			if (!IsOpacityActive) {
				return Edge < 1 ? BlendMode.Cutout : BlendMode.Opaque;
			}

			// here, opacity is active. let's look at the stats.
			var stats = Map.GetStats();
			if (!stats.HasTransparentPixels) {
				return BlendMode.Translucent;
			}
			return stats.Translucent / stats.Transparent > 0.1
				? BlendMode.Translucent
				: BlendMode.Cutout;
		}
	}

	public enum BlendMode
	{
		Opaque,
		Cutout,
		Translucent
	}
}
