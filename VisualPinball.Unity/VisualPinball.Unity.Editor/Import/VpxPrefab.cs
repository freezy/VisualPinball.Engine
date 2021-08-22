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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{

	internal class VpxPrefab<TItem, TData, TMainAuthoring> : IVpxPrefab
		where TItem : Item<TData>
		where TData : ItemData
		where TMainAuthoring : ItemMainAuthoring<TItem, TData>, IItemMainAuthoring
	{
		public GameObject GameObject { get; }
		public IItemMainAuthoring MainComponent => _mainComponent;
		public MeshFilter[] MeshFilters => GameObject.GetComponentsInChildren<MeshFilter>();
		public IRenderable Renderable => _item as IRenderable;
		public bool ExtractMesh { get; set; }
		public bool SkipParenting => false;

		public IEnumerable<GameObject> MeshGameObjects => _item is IRenderable
			? GameObject.GetComponentsInChildren<MeshFilter>().Select(mf => mf.gameObject)
			: Array.Empty<GameObject>();

		private readonly TItem _item;
		private readonly ItemMainAuthoring<TItem, TData> _mainComponent;
		private readonly List<MonoBehaviour> _updatedComponents = new List<MonoBehaviour>();

		public VpxPrefab(Object prefab, TItem item)
		{
			_item = item;
			GameObject = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
			GameObject!.name = item.Name;
			_mainComponent = GameObject.GetComponent<TMainAuthoring>();
		}

		public void SetData()
		{
			var updatedComponents = _mainComponent.SetData(_item.Data);
			_updatedComponents.AddRange(updatedComponents);
		}

		public void SetReferencedData(IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			var updatedComponents = _mainComponent.SetReferencedData(_item.Data, materialProvider, textureProvider, components);
			_updatedComponents.AddRange(updatedComponents);
			if (_mainComponent is IItemMainRenderableAuthoring renderComponent) {
				renderComponent.UpdateTransforms();
			}
		}

		public void PersistData()
		{
			EditorUtility.SetDirty(GameObject);
			PrefabUtility.RecordPrefabInstancePropertyModifications(GameObject.transform);
			foreach (var comp in _updatedComponents.Distinct()) {
				PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
			}
		}

		public void FreeBinaryData()
		{
			_item.Data.FreeBinaryData();
		}
	}
}
