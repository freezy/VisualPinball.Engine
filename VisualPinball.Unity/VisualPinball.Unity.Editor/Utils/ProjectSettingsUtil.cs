using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace VisualPinball.Unity.Editor
{
	public static class ProjectSettingsUtil
    {
		[MenuItem("Visual Pinball/Rendering/Restore Defaults (Forward Rendering)", false, 15)]
		public static void RestoreRenderingDefaultsForward()
		{
			EnableForwardRenderingPath();
			SetShadowMaskDefaults();
		}

		[MenuItem("Visual Pinball/Rendering/Restore Defaults (Deferred Rendering)", false, 16)]
		public static void RestoreRenderingDefaultsDeferred()
		{
			EnableDeferredRenderingPath();
			SetShadowMaskDefaults();
		}

		/// <summary>
		/// Enables the deferred rendering path for all graphics tiers for the current build target
		/// </summary>
		public static void EnableDeferredRenderingPath()
		{
			var buildTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
			for (var i = GraphicsTier.Tier1; i <= GraphicsTier.Tier3; i++) {
				var tierSettings = EditorGraphicsSettings.GetTierSettings(buildTarget, i);
				tierSettings.renderingPath = RenderingPath.DeferredShading;
				EditorGraphicsSettings.SetTierSettings(buildTarget, i, tierSettings);
			}
		}

		/// <summary>
		/// Enables (default) forward rendering path, and adjusts default values
		/// </summary>
		public static void EnableForwardRenderingPath()
		{
			var buildTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
			for (var i = GraphicsTier.Tier1; i <= GraphicsTier.Tier3; i++) {
				var tierSettings = EditorGraphicsSettings.GetTierSettings(buildTarget, i);
				tierSettings.renderingPath = RenderingPath.Forward;
				EditorGraphicsSettings.SetTierSettings(buildTarget, i, tierSettings);
			}
			// This light count is a bit bonkers, but playfields are a single mesh, and
			// with lots of inserts expected to be lit up we need a pretty high count
			QualitySettings.pixelLightCount = 150;
		}

		/// <summary>
		/// Sets VPE-appropriate defaults for shadow settings
		/// (since in Unity's defaults expect a much larger scene)
		/// </summary>
		public static void SetShadowMaskDefaults()
		{
			QualitySettings.shadowDistance = 4;
		}
    }
}
