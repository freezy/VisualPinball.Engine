using MemoryPack;
using UnityEngine;
using VisualPinball.Engine.Math;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global

namespace VisualPinball.Unity
{
	[MemoryPackable]
	public partial struct ReferencePackable
	{
		public string Path;
		public string Type;

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
	public partial struct DragPointPackable
	{
		public string Id;
		public PackableFloat3 Center;
		public bool IsSmooth;
		public bool IsSlingshot;
		public bool HasAutoTexture;
		public float TextureCoord;
		public bool IsLocked;
		public int EditorLayer;
		public string EditorLayerName;
		public bool EditorLayerVisibility;
		public float CalcHeight;

		public static DragPointPackable From(DragPointData data)
		{
			return new DragPointPackable {
				Id = data.Id,
				Center = data.Center,
				IsSmooth = data.IsSmooth,
				IsSlingshot = data.IsSlingshot,
				HasAutoTexture = data.HasAutoTexture,
				TextureCoord = data.TextureCoord,
				IsLocked = data.IsLocked,
				EditorLayer = data.EditorLayer,
				EditorLayerName = data.EditorLayerName,
				EditorLayerVisibility = data.EditorLayerVisibility,
				CalcHeight = data.CalcHeight,
			};
		}

		public DragPointData ToDragPoint()
		{
			return new DragPointData(Center) {
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

	public readonly struct PackableFloat3
	{
		private readonly float _x;
		private readonly float _y;
		private readonly float _z;

		public PackableFloat3(float x, float y, float z)
		{
			_x = x;
			_y = y;
			_z = z;
		}

		public static implicit operator Vertex3D(PackableFloat3 v) => new(v._x, v._y, v._z);
		public static implicit operator PackableFloat3(Vertex3D v) => new(v.X, v.Y, v.Z);
	}
}
