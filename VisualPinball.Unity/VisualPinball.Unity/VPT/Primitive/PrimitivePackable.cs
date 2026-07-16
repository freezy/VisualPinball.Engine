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
	public struct PrimitivePackable
	{
		public static byte[] Pack(PrimitiveComponent _) => PackageApi.Packer.Pack(new PrimitivePackable());

		public static void Unpack(byte[] bytes, PrimitiveComponent comp)
		{
			// no data
		}
	}

	public struct PrimitiveColliderPackable
	{
		public bool IsMovable;
		public bool HitEvent;
		public float Threshold;
		public float CollisionReductionFactor;

		public static byte[] Pack(PrimitiveColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new PrimitiveColliderPackable {
				IsMovable = comp._isKinematic,
				HitEvent = comp.HitEvent,
				Threshold = comp.Threshold,
				CollisionReductionFactor = comp.CollisionReductionFactor,
			});
		}

		public static void Unpack(byte[] bytes, PrimitiveColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<PrimitiveColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.HitEvent = data.HitEvent;
			comp.Threshold = data.Threshold;
			comp.CollisionReductionFactor = data.CollisionReductionFactor;
		}
	}

	public struct PrimitiveColliderReferencesPackable
	{
		public PhysicalMaterialPackable PhysicalMaterial;
		// GUID of the collider mesh stored in colliders.glb — set only for invisible collider
		// primitives (the visible ones keep their mesh in table.glb and report none, leaving this empty).
		public string ColliderMeshGuid;

		public static byte[] PackReferences(PrimitiveColliderComponent comp, PackagedFiles files)
		{
			return PackageApi.Packer.Pack(new PrimitiveColliderReferencesPackable {
				PhysicalMaterial = new PhysicalMaterialPackable {
					Elasticity = comp.PhysicsElasticity,
					ElasticityFalloff = comp.PhysicsElasticityFalloff,
					Friction = comp.PhysicsFriction,
					RollingResistance = comp.PhysicsRollingResistance,
					Scatter = comp.PhysicsScatter,
					Overwrite = comp.PhysicsOverwrite,
					AssetRef = files.AddAsset(comp.PhysicsMaterialReference),
				},
				ColliderMeshGuid = files.GetColliderMeshGuid(comp, 0),
			});
		}

		public static void Unpack(byte[] bytes, PrimitiveColliderComponent comp, PackagedFiles files)
		{
			var data = PackageApi.Packer.Unpack<PrimitiveColliderReferencesPackable>(bytes);
			var pm = data.PhysicalMaterial;
			comp.PhysicsElasticity = pm.Elasticity;
			comp.PhysicsElasticityFalloff = pm.ElasticityFalloff;
			comp.PhysicsFriction = pm.Friction;
			comp.PhysicsRollingResistance = pm.RollingResistance;
			comp.PhysicsScatter = pm.Scatter;
			comp.PhysicsOverwrite = pm.Overwrite;
			comp.PhysicsMaterialReference = files.GetAsset<PhysicsMaterialAsset>(pm.AssetRef);

			// Re-attach the collider mesh that travelled in colliders.glb (invisible primitives only),
			// so the runtime collider generator — which reads MeshFilter.sharedMesh via GetUnityMesh —
			// finds it. Visible primitives leave the GUID empty and reuse the table.glb mesh.
			if (!string.IsNullOrEmpty(data.ColliderMeshGuid) && comp.MainComponent) {
				comp.MainComponent.SetUnityMesh(files.GetColliderMesh(data.ColliderMeshGuid, 0));
			}
		}
	}

	public struct PrimitiveMeshPackable
	{
		public bool UseLegacyMesh;
		public int Sides;

		public static byte[] Pack(PrimitiveMeshComponent comp)
		{
			return PackageApi.Packer.Pack(new PrimitiveMeshPackable {
				UseLegacyMesh = comp.UseLegacyMesh,
				Sides = comp.Sides,
			});
		}

		public static void Unpack(byte[] bytes, PrimitiveMeshComponent comp)
		{
			var data = PackageApi.Packer.Unpack<PrimitiveMeshPackable>(bytes);
			comp.UseLegacyMesh = data.UseLegacyMesh;
			comp.Sides = data.Sides;
		}
	}
}
