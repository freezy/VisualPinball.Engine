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

using MemoryPack;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Editor.Packaging;
using VisualPinball.Unity.Packaging;

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

	public partial class ScriptableObjectPackable
	{
		/// <summary>
		/// This links the asset to the materials that use it.
		/// </summary>
		public int InstanceId;
		public ScriptableObject Object;

		public static byte[] Pack(ScriptableObject obj)
		{
			return PackageApi.Packer.Pack(new ScriptableObjectPackable {
				InstanceId = obj.GetInstanceID(),
				Object = obj,
			});
		}
	}

	[MemoryPackable]
	public partial struct PhysicalMaterialPackable
	{
		public float Elasticity;
		public float ElasticityFalloff;
		public float Friction;
		public float Scatter;
		public bool Overwrite;
		public int AssetRef;

		public static byte[] Pack(float elasticity, float elasticityFalloff, float friction,
			float scatter, bool overwrite, PhysicsMaterialAsset asset, PackagedFiles files)
		{
			return PackageApi.Packer.Pack(new PhysicalMaterialPackable {
				Elasticity = elasticity,
				ElasticityFalloff = elasticityFalloff,
				Friction = friction,
				Scatter = scatter,
				Overwrite = overwrite,
				AssetRef = files.AddAsset(asset),
			});
		}

		public PhysicalMaterialPackable Unpack(byte[] bytes)
		{
			var data = PackageApi.Packer.Unpack<PhysicalMaterialPackable>(bytes);
			return new PhysicalMaterialPackable {
				Elasticity = data.Elasticity,
				ElasticityFalloff = data.ElasticityFalloff,
				Friction = data.Friction,
				Scatter = data.Scatter,
				Overwrite = data.Overwrite,
				AssetRef = data.AssetRef,
			};
		}
	}

	[MemoryPackable]
	public partial struct PackableFloat3
	{
		public float X;
		public float Y;
		public float Z;

		public PackableFloat3(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public static implicit operator Vertex3D(PackableFloat3 v) => new(v.X, v.Y, v.Z);
		public static implicit operator PackableFloat3(Vertex3D v) => new(v.X, v.Y, v.Z);
		public static implicit operator Vector3(PackableFloat3 v) => new(v.X, v.Y, v.Z);
		public static implicit operator PackableFloat3(Vector3 v) => new(v.x, v.y, v.z);
	}
}
