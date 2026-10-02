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
	/// <summary>
	/// How an insert's UVs are optimized and its normal map baked, in Blender.
	/// </summary>
	/// <remarks>
	/// None of this changes the generated mesh, so changing it does not make an
	/// existing bake stale; it takes effect on the next bake.
	/// </remarks>
	[Serializable]
	public struct InsertBakeSettings
	{
		[Tooltip("Width and height of the baked normal map, in pixels.")]
		public int TextureSize;

		[Tooltip("How much smaller than the normal map the optical normals are baked. Every facet is an island of its own, so a small map runs out of texels for the islands before it runs out of detail: keep the same size unless the insert has few facets. 1 is the same size, 2 half, 4 a quarter.")]
		public int OpticalScale;

		[Tooltip("How much smaller than the normal map the light response is baked: a soft glow and smooth masks that need fewer texels than the creases, but each facet island still needs a few of its own. 1 is the same size, 2 half, 4 a quarter, 8 an eighth.")]
		public int ResponseScale;

		[Tooltip("How many pixels each UV island is extended by in the normal map. Islands are packed at least twice this far apart; the smaller maps keep the same distance in their own pixels.")]
		public int Padding;

		[Tooltip("How far every sharp edge is rounded off in the normal map, in VPX units. Zero bakes the edges sharp.")]
		public float EdgeBevel;

		[Range(0f, 1f)]
		[Tooltip("Fine surface texture baked into the body's normals. Zero leaves the surface smooth. The hybrid material's Surface Grain draws the same grain at any zoom without a bake; keep this at zero with it. Takes effect after baking again.")]
		public float NormalGrainStrength;

		[Min(0.1f)]
		[Tooltip("Size of the baked surface grain in VPX units. Smaller values make finer grain. Takes effect after baking again.")]
		public float NormalGrainSize;

		/// <summary>The optical normals' size in pixels, from the normal map's and the scale.</summary>
		public int OpticalSize => ScaledSize(OpticalScale);

		/// <summary>The light response's size in pixels, from the normal map's and the scale.</summary>
		public int ResponseSize => ScaledSize(ResponseScale);

		private int ScaledSize(int scale) => System.Math.Max(MinMapSize, TextureSize / System.Math.Max(1, scale));

		public const int MinMapSize = 64;

		[Tooltip("Let Blender unwrap the mesh along the generator's charts. Off keeps the generator's own layout and only repacks it.")]
		public bool Unwrap;

		[Tooltip("Pack with UVPackMaster when it is installed in Blender, instead of Blender's own packer.")]
		public bool PreferUvPackMaster;

		[Tooltip("Bake on the GPU, as set up in Blender's preferences, instead of the CPU.")]
		public bool Gpu;

		[Tooltip("Also bake optical normals and a soft internal light response for the HDRP hybrid insert material.")]
		public bool HybridOptics;

		public static InsertBakeSettings Default => new() {
			TextureSize = 1024,
			OpticalScale = 1,
			ResponseScale = 2,
			Padding = 8,
			EdgeBevel = 1f,
			NormalGrainStrength = 0f,
			NormalGrainSize = 1.5f,
			Unwrap = true,
			PreferUvPackMaster = true,
			Gpu = false,
			HybridOptics = true,
		};
	}
}
