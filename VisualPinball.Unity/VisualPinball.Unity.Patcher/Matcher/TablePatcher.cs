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
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trough;
using VisualPinball.Unity.Editor;
using Light = UnityEngine.Light;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Patcher
{
	[Api]
	public abstract class TablePatcher
	{
		public TableContainer TableContainer;
		public ITextureProvider TextureProvider;
		public IMaterialProvider MaterialProvider;

		/// <summary>
		/// This method is executed once after all element-specific patches had
		/// been applied.<p/>
		///
		/// Override this method when you need to create new objects or make global
		/// changes to the project.
		/// </summary>
		/// <param name="tableGo">GameObject of the table.</param>
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
			newGo.transform.SetParent(parentGo.transform, false);
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

		/// <summary>
		/// Set a new parent for the given child while keeping the position and rotation.
		/// </summary>
		/// <param name="child"></param>
		/// <param name="parent"></param>
		protected static void Reparent(GameObject child, GameObject parent)
		{
			var rot = child.transform.rotation;
			var pos = child.transform.position;

			// re-parent the child
			child.transform.SetParent(parent.transform, false);

			child.transform.rotation = rot;
			child.transform.position = pos;
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
		protected static TroughComponent CreateTrough(GameObject tableGo, GameObject parentGo,
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
					troughComponent.PlayfieldExitKickerItem = kicker.AvailableCoils.First().Id;
				}
				if (string.Equals(kicker.name, entrySwitch, StringComparison.OrdinalIgnoreCase)) {
					troughComponent.PlayfieldEntrySwitch = kicker;
					troughComponent.PlayfieldEntrySwitchItem = kicker.AvailableSwitches.First().Id;
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
			return troughComponent;
		}

		protected T FindSiblingComponent<T>(MonoBehaviour mb, string name) where T : MonoBehaviour
		{
			return mb.gameObject.transform.parent.transform.Find(name).gameObject.GetComponent<T>();
		}

		/// <summary>
		/// Creates a drop target bank component.
		/// </summary>
		/// <param name="tableGo">Table game object, for retrieving references</param>
		/// <param name="parentGo">Parent game object of the new trough</param>
		/// <param name="name">Name of the new drop target bank</param>
		/// <param name="names">A list of drop targets that are in the drop target bank.</param>

		protected static DropTargetBankComponent CreateDropTargetBank(GameObject tableGo, GameObject go,
			string name = "DropTargetBank", params string[] dropTargetNames)
		{
			var playfieldGo = go.GetComponentInParent<PlayfieldComponent>().gameObject;
			var dropTargetBankParentGo = GetOrCreateGameObject(playfieldGo, "Drop Target Banks");

			var dropTargetBankGo = PrefabUtility.InstantiatePrefab(DropTargetBankComponent.LoadPrefab(), dropTargetBankParentGo.transform) as GameObject;
			var dropTargetBank = dropTargetBankGo!.GetComponent<DropTargetBankComponent>();

			var compIndex = tableGo.GetComponentsInChildren<DropTargetComponent>()
				.ToDictionary(dtc => dtc.name, dtc => dtc);
			var dropTargetComponents = dropTargetNames
				.Where(n => compIndex.ContainsKey(n))
				.Select(n => compIndex[n])
				.ToArray();

			dropTargetBank.name = name;
			dropTargetBank.BankSize = dropTargetComponents.Length;
			dropTargetBank.DropTargets = dropTargetComponents;

			return dropTargetBank;
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
		/// Sets the temperature of the light.
		/// </summary>
		/// <param name="go">Game object of the light</param>
		/// <param name="temp">Temperature in Kelvin</param>
		protected static void LightTemperature(GameObject go, float temp)
		{
			foreach (var l in go.GetComponentsInChildren<Light>()) {
				RenderPipeline.Current.LightConverter.SetTemperature(l, temp);
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
		/// Sets the angle of a spot light.
		/// </summary>
		/// <remarks>
		/// Supports multiple light sources.
		/// </remarks>
		/// <param name="go">Game object of the spot light</param>
		/// <param name="outer">Outer angle of the spot</param>
		/// <param name="inner">Inner angle of the spot, in percent of the outer angle</param>
		protected static void PointRange(GameObject go, float outer, float inner)
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
		protected static void LightIntensity(GameObject go, float intensityLumen)
		{
			var lights = go.GetComponentsInChildren<Light>();
			foreach (var light in lights) {
				RenderPipeline.Current.LightConverter.SetIntensity(light, intensityLumen);
			}
		}

		/// <summary>
		/// Sets the range of the light.
		/// </summary>
		/// <param name="go">Game object of the light</param>
		/// <param name="range">Range in meters</param>
		protected static void LightRange(GameObject go, float range)
		{
			var lights = go.GetComponentsInChildren<Light>();
			foreach (var light in lights) {
				RenderPipeline.Current.LightConverter.SetRange(light, range);
			}
		}

		/// <summary>
		/// Sets the shadow of the light.
		/// </summary>
		/// <param name="go">Game object of the light</param>
		/// <param name="enabled">Whether to enable or disable shadows.</param>
		/// <param name="isDynamic">If true, update on each frame.</param>
		/// <param name="nearPlane">Distance from when on shadows are cast.</param>
		protected static void LightShadow(GameObject go, bool enabled, bool isDynamic, float nearPlane = 0.01f)
		{
			var lights = go.GetComponentsInChildren<Light>();
			foreach (var light in lights) {
				RenderPipeline.Current.LightConverter.SetShadow(light, enabled, isDynamic, nearPlane);
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
		/// Creates a point light.
		/// </summary>
		/// <param name="name">Name of the new light</param>
		/// <param name="x">X-position on the playfield</param>
		/// <param name="y">Y-position on the playfield</param>
		/// <param name="parentGo">Game object to parent to (usually "Lights")</param>
		/// <returns></returns>
		protected static LightComponent CreateLight(string name, float x, float y, GameObject parentGo)
		{
			var light = Engine.VPT.Light.Light.GetDefault(name, x, y);
			light.Data.ShowBulbMesh = false;

			var prefab = RenderPipeline.Current.PrefabProvider.CreateLight();
			var lightGo = PrefabUtility.InstantiatePrefab(prefab, parentGo.transform) as GameObject;
			if (!lightGo) {
				return null;
			}
			lightGo.name = name;
			var lightTransform = lightGo.transform;
			var lightComponent = lightGo.GetComponent<LightComponent>();
			lightComponent.SetData(light.Data);
			lightComponent.UpdateTransforms();
			lightTransform.Find("Bulb").gameObject.SetActive(false);
			lightTransform.Find("Socket").gameObject.SetActive(false);
			return lightComponent;
		}

		/// <summary>
		/// Converts a normal light to an insert light, by deleting and re-creating the insert prefab.
		/// </summary>
		/// <param name="lo">Light component to convert</param>
		/// <returns>New converted game object</returns>
		protected GameObject ConvertToInsertLight(LightComponent lo)
		{
			var name = lo.name;
			var parent = lo.transform.parent.gameObject;
			Object.DestroyImmediate(lo.gameObject);
			return CreateInsertLight(TableContainer.Get<VisualPinball.Engine.VPT.Light.Light>(name).Data, parent);
		}

		/// <summary>
		/// Creates an insert light based on existing light data.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="parentGo"></param>
		private GameObject CreateInsertLight(LightData data, GameObject parentGo)
		{
			var prefab = RenderPipeline.Current.PrefabProvider.CreateInsertLight();
			var go = PrefabUtility.InstantiatePrefab(prefab, parentGo.transform) as GameObject;
			go!.name = data.Name;
			data.OffImage = TableContainer.Table.Data.Image;
			var lc = go.GetComponent<LightComponent>();
			lc.SetData(data);
			lc.SetReferencedData(data, TableContainer.Table, MaterialProvider, TextureProvider, null);

			EditorUtility.SetDirty(go);
			PrefabUtility.RecordPrefabInstancePropertyModifications(lc);

			return go;
		}

		/// <summary>
		/// Duplicates a light source.
		/// </summary>
		/// <param name="go">Game object of the light source</param>
		/// <param name="x">X-position of the new light source, relative to the lamp</param>
		/// <param name="y">Y-position of the new light source, relative to the lamp</param>
		/// <param name="z">Z-position of the new light source, relative to the lamp</param>
		protected static void DuplicateLight(GameObject go, float x, float y, float z)
		{
			var light = go.GetComponentInChildren<Light>();
			if (light != null) {
				var newGo = Object.Instantiate(light.gameObject, go.transform, true);
				newGo.transform.localPosition = new Vector3(x, y, z);
			}
		}

		/// <summary>
		/// Creates a light group with the given light names
		/// </summary>
		/// <param name="go">GameObject to add the light group to.</param>
		/// <param name="lightNames">Names of the light GameObjects. They must be sister objects of the first parameter.</param>
		protected static void LinkLights(GameObject go, params string[] lightNames)
		{
			var parentTransform = go.transform.parent;
			var lightComponents = lightNames
				.Select(n => parentTransform.Find(n).GetComponent<LightComponent>())
				.Where(c => c != null);
			var lg = go.AddComponent<LightGroupComponent>();
			lg.Lights = lightComponents.ToArray();
		}

		#endregion

		#region Mapping Helpers

		/// <summary>
		/// Links a coil device to an existing coil mapping.
		/// </summary>
		/// <param name="tableComponent">Table component for retrieving mappings.</param>
		/// <param name="coilId">The ID of the coil mapping that the coil device will be linked to</param>
		/// <param name="coilDevice">The coil device to be linked</param>
		/// <param name="deviceItem">If set, it's the device item, otherwise the first item of the device.</param>
		protected static void LinkCoil(TableComponent tableComponent, string coilId, ICoilDeviceComponent coilDevice, string deviceItem = null)
		{
			var coilMapping = tableComponent.MappingConfig.Coils.FirstOrDefault(cm => cm.Id == coilId);
			if (coilMapping == null) {
				return;
			}
			coilMapping.Device = coilDevice;
			coilMapping.DeviceItem = deviceItem ?? coilDevice.AvailableCoils.First().Id;
		}

		/// <summary>
		/// Links a coil device to an existing coil mapping if it matches a given name.
		/// </summary>
		/// <param name="tableComponent">Table component for retrieving mappings.</param>
		/// <param name="elementName">The name that the coil device's GameObject has to match in order to be linked.</param>
		/// <param name="coilId">The ID of the coil mapping that the coil device will be linked to</param>
		/// <param name="coilDevice">The coil device to be linked</param>
		/// <param name="deviceItem">If set, it's the device item, otherwise the first item of the device.</param>
		protected static void LinkCoil(TableComponent tableComponent, string elementName, string coilId, ICoilDeviceComponent coilDevice, string deviceItem = null)
		{
			if (!string.Equals(coilDevice.gameObject.name, elementName, StringComparison.OrdinalIgnoreCase)) {
				return;
			}
			LinkCoil(tableComponent, coilId, coilDevice, deviceItem);
		}

		/// <summary>
		/// Links a switch device to an existing switch mapping.
		/// </summary>
		/// <param name="tableComponent">Table component for retrieving mappings.</param>
		/// <param name="switchId">The ID of the switch mapping that the switch device will be linked to</param>
		/// <param name="switchDevice">The switch device to be linked</param>
		/// <param name="switchDeviceItem">Switch ID inside of the device item. If null, the first switch will be used.</param>
		protected static void LinkSwitch(TableComponent tableComponent, string switchId, ISwitchDeviceComponent switchDevice, string switchDeviceItem = null)
		{
			var switchMapping = tableComponent.MappingConfig.Switches.FirstOrDefault(sw => sw.Id == switchId);
			if (switchMapping == null) {
				return;
			}
			switchMapping.Device = switchDevice;
			switchMapping.DeviceItem = switchDeviceItem ?? switchDevice.AvailableSwitches.First().Id;
		}

		/// <summary>
		/// Links a switch device to an existing switch mapping if it matches a given name.
		/// </summary>
		/// <param name="tableComponent">Table component for retrieving mappings.</param>
		/// <param name="elementName">The name that the switch device's GameObject has to match in order to be linked.</param>
		/// <param name="switchId">The ID of the switch mapping that the switch device will be linked to</param>
		/// <param name="switchDevice">The switch device to be linked</param>
		protected static void LinkSwitch(TableComponent tableComponent, string elementName, string switchId, ISwitchDeviceComponent switchDevice)
		{
			if (!string.Equals(switchDevice.gameObject.name, elementName, StringComparison.OrdinalIgnoreCase)) {
				return;
			}
			LinkSwitch(tableComponent, switchId, switchDevice);
		}

		#endregion
	}
}
