// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using System.Linq;
using System.Text;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT
{
	public class PbrMaterial
	{
		private const float ColorOpacityThreshold = 0.9f;

		public readonly Texture Map;
		public readonly Texture NormalMap;
		public readonly Texture EnvMap;

		public bool HasMap => Map != null;
		public bool HasNormalMap => NormalMap != null;
		public BlendMode MapBlendMode => GetBlendMode();

		public Color Color => _material?.BaseColor ?? new Color(0xffffff, ColorFormat.Bgr);
		public bool IsMetal => _material?.IsMetal ?? false;
		public bool IsOpacityActive => _material?.IsOpacityActive ?? false;
		public float Opacity => MathF.Min(1, MathF.Max(0, _material?.Opacity ?? 1f));
		public float Roughness => _material?.Roughness ?? 0.5f;
		private float Edge => _material?.Edge ?? 0f;

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
			Map.Analyze();
		}

		private BlendMode GetBlendMode()
		{
			// if there is no map, let's look at the transparency settings of the material.
			if (!HasMap) {
				return IsOpacityActive && Opacity < ColorOpacityThreshold
					? BlendMode.Translucent
					: BlendMode.Opaque;
			}

			// if there's a map, we need stats.
			var stats = Map.GetStats();

			// map is opaque: easy
			if (stats.IsOpaque) {
				return BlendMode.Opaque;
			}

			// not opaque, but all alphas are > 0
			if (!stats.HasTransparentPixels) {
				return BlendMode.Translucent;
			}

			// educated guess based on alpha stats (might need to be tweaked)
			return stats.Translucent / stats.Transparent > 0.1
				? BlendMode.Translucent
				: BlendMode.Cutout;
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.AppendLine($"Id              {Id}");
			sb.AppendLine($"Color           {Color}");
			sb.AppendLine($"IsMetal         {IsMetal}");
			sb.AppendLine($"Roughness       {Roughness}");
			sb.AppendLine($"Edge            {Edge}");
			sb.AppendLine($"IsOpacityActive {IsOpacityActive}");
			sb.AppendLine($"Opacity         {Opacity}");
			sb.AppendLine($"Map             {Map?.ToString() ?? "none"}".Trim());
			sb.AppendLine($"MapBlendMode    {MapBlendMode}");
			sb.AppendLine($"NormalMap       {NormalMap?.ToString() ?? "none"}".Trim());

			return sb.ToString();
		}
	}

	public enum BlendMode
	{
		Opaque,
		Cutout,
		Translucent
	}
}
