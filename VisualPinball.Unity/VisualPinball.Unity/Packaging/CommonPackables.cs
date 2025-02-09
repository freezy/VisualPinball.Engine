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

using UnityEngine;
using VisualPinball.Engine.Math;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global

namespace VisualPinball.Unity
{
	public struct ReferencePackable
	{
		public string Path;
		public string Type;

		public ReferencePackable(string path, string type)
		{
			Path = path;
			Type = type;
		}
	}

	public struct DragPointPackable
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

	public class MetaPackable
	{
		/// <summary>
		/// This links the asset to the materials that use it.
		/// </summary>
		public int InstanceId;

		public static byte[] Pack(ScriptableObject obj)
		{
			return PackageApi.Packer.Pack(obj);
		}

		public static byte[] PackMeta(ScriptableObject obj) => PackageApi.Packer.Pack(new MetaPackable { InstanceId = obj.GetInstanceID() });

		public static MetaPackable UnpackMeta(byte[] data) => PackageApi.Packer.Unpack<MetaPackable>(data);
	}

	public struct PhysicalMaterialPackable
	{
		public float Elasticity;
		public float ElasticityFalloff;
		public float Friction;
		public float Scatter;
		public bool Overwrite;
		public int AssetRef;

		public static byte[] Pack(ICollidableComponent comp, PackagedFiles files)
		{
			return PackageApi.Packer.Pack(new PhysicalMaterialPackable {
				Elasticity = comp.PhysicsElasticity,
				ElasticityFalloff = comp.PhysicsElasticityFalloff,
				Friction = comp.PhysicsFriction,
				Scatter = comp.PhysicsScatter,
				Overwrite = comp.PhysicsOverwrite,
				AssetRef = files.AddAsset(comp.PhysicsMaterialReference),
			});
		}

		public static void Unpack(byte[] bytes, ICollidableComponent comp, PackagedFiles files)
		{
			var data = PackageApi.Packer.Unpack<PhysicalMaterialPackable>(bytes);
			comp.PhysicsElasticity = data.Elasticity;
			comp.PhysicsElasticityFalloff = data.ElasticityFalloff;
			comp.PhysicsFriction = data.Friction;
			comp.PhysicsScatter = data.Scatter;
			comp.PhysicsOverwrite = data.Overwrite;
			comp.PhysicsMaterialReference = files.GetAsset<PhysicsMaterialAsset>(data.AssetRef);
		}
	}

	public struct PackableFloat3
	{
		public float X;
		public float Y;
		public float Z;

		public PackableFloat3(float x, float y, float z) {
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
