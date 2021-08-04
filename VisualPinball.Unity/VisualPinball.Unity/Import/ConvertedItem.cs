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
		GameObject GameObject { get; }

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

		public bool IsPrefab { get; set; }

		/// <summary>
		/// Checks whether this item is correctly attached to a given parent.
		/// </summary>
		/// <param name="parent">Parent to check</param>
		/// <returns>true if valid, false otherwise.</returns>
		bool IsValidChild(IConvertedItem parent);

		/// <summary>
		/// Destroys the game item inclusively all children.
		/// </summary>
		void Destroy();

		/// <summary>
		/// Destroys the game items of all mesh components. Should only be
		/// called when the mesh component sits on a different game object
		/// than the main component.
		/// </summary>
		void DestroyMeshComponents();

		/// <summary>
		/// Destroys the collider component. If the collider component sits on
		/// a different game object than the main component, the game object
		/// is destroyed as well.
		/// </summary>
		void DestroyColliderComponent();

		void SetData(ItemData data, IMaterialProvider materialProvider, Dictionary<string, IItemMainAuthoring> itemMainAuthorings);
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
		public GameObject GameObject { get; }

		public Type MainAuthoringType => _mainAuthoring.GetType();

		public TMainAuthoring Authoring => _mainAuthoring;
		public IItemMainAuthoring MainAuthoring => _mainAuthoring;
		public IEnumerable<IItemMeshAuthoring> MeshAuthoring => _meshAuthoring;
		public IItemColliderAuthoring ColliderAuthoring => _colliderAuthoring;
		public bool IsProceduralMesh { get; set; } = true;
		public bool IsPrefab { get; set; }

		private readonly TMainAuthoring _mainAuthoring;
		private ItemColliderAuthoring<TItem, TData, TMainAuthoring> _colliderAuthoring;

		private readonly TData _itemData;
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
			GameObject = gameObject;
			_itemData = item.Data;

			_mainAuthoring = GameObject.AddComponent<TMainAuthoring>();
			if (_mainAuthoring == null) {
				var mainComp = GameObject.GetComponent<IItemMainAuthoring>();
				if (mainComp != null) {
					throw new Exception($"Prefab loaded but main component is of type {mainComp.GetType()} for item {item.Name} of type {item.GetType()}.");
				}
				throw new Exception($"Prefab loaded but has no main component applied ({item.Name}).");
			}
			_mainAuthoring.SetItem(item);
		}

		/// <summary>
		/// Instantiates for <b>existing</b> items.
		/// </summary>
		/// <remarks>
		/// Used when just creating a new item with the toolbox.
		/// </remarks>
		/// <param name="gameObject">Existing game object which already has the main component set.</param>
		public ConvertedItem(GameObject gameObject, bool isPrefab = false)
		{
			GameObject = gameObject;
			_itemData = GameObject.GetComponent<TMainAuthoring>().Data;
			_mainAuthoring = GameObject.GetComponentInChildren<TMainAuthoring>();
			_colliderAuthoring = GameObject.GetComponentInChildren<ItemColliderAuthoring<TItem,TData,TMainAuthoring>>();
			IsPrefab = isPrefab;
		}

		public void AddMeshAuthoring<T>(string name) where T : Component, IItemMeshAuthoring
		{
			var meshGo = new GameObject(name);
			var meshComp = meshGo.AddComponent<T>();
			meshGo.transform.SetParent(_mainAuthoring.transform, false);
			meshGo.layer = SceneTableContainer.ChildObjectsLayer;
			_meshAuthoring.Add(meshComp);
		}

		public void SetMeshAuthoring<T>() where T : Component, IItemMeshAuthoring
		{
			_meshAuthoring.Add(GameObject.AddComponent<T>());
		}

		public void SetColliderAuthoring<T>(IMaterialProvider materialProvider) where T : ItemColliderAuthoring<TItem, TData, TMainAuthoring>
		{
			if (!_mainAuthoring.IsCollidable) {
				return;
			}
			_colliderAuthoring = GameObject.AddComponent<T>();
			if (_itemData is IPhysicsMaterialData physicsMaterialData) {
				_colliderAuthoring.PhysicsMaterial = materialProvider.GetPhysicsMaterial(physicsMaterialData.GetPhysicsMaterial());
			}
		}

		public void SetAnimationAuthoring<T>(string name) where T : Component, IItemAnimationAuthoring
		{
			var go = GameObject.transform.Find(name).gameObject;
			go.AddComponent<T>();
		}

		public IConvertedItem AddConvertToEntity()
		{
			var cte = GameObject.AddComponent<ConvertToEntity>();
			cte.ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;

			return this;
		}

		public void Destroy()
		{
			_mainAuthoring.Destroy();
		}

		public void Destroy<T>() where T : Component
		{
			var childAuthoring = GameObject.GetComponentInChildren<T>();
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
		public void SetData(ItemData data, IMaterialProvider materialProvider, Dictionary<string, IItemMainAuthoring> itemMainAuthorings)
		{
			if (data is TData itemData) {
				_mainAuthoring.SetData(itemData, materialProvider, itemMainAuthorings);

			} else {
				throw new InvalidCastException($"Cannot set data of type {data.GetType()} to {_mainAuthoring.GetType()}");
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
