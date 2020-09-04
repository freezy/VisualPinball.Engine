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
