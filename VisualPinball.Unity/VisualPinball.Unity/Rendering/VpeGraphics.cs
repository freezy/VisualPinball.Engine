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

	/// <summary>Temporal upscaler the camera renders through, at a lower internal resolution.</summary>
	public enum VpeUpscaler
	{
		Off = 0,
		Dlss = 1,   // NVIDIA Deep Learning Super Sampling
		Fsr2 = 2,   // AMD FidelityFX Super Resolution 2
		Fsr1 = 3,   // URP spatial upscaling
		Stp = 4,    // URP Spatial-Temporal Post-processing
	}

	/// <summary>How far the upscaler lowers the internal resolution (each upscaler picks its own scale per mode).</summary>
	public enum VpeUpscalerQuality
	{
		Quality = 0,
		Balanced = 1,
		Performance = 2,
		UltraPerformance = 3,
	}

	/// <summary>How much what moves fast streaks, the balls above all. High is still a short streak.</summary>
	public enum VpeMotionBlur
	{
		Off = 0,
		Low = 1,
		Medium = 2,
		High = 3,
	}

	/// <summary>What the lights around the table cast shadows with. The lamps on the table never do.</summary>
	public enum VpeShadows
	{
		Off = 0,
		/// <summary>Small shadow maps with hard edges.</summary>
		Low = 1,
		/// <summary>The shadow maps and filtering the lights were set up with.</summary>
		High = 2,
	}

	/// <summary>How far and how finely screen-space reflections are traced.</summary>
	public enum VpeReflectionQuality
	{
		/// <summary>A short trace on opaque surfaces.</summary>
		Low = 0,
		/// <summary>A medium trace on opaque surfaces.</summary>
		Medium = 1,
		/// <summary>The trace the volume was set up with, on transparent surfaces too where the pipeline has that.</summary>
		High = 2,
	}

	/// <summary>An effect a render tier may leave out, see <see cref="VpeGraphics.IsEffectSupported"/>.</summary>
	public enum VpeEffect
	{
		ScreenSpaceReflections,
		AmbientOcclusion,
		ScreenSpaceGlobalIllumination,
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
		// Experimental: clear refractive materials (ramps, transparent plastics) are ray traced recursively.
		public bool RayTracedRecursiveRendering;
		public int Upscaler;        // VpeUpscaler
		public int UpscalerQuality; // VpeUpscalerQuality
		// The balls reflect the playfield and what stands on it where SSR finds nothing, instead of the sky.
		public bool BallReflections;
		public int MotionBlur; // VpeMotionBlur
		public int Shadows; // VpeShadows
		public int ReflectionQuality; // VpeReflectionQuality, while ScreenSpaceReflections is on
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

		/// <summary>
		/// Whether an upscaler runs on this machine, as the active pipeline reports it (it registers the check
		/// next to its applier). Off is always supported.
		/// </summary>
		public static bool IsUpscalerSupported(VpeUpscaler upscaler)
			=> upscaler == VpeUpscaler.Off || (UpscalerSupport != null && UpscalerSupport(upscaler));

		public static System.Func<VpeUpscaler, bool> UpscalerSupport;

		/// <summary>
		/// Whether the active pipeline, with the render tier (pipeline asset) in use right now, renders an
		/// effect at all: a tier can leave one out, and its switch then does nothing. Without a pipeline to
		/// ask, everything counts as supported.
		/// </summary>
		public static bool IsEffectSupported(VpeEffect effect) => EffectSupport == null || EffectSupport(effect);

		public static System.Func<VpeEffect, bool> EffectSupport;

		/// <summary>Whether the active pipeline applies <see cref="VpeGraphicsSettings.BallReflections"/> (it sets this next to its applier).</summary>
		public static bool BallReflectionsSupported;

		/// <summary>Whether the active pipeline applies <see cref="VpeGraphicsSettings.MotionBlur"/> (it sets this next to its applier).</summary>
		public static bool MotionBlurSupported;

		/// <summary>Whether the active pipeline can blur what's out of focus (it registers <see cref="DepthOfFieldHandler"/> next to its applier).</summary>
		public static bool DepthOfFieldSupported => DepthOfFieldHandler != null;

		/// <summary>
		/// Turns depth of field on the main camera on or off, focused on whatever is at the center of the screen.
		/// A table load brings its own volume, so the host sets it again after one.
		/// </summary>
		public static void SetDepthOfField(bool on) => DepthOfFieldHandler?.Invoke(on);

		public static System.Action<bool> DepthOfFieldHandler;

		/// <summary>The share of the output resolution the camera renders at (1 without upscaling), as the pipeline reports it.</summary>
		public static float RenderScale => RenderScaleSource?.Invoke() ?? 1f;

		public static System.Func<float> RenderScaleSource;

		/// <summary>
		/// What the pipeline renders a camera with right now, as it resolved it from the volumes, the camera
		/// and the pipeline asset: one line, for a log or a benchmark's record. It tells whether a setting
		/// arrived, which the settings themselves can't. Empty when the pipeline doesn't say.
		/// </summary>
		public static string DescribeEffective(UnityEngine.Camera camera) => camera != null ? EffectiveSettingsSource?.Invoke(camera) ?? "" : "";

		public static System.Func<UnityEngine.Camera, string> EffectiveSettingsSource;

		/// <summary>
		/// Lets the pipeline give back the render buffers it sized for an output that's gone, e.g. after a
		/// benchmark rendered at a higher resolution than the screen's. The pipelines that keep such buffers
		/// register <see cref="ReleaseRenderBuffersHandler"/> next to their applier.
		/// </summary>
		public static void ReleaseRenderBuffers(int width, int height) => ReleaseRenderBuffersHandler?.Invoke(width, height);

		public static System.Action<int, int> ReleaseRenderBuffersHandler;
	}
}
