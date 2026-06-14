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

namespace VisualPinball.Unity
{
	/// <summary>Per-camera post anti-aliasing mode (mirrors HDRP's HDAdditionalCameraData.AntialiasingMode).</summary>
	public enum VpeAntiAliasing
	{
		None = 0,
		Fxaa = 1,
		Smaa = 2,
		Taa = 3,
	}

	/// <summary>
	/// Plain-data render-quality settings that are tunable at runtime via the active render pipeline's
	/// <see cref="IVpeGraphicsApplier"/>. Carries only the SRP-specific knobs (camera AA + Volume effects);
	/// engine-level settings (resolution, display mode, vsync, frame cap, quality level / pipeline-asset
	/// swap) are applied directly by the host app since they need no pipeline types.
	/// </summary>
	public struct VpeGraphicsSettings
	{
		public int AntiAliasing; // VpeAntiAliasing
		public bool ScreenSpaceReflections;
		public bool AmbientOcclusion;
		public bool ScreenSpaceGlobalIllumination;
		public bool Bloom;
		public bool RayTracedGlobalIllumination;
		public bool RayTracedReflections;
	}

	/// <summary>Implemented by the active render pipeline (e.g. HDRP) to apply <see cref="VpeGraphicsSettings"/>.</summary>
	public interface IVpeGraphicsApplier
	{
		void Apply(VpeGraphicsSettings settings);
	}

	/// <summary>
	/// Bridges plain-data graphics settings from the host app to the render-pipeline-specific applier. The
	/// SRP assembly (e.g. HDRP) registers its applier via <c>[RuntimeInitializeOnLoadMethod]</c>, the same
	/// pattern as <c>VpeMaterialResolver</c>, so the host never references pipeline types.
	/// </summary>
	public static class VpeGraphics
	{
		private static IVpeGraphicsApplier _applier;

		public static bool HasApplier => _applier != null;

		public static void Register(IVpeGraphicsApplier applier) => _applier = applier;

		public static void Apply(VpeGraphicsSettings settings) => _applier?.Apply(settings);
	}
}
