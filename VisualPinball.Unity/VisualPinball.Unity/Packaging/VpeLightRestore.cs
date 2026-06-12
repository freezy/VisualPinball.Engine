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
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Restores authored light state after a glTF import. Shared by the runtime and the editor
	/// import paths so both produce the same lights.
	///
	/// Two steps: the export multiplies light intensities by
	/// <see cref="PackageApi.LightIntensityFactor"/> (glTF's lumen conversion swallows most of
	/// it), which the import divides back out; then the lights payload (meta/lights.json, the
	/// authored source of truth) is applied on top, overriding whatever survived the glTF
	/// round-trip.
	/// </summary>
	public static class VpeLightRestore
	{
		public static void NormalizeImportedLightIntensities(GameObject table)
		{
			if (!table || PackageApi.LightIntensityFactor <= 0f || Mathf.Approximately(PackageApi.LightIntensityFactor, 1f)) {
				return;
			}

			foreach (var light in table.GetComponentsInChildren<Light>(true)) {
				var originalIntensity = light.intensity;
				light.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
				light.intensity = originalIntensity / PackageApi.LightIntensityFactor;
				NormalizeHdrpLightIntensity(light, originalIntensity);
			}
		}

		private static void NormalizeHdrpLightIntensity(Light light, float originalUnityIntensity)
		{
			foreach (var component in light.GetComponents<Component>()) {
				if (component == null || component.GetType().FullName != "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData") {
					continue;
				}

				var intensityProperty = component.GetType().GetProperty("intensity");
				if (intensityProperty == null || !intensityProperty.CanRead || !intensityProperty.CanWrite) {
					continue;
				}

				var value = intensityProperty.GetValue(component);
				if (value is not float hdIntensity) {
					continue;
				}

				// HDRP usually mirrors Light.intensity. Only compensate separately if it still contains the
				// boosted glTF import value, otherwise setting Light.intensity above already normalized it.
				var tolerance = Mathf.Max(0.001f, Mathf.Abs(originalUnityIntensity) * 0.001f);
				if (Mathf.Abs(hdIntensity - originalUnityIntensity) <= tolerance) {
					intensityProperty.SetValue(component, hdIntensity / PackageApi.LightIntensityFactor);
				}
			}
		}

		/// <summary>
		/// Applies the lights payload (meta/lights.json) to the imported table. Lights resolve by
		/// node id, falling back to their index for lights on synthetic nodes.
		/// </summary>
		public static void RestoreLightProfiles(GameObject table, IPackageFolder tableFolder, Func<string, Transform> resolveNode)
		{
			if (!table || tableFolder == null || !tableFolder.TryGetFolder(PackageApi.MetaFolder, out var metaFolder)) {
				return;
			}

			if (!metaFolder.TryGetFile(PackageApi.LightsFile, out var payloadFile, PackageApi.Packer.FileExtension)) {
				return;
			}
			var payload = PackageApi.Packer.Unpack<VpeLightsPayload>(payloadFile.GetData());

			if (payload.Lights == null || payload.Lights.Count == 0) {
				return;
			}

			var matchedLights = new HashSet<int>();
			var lights = table.GetComponentsInChildren<Light>(true);
			for (var i = 0; i < payload.Lights.Count; i++) {
				var profile = payload.Lights[i];
				var target = ResolveLightProfileTarget(profile, lights, i, matchedLights, resolveNode);
				if (!target) {
					continue;
				}

				profile.Apply(target);
				matchedLights.Add(target.GetInstanceID());
			}
		}

		private static Light ResolveLightProfileTarget(
			LightSourcePackable profile,
			IReadOnlyList<Light> lights,
			int profileIndex,
			ISet<int> matchedLights,
			Func<string, Transform> resolveNode)
		{
			Transform transform = null;
			if (!string.IsNullOrEmpty(profile.NodeId) && resolveNode != null) {
				transform = resolveNode(profile.NodeId);
			}
			if (transform) {
				var exact = transform.GetComponent<Light>();
				if (exact) {
					return exact;
				}

				var descendant = transform.GetComponentsInChildren<Light>(true)
					.FirstOrDefault(light => !matchedLights.Contains(light.GetInstanceID()));
				if (descendant) {
					return descendant;
				}
			}

			return profileIndex >= 0 && profileIndex < lights.Count ? lights[profileIndex] : null;
		}
	}
}
