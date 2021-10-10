// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Trough;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.VisualPinball.Unity.Patcher.Matcher
{
	[Api]
	public abstract class TablePatcher
	{
		public virtual void PostPatch(GameObject tableGo)
		{
			CreateTrough(tableGo, Playfield(tableGo));
		}

		#region GameObject Helpers

		/// <summary>
		/// Returns the playfield of a given table game object.
		/// </summary>
		/// <param name="tableGo">Table game object</param>
		/// <returns></returns>
		protected static GameObject Playfield(GameObject tableGo)
		{
			var pf = tableGo.GetComponentInChildren<PlayfieldComponent>();
			if (pf) {
				return pf.gameObject;
			}

			Debug.LogWarning($"Cannot find playfield of \"{tableGo.name}\".");
			return tableGo;

		}

		/// <summary>
		/// Creates an empty game object.
		/// </summary>
		/// <param name="parentGo">Parent of the new game object</param>
		/// <param name="name">Name of the new game object</param>
		/// <returns></returns>
		protected static GameObject CreateEmptyGameObject(GameObject parentGo, string name)
		{
			var newGo = new GameObject(name);
			newGo.transform.SetParent(parentGo.transform);
			return newGo;
		}

		protected static GameObject GetOrCreateGameObject(GameObject parentGo, string name)
		{
			for (var i = 0; i < parentGo.transform.childCount; i++) {
				if (parentGo.transform.GetChild(i).gameObject.name == name) {
					return parentGo.transform.GetChild(i).gameObject;
				}
			}

			return CreateEmptyGameObject(parentGo, name);
		}

		#endregion

		#region Element Helpers

		/// <summary>
		/// Creates a trough.
		/// </summary>
		/// <param name="tableGo">Table game object, for retrieving references</param>
		/// <param name="parentGo">Parent game object of the new trough</param>
		/// <param name="name">Name of the new trough</param>
		/// <param name="exitKicker">Name of the exit kicker</param>
		/// <param name="entrySwitch">Name of the entry switch</param>
		protected static void CreateTrough(GameObject tableGo, GameObject parentGo,
			string name = "Trough", string exitKicker = "BallRelease", string entrySwitch = "Drain")
		{
			var trough = new Trough(new TroughData {
				BallCount = 4,
				SwitchCount = 4,
				Type = TroughType.ModernMech,
			});

			var troughGo = trough.InstantiateEditorPrefab(parentGo.transform);
			var troughComponent = troughGo.GetComponent<TroughComponent>();

			var kickers = tableGo.GetComponentsInChildren<KickerComponent>();
			foreach (var kicker in kickers) {
				if (string.Equals(kicker.name, exitKicker, StringComparison.OrdinalIgnoreCase)) {
					troughComponent.PlayfieldExitKicker = kicker;
				}
				if (string.Equals(kicker.name, entrySwitch, StringComparison.OrdinalIgnoreCase)) {
					troughComponent.PlayfieldEntrySwitch = kicker;
				}
			}

			if (troughComponent.PlayfieldEntrySwitch == null) {
				var triggers = tableGo.GetComponentsInChildren<TriggerComponent>();
				foreach (var trigger in triggers) {

					if (string.Equals(trigger.name, entrySwitch, StringComparison.OrdinalIgnoreCase)) {
						troughComponent.PlayfieldEntrySwitch = trigger;
					}
				}
			}

			troughGo.name = name;
		}

		#endregion

		#region Light Helpers

		/// <summary>
		/// Adds a light group component to an existing game object.
		/// </summary>
		/// <param name="tableGo">Table game object for retrieving light references.</param>
		/// <param name="go">Game object to which the light group is added to.</param>
		/// <param name="names">A list of light names that are part of the light group.</param>
		protected static LightGroupComponent AddLightGroup(GameObject tableGo, GameObject go, params string[] names)
		{
			var nameIndex = new HashSet<string>(names);
			var lightComponentGroup = go.AddComponent<LightGroupComponent>();
			var lights = tableGo
				.GetComponentsInChildren<LightComponent>()
				.Where(lc => nameIndex.Contains(lc.name));
			lightComponentGroup.Lights = lights.ToArray();

			return lightComponentGroup;
		}

		/// <summary>
		/// Sets the light color of a light source.
		/// </summary>
		/// <remarks>
		/// Supports multiple light sources.
		/// </remarks>
		/// <param name="go">Game object of the light source</param>
		/// <param name="color">New color of the light source</param>
		protected static void LightColor(GameObject go, Color color)
		{
			var lights = go.GetComponentsInChildren<Light>();
			foreach (var light in lights) {
				RenderPipeline.Current.LightConverter.SetColor(light, color);
			}
		}

		/// <summary>
		/// Sets the angle of a spot light.
		/// </summary>
		/// <remarks>
		/// Supports multiple light sources.
		/// </remarks>
		/// <param name="go">Game object of the spot light</param>
		/// <param name="outer">Outer angle of the spot</param>
		/// <param name="inner">Inner angle of the spot, in percent of the outer angle</param>
		protected static void SpotAngle(GameObject go, float outer, float inner)
		{
			var lights = go.GetComponentsInChildren<Light>();
			foreach (var light in lights) {
				RenderPipeline.Current.LightConverter.SpotLight(light, outer, inner);
			}
		}

		/// <summary>
		/// Sets a light source to pyramid spotlight and sets its parameters.
		/// </summary>
		/// <remarks>
		/// Supports multiple light sources.
		/// </remarks>
		/// <param name="go">Game object of the light source</param>
		/// <param name="angle">Angle of the pyramid</param>
		/// <param name="ar">Aspect ratio of the pyramid</param>
		protected static void PyramidAngle(GameObject go, float angle, float ar)
		{
			var lights = go.GetComponentsInChildren<Light>();
			foreach (var light in lights) {
				RenderPipeline.Current.LightConverter.PyramidAngle(light, angle, ar);
			}
		}

		/// <summary>
		/// Sets the intensity of a light source in lumen.
		/// </summary>
		/// <remarks>
		/// Supports multiple light sources.
		/// </remarks>
		/// <param name="go">Game object of the light source</param>
		/// <param name="intensityLumen">Intensity of the light in lumen</param>
		protected static void Intensity(GameObject go, float intensityLumen)
		{
			var lights = go.GetComponentsInChildren<Light>();
			foreach (var light in lights) {
				RenderPipeline.Current.LightConverter.SetIntensity(light, intensityLumen);
			}
		}

		/// <summary>
		/// Sets the light position.
		/// </summary>
		/// <remarks>
		/// Supports multiple light sources. Note that this only applies to the light source, not to the light itself.
		/// </remarks>
		/// <param name="go">Game object of the light source</param>
		/// <param name="x">X-position of the source relative to the light</param>
		/// <param name="y">Y-position of the source relative to the light</param>
		/// <param name="z">Z-position of the source relative to the light</param>
		protected static void LightPos(GameObject go, float x, float y, float z)
		{
			var light = go.GetComponentInChildren<Light>();
			if (light != null) {
				light.gameObject.transform.localPosition = new Vector3(x, y, z);
			}
		}

		/// <summary>
		/// Duplicates a light source.
		/// </summary>
		/// <param name="go">Game object of the light source</param>
		/// <param name="x">X-position of the new light source, relative to the lamp</param>
		/// <param name="y">Y-position of the new light source, relative to the lamp</param>
		/// <param name="z">Z-position of the new light source, relative to the lamp</param>
		protected static void Duplicate(GameObject go, float x, float y, float z)
		{
			var light = go.GetComponentInChildren<Light>();
			if (light != null) {
				var newGo = Object.Instantiate(light.gameObject, go.transform, true);
				newGo.transform.localPosition = new Vector3(x, y, z);
			}
		}

		#endregion
	}
}
