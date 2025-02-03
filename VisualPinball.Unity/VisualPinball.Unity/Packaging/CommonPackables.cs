using MemoryPack;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity
{
	[MemoryPackable]
	public readonly partial struct ReferencePackable
	{
		public readonly string Path;
		public readonly string Type;

		public ReferencePackable(string path, string type)
		{
			Path = path;
			Type = type;
		}

		public T Resolve<T>(Transform root, PackNameLookup packNameLookup) where T: class
		{
			var transform = root.FindByPath(Path);
			if (transform == null) {
				Debug.LogError($"Error resolving reference {Type}@{Path}: No object found at path.");
				return null;
			}
			var type = packNameLookup.GetType(Type);
			if (type == null) {
				Debug.LogError($"Error resolving type name {Type} to type. PackAs[] attribute missing?");
				return null;
			}
			var component = transform.gameObject.GetComponent(type);

			if (component == null) {
				Debug.LogError($"Error resolving reference {Type}@{Path}: No component of type {type.FullName} on game object {transform.name}");
			}

			if (component is T compT) {
				return compT;
			}

			Debug.LogError($"Error resolving reference {Type}@{Path}: Component on {transform.name} required to be of type {typeof(T).FullName}, but is {component.GetType().FullName}.");
			return null;
		}

		public T Resolve<T, TI>(Transform root, PackNameLookup packNameLookup) where T: class
		{
			var component = Resolve<T>(root, packNameLookup);
			if (component is TI) {
				return component;
			}
			Debug.LogError($"Error resolving reference {Type}@{Path}: Component does not inherit {typeof(TI).FullName}.");
			return null;
		}
	}

	[MemoryPackable]
	public readonly partial struct DragPointPackable
	{
		public readonly string Id;
		public readonly float3 Center;
		public readonly bool IsSmooth;
		public readonly bool IsSlingshot;
		public readonly bool HasAutoTexture;
		public readonly float TextureCoord;
		public readonly bool IsLocked;
		public readonly int EditorLayer;
		public readonly string EditorLayerName;
		public readonly bool EditorLayerVisibility;
		public readonly float CalcHeight;

		[MemoryPackConstructor]
		public DragPointPackable(string id, float3 center, bool isSmooth, bool isSlingshot, bool hasAutoTexture, float textureCoord, bool isLocked, int editorLayer, string editorLayerName, bool editorLayerVisibility, float calcHeight)
		{
			Id = id;
			Center = center;
			IsSmooth = isSmooth;
			IsSlingshot = isSlingshot;
			HasAutoTexture = hasAutoTexture;
			TextureCoord = textureCoord;
			IsLocked = isLocked;
			EditorLayer = editorLayer;
			EditorLayerName = editorLayerName;
			EditorLayerVisibility = editorLayerVisibility;
			CalcHeight = calcHeight;
		}

		public DragPointPackable(DragPointData data)
		{
			Id = data.Id;
			Center = new float3(data.Center.X, data.Center.Y, data.Center.Z);
			IsSmooth = data.IsSmooth;
			IsSlingshot = data.IsSlingshot;
			HasAutoTexture = data.HasAutoTexture;
			TextureCoord = data.TextureCoord;
			IsLocked = data.IsLocked;
			EditorLayer = data.EditorLayer;
			EditorLayerName = data.EditorLayerName;
			EditorLayerVisibility = data.EditorLayerVisibility;
			CalcHeight = data.CalcHeight;
		}

		public DragPointData ToData()
		{
			return new DragPointData(new Vertex3D(Center.x, Center.y, Center.z)) {
				Id = Id,
				IsSmooth = IsSmooth,
				IsSlingshot = IsSlingshot,
				HasAutoTexture = HasAutoTexture,
				TextureCoord = TextureCoord,
				IsLocked = IsLocked,
				EditorLayer = EditorLayer,
				EditorLayerName = EditorLayerName,
				EditorLayerVisibility = EditorLayerVisibility,
				CalcHeight = CalcHeight
			};
		}
	}
}
