using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class ConvertedItem
	{
		public readonly IItemMainAuthoring MainAuthoring;
		public IEnumerable<IItemMeshAuthoring> MeshAuthoring;
		public IItemColliderAuthoring ColliderAuthoring;

		public ConvertedItem()
		{
			MainAuthoring = null;
			MeshAuthoring = new IItemMeshAuthoring[0];
			ColliderAuthoring = null;
		}

		public ConvertedItem(IItemMainAuthoring mainAuthoring)
		{
			MainAuthoring = mainAuthoring;
			MeshAuthoring = new IItemMeshAuthoring[0];
			ColliderAuthoring = null;
		}

		public ConvertedItem(IItemMainAuthoring mainAuthoring, IEnumerable<IItemMeshAuthoring> meshAuthoring)
		{
			MainAuthoring = mainAuthoring;
			MeshAuthoring = meshAuthoring;
			ColliderAuthoring = null;
		}

		public ConvertedItem(IItemMainAuthoring mainAuthoring, IEnumerable<IItemMeshAuthoring> meshAuthoring, IItemColliderAuthoring colliderAuthoring)
		{
			MainAuthoring = mainAuthoring;
			MeshAuthoring = meshAuthoring;
			ColliderAuthoring = colliderAuthoring;
		}

		public void Destroy()
		{
			MainAuthoring.Destroy();
		}

		public void DestroyMeshComponent()
		{
			if (MainAuthoring is IItemMainRenderableAuthoring renderableAuthoring) {
				renderableAuthoring.DestroyMeshComponent();
				MeshAuthoring = new IItemMeshAuthoring[0];
			}
		}

		public void DestroyColliderComponent()
		{
			if (MainAuthoring is IItemMainRenderableAuthoring renderableAuthoring) {
				renderableAuthoring.DestroyColliderComponent();
				ColliderAuthoring = null;
			}
		}

		public bool IsValidChild(ConvertedItem parent)
		{
			if (MeshAuthoring.Any()) {
				return MeshAuthoring.First().ValidParents.Contains(parent.MainAuthoring.GetType());
			}

			if (ColliderAuthoring != null) {
				return ColliderAuthoring.ValidParents.Contains(parent.MainAuthoring.GetType());
			}

			return MainAuthoring.ValidParents.Contains(parent.MainAuthoring.GetType());
		}

		public static T CreateChild<T>(GameObject obj, string name) where T : MonoBehaviour, IItemMeshAuthoring
		{
			var subObj = new GameObject(name);
			subObj.transform.SetParent(obj.transform, false);
			var comp = subObj.AddComponent<T>();
			subObj.layer = VpxConverter.ChildObjectsLayer;
			return comp;
		}
	}
}
