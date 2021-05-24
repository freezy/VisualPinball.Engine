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

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class ConvertedComponent
	{
		public IEnumerable<ItemMeshComponent> MeshComponents => _meshComponents;

		private readonly GameObject _gameObject;
		private Component _mainComponent;
		private readonly List<ItemMeshComponent> _meshComponents = new List<ItemMeshComponent>();

		public ConvertedComponent(GameObject gameObject)
		{
			_gameObject = gameObject;
		}

		public T AddMainComponent<T>() where T : Component
		{
			var comp = _gameObject.AddComponent<T>();
			_mainComponent = comp;
			return comp;
		}

		public T AddMeshComponent<T>(string name) where T : ItemMeshComponent
		{
			var meshGo = new GameObject(name);
			meshGo.transform.SetParent(_mainComponent.transform, false);
			var meshComp = meshGo.AddComponent<T>();
			meshGo.layer = VpxConverter.ChildObjectsLayer;
			_meshComponents.Add(meshComp);
			return meshComp;
		}

		public ConvertedComponent AddConvertToEntity()
		{
			_gameObject.AddComponent<ConvertToEntity>();
			return this;
		}
	}
}
