using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class ConvertedComponent
	{
		public List<ItemMeshComponent> MeshComponents => _meshComponents;

		private readonly GameObject _gameObject;
		private Component _mainComponent;
		private List<ItemMeshComponent> _meshComponents = new List<ItemMeshComponent>();

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
			var subObj = new GameObject(name);
			subObj.transform.SetParent(_mainComponent.transform, false);
			var comp = subObj.AddComponent<T>();
			subObj.layer = VpxConverter.ChildObjectsLayer;
			_meshComponents.Add(comp);
			return comp;
		}
	}
}
