// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using VisualPinball.Engine.VPT.Table;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{

	internal class VpxPrefab<TItem, TData, TMainComponent> : IVpxPrefab
		where TItem : Item<TData>
		where TData : ItemData
		where TMainComponent : MainComponent<TData>, IMainComponent
	{
		public GameObject GameObject { get; }
		public IMainComponent MainComponent => _mainComponent;
		public bool ExtractMesh { get; set; }
		public bool SkipParenting => false;

		private readonly TItem _item;
		private readonly MainComponent<TData> _mainComponent;
		private readonly List<MonoBehaviour> _updatedComponents = new List<MonoBehaviour>();

		public VpxPrefab(Object prefab, TItem item)
		{
			_item = item;
			if (!prefab) {
				throw new Exception($"Could not instantiate prefab for item {item} of type {item.GetType()}.");
			}

			GameObject = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
			GameObject!.name = item.Name;
			_mainComponent = GameObject.GetComponent<TMainComponent>();
			// if (_mainComponent && _mainComponent.HasProceduralMesh) {
			// 	PrefabUtility.UnpackPrefabInstance(GameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
			// }
		}

		public void SetData()
		{
			var updatedComponents = _mainComponent.SetData(_item.Data);
			_mainComponent.IsLocked = _item.Data.IsLocked;
			_mainComponent.EditorLayer = _item.Data.EditorLayer;
			_mainComponent.EditorLayerName = _item.Data.EditorLayerName;
			_mainComponent.EditorLayerVisibility = _item.Data.EditorLayerVisibility;
			_updatedComponents.AddRange(updatedComponents);
		}

		public void SetReferencedData(Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			var updatedComponents = _mainComponent.SetReferencedData(_item.Data, table, materialProvider, textureProvider, components);
			_updatedComponents.AddRange(updatedComponents);
			UpdateTransforms();
		}

		public void UpdateTransforms()
		{
			if (_mainComponent && _mainComponent is IMainRenderableComponent renderComponent) {
				renderComponent.UpdateTransforms();
			}
		}

		public void PersistData()
		{
			EditorUtility.SetDirty(GameObject);
			PrefabUtility.RecordPrefabInstancePropertyModifications(GameObject.transform);
			foreach (var comp in _updatedComponents.Distinct()) {
				if (comp) {
					PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
				}
			}
		}

		public void FreeBinaryData()
		{
			_item.Data.FreeBinaryData();
		}
	}
}
