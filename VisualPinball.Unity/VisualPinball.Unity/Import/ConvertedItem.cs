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
using VisualPinball.Engine.VPT;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity
{
	/// <summary>
	/// An untyped interface for <see cref="ConvertedItem{TItem,TData,TMainAuthoring}"/>.
	/// </summary>
	public interface IConvertedItem
	{
		/// <summary>
		/// Type of the main component. Used to check whether a sub component
		/// has the correct parent.
		/// </summary>
		Type MainAuthoringType { get; }

		/// <summary>
		/// Main authoring component
		/// </summary>
		IItemMainAuthoring MainAuthoring { get; }

		/// <summary>
		/// List of mesh authoring components
		/// </summary>
		IEnumerable<IItemMeshAuthoring> MeshAuthoring { get; }

		/// <summary>
		/// Collider authoring component
		/// </summary>
		IItemColliderAuthoring ColliderAuthoring { get; }

		/// <summary>
		/// If false, the mesh is saved as an asset in the asset folder.
		/// </summary>
		bool IsProceduralMesh { get; set; }

		/// <summary>
		/// Checks whether this item is correctly attached to a given parent.
		/// </summary>
		/// <param name="parent">Parent to check</param>
		/// <returns>true if valid, false otherwise.</returns>
		bool IsValidChild(IConvertedItem parent);

		/// <summary>
		/// Destroys the game item inclusively all children.
		/// </summary>
		public void Destroy();

		/// <summary>
		/// Destroys the game items of all mesh components. Should only be
		/// called when the mesh component sits on a different game object
		/// than the main component.
		/// </summary>
		public void DestroyMeshComponents();

		/// <summary>
		/// Destroys the collider component. If the collider component sits on
		/// a different game object than the main component, the game object
		/// is destroyed as well.
		/// </summary>
		public void DestroyColliderComponent();
	}

	/// <summary>
	/// A helper class that provides easy access to creating and destroying the
	/// various authoring components.
	/// </summary>
	/// <typeparam name="TItem">Item type</typeparam>
	/// <typeparam name="TData">Data type</typeparam>
	/// <typeparam name="TMainAuthoring">Main component type</typeparam>
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

		/// <summary>
		/// Instantiates for <b>new</b> items.
		/// </summary>
		/// <remarks>
		/// Used when importing a new table.
		/// </remarks>
		/// <param name="gameObject">Freshly created game object</param>
		/// <param name="item">Item to assign to</param>
		public ConvertedItem(GameObject gameObject, TItem item)
		{
			_gameObject = gameObject;
			_itemData = item.Data;

			_mainAuthoring = _gameObject.AddComponent<TMainAuthoring>();
			_mainAuthoring.SetItem(item);
		}

		/// <summary>
		/// Instantiates for <b>existing</b> items.
		/// </summary>
		/// <remarks>
		/// Used when just creating a new item with the toolbox.
		/// </remarks>
		/// <param name="gameObject">Existing game object which already has the main component set.</param>
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

		public void DestroyMeshComponents()
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
