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
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity
{
	public interface IConvertedItem
	{
		Type MainAuthoringType { get; }

		IItemMainAuthoring MainAuthoring { get; }
		IEnumerable<IItemMeshAuthoring> MeshAuthoring { get; }
		IItemColliderAuthoring ColliderAuthoring { get; }
		bool IsProceduralMesh { get; set; }

		bool IsValidChild(IConvertedItem parent);
		public void Destroy();
		public void DestroyMeshComponent();
		public void DestroyColliderComponent();
	}

	public class ConvertedItem<TItem, TData, TMainAuthoring> : IConvertedItem
		where TItem : Item<TData>
		where TData : ItemData
		where TMainAuthoring : ItemMainAuthoring<TItem, TData>, IItemMainAuthoring
	{
		public Type MainAuthoringType => _mainAuthoring.GetType();

		public TMainAuthoring Authoring => _mainAuthoring;
		public IItemMainAuthoring MainAuthoring => _mainAuthoring;
		public IEnumerable<IItemMeshAuthoring> MeshAuthoring => _meshAuthoring;
		public IItemColliderAuthoring ColliderAuthoring => _colliderAuthoring;
		public bool IsProceduralMesh { get; set; }

		private readonly TMainAuthoring _mainAuthoring;
		private ItemColliderAuthoring<TItem, TData, TMainAuthoring> _colliderAuthoring;

		private readonly TData _itemData;
		private readonly GameObject _gameObject;
		private readonly List<IItemMeshAuthoring> _meshAuthoring = new List<IItemMeshAuthoring>();

		public ConvertedItem(GameObject gameObject, TItem item)
		{
			_gameObject = gameObject;
			_itemData = item.Data;

			_mainAuthoring = _gameObject.AddComponent<TMainAuthoring>();
			_mainAuthoring.SetItem(item);
		}

		public ConvertedItem(GameObject gameObject)
		{
			_gameObject = gameObject;
			_itemData = _gameObject.GetComponent<TMainAuthoring>().Data;
		}

		public void AddMeshAuthoring<T>(string name) where T : Component, IItemMeshAuthoring
		{
			var meshGo = new GameObject(name);
			meshGo.transform.SetParent(_mainAuthoring.transform, false);
			var meshComp = meshGo.AddComponent<T>();
			meshGo.layer = SceneTableContainer.ChildObjectsLayer;
			_meshAuthoring.Add(meshComp);
		}

		public void SetMeshAuthoring<T>() where T : Component, IItemMeshAuthoring
		{
			var meshComp = _gameObject.AddComponent<T>();
			_meshAuthoring.Add(meshComp);
		}

		public void SetColliderAuthoring<T>(IMaterialProvider materialProvider) where T : ItemColliderAuthoring<TItem, TData, TMainAuthoring>
		{
			if (!_mainAuthoring.IsCollidable) {
				return;
			}
			_colliderAuthoring = _gameObject.AddComponent<T>();
			if (_itemData is IPhysicsMaterialData physicsMaterialData) {
				_colliderAuthoring.PhysicsMaterial = materialProvider.GetPhysicsMaterial(physicsMaterialData.GetPhysicsMaterial());
			}
		}

		public void SetAnimationAuthoring<T>(string name) where T : Component, IItemAnimationAuthoring
		{
			var go = _gameObject.transform.Find(name).gameObject;
			go.AddComponent<T>();
		}

		public IConvertedItem AddConvertToEntity()
		{
			_gameObject.AddComponent<ConvertToEntity>();
			return this;
		}

		public void Destroy()
		{
			_mainAuthoring.Destroy();
		}

		public void Destroy<T>() where T : Component
		{
			var childAuthoring = _gameObject.GetComponentInChildren<T>();
			if (childAuthoring) {
				Object.DestroyImmediate(childAuthoring.gameObject);
			}
		}

		public void DestroyMeshComponent()
		{
			if (_mainAuthoring is IItemMainRenderableAuthoring renderableAuthoring) {
				renderableAuthoring.DestroyMeshComponent();
				_meshAuthoring.Clear();
			}
		}

		public void DestroyColliderComponent()
		{
			if (_mainAuthoring is IItemMainRenderableAuthoring renderableAuthoring) {
				renderableAuthoring.DestroyColliderComponent();
				_colliderAuthoring = null;
			}
		}

		public bool IsValidChild(IConvertedItem parent)
		{
			if (MeshAuthoring.Any()) {
				return MeshAuthoring.First().ValidParents.Contains(parent.MainAuthoringType);
			}

			if (_colliderAuthoring != null) {
				return _colliderAuthoring.ValidParents.Contains(parent.MainAuthoringType);
			}

			return _mainAuthoring.ValidParents.Contains(parent.MainAuthoringType);
		}
	}
}
