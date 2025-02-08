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
		public float Threshold;

		public static byte[] Pack(HitTargetColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new HitTargetColliderPackable {
				Threshold = comp.Threshold,
			});
		}

		public static void Unpack(byte[] bytes, HitTargetColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<HitTargetColliderPackable>(bytes);
			comp.Threshold = data.Threshold;
		}
	}

	public struct HitTargetColliderReferencesPackable
	{
		public PhysicalMaterialPackable PhysicalMaterial;
		public string ColliderMeshGuid;

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
				ColliderMeshGuid = files.GetColliderMeshGuid(comp)
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
			comp.ColliderMesh = files.GetColliderMesh(data.ColliderMeshGuid);
		}
	}

}
