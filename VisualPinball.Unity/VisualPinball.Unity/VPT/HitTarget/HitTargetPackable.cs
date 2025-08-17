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

// ReSharper disable MemberCanBePrivate.Global

namespace VisualPinball.Unity
{
	public struct HitTargetPackable
	{
		public static byte[] Pack(HitTargetComponent _) => PackageApi.Packer.Pack(new HitTargetPackable());

		public static void Unpack(byte[] bytes, HitTargetComponent comp)
		{
			// no data
		}
	}

	public struct HitTargetColliderPackable
	{
		public bool IsMovable;
		public float Threshold;

		public static byte[] Pack(HitTargetColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new HitTargetColliderPackable {
				IsMovable = comp._isKinematic,
				Threshold = comp.Threshold,
			});
		}

		public static void Unpack(byte[] bytes, HitTargetColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<HitTargetColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.Threshold = data.Threshold;
		}
	}

	public struct HitTargetColliderReferencesPackable
	{
		public PhysicalMaterialPackable PhysicalMaterial;
		public string FrontColliderMeshGuid;

		public static byte[] PackReferences(HitTargetColliderComponent comp, PackagedFiles files)
		{
			return PackageApi.Packer.Pack(new HitTargetColliderReferencesPackable {
				PhysicalMaterial = new PhysicalMaterialPackable {
					Elasticity = comp.Elasticity,
					ElasticityFalloff = comp.ElasticityFalloff,
					Friction = comp.Friction,
					Scatter = comp.Scatter,
					Overwrite = comp.OverwritePhysics,
					AssetRef = files.AddAsset(comp.PhysicsMaterial),
				},
				FrontColliderMeshGuid = files.GetColliderMeshGuid(comp, 0)
			});
		}

		public static void Unpack(byte[] bytes, HitTargetColliderComponent comp, PackagedFiles files)
		{
			var data = PackageApi.Packer.Unpack<HitTargetColliderReferencesPackable>(bytes);
			comp.Elasticity = data.PhysicalMaterial.Elasticity;
			comp.ElasticityFalloff = data.PhysicalMaterial.ElasticityFalloff;
			comp.Friction = data.PhysicalMaterial.Friction;
			comp.Scatter = data.PhysicalMaterial.Scatter;
			comp.OverwritePhysics = data.PhysicalMaterial.Overwrite;
			comp.PhysicsMaterial = files.GetAsset<PhysicsMaterialAsset>(data.PhysicalMaterial.AssetRef);
			comp.FrontColliderMesh = files.GetColliderMesh(data.FrontColliderMeshGuid, 0);
		}
	}

	public struct HitTargetAnimationPackable
	{
		public float Speed;
		public float MaxAngle;

		public static byte[] Pack(HitTargetAnimationComponentLegacy comp)
		{
			return PackageApi.Packer.Pack(new HitTargetAnimationPackable {
				Speed = comp.Speed,
				MaxAngle = comp.MaxAngle,
			});
		}

		public static void Unpack(byte[] bytes, HitTargetAnimationComponentLegacy comp)
		{
			var data = PackageApi.Packer.Unpack<HitTargetAnimationPackable>(bytes);
			comp.Speed = data.Speed;
			comp.MaxAngle = data.MaxAngle;
		}
	}

}
