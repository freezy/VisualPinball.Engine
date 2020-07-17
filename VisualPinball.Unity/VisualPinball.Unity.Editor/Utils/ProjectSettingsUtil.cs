using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace VisualPinball.Unity.Editor.Utils
{
	public static class ProjectSettingsUtil
    {
		/// <summary>
		/// Adjusts project settings to match VPE's "defaults"
		/// </summary>
		public static void SetAllDefaults()
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
		/// Sets VPE-appropriate defaults for shadow settings
		/// (since in Unity's defaults expect a much larger scene)
		/// </summary>
		public static void SetShadowMaskDefaults()
		{
			QualitySettings.shadowDistance = 4;
		}
    }
}
