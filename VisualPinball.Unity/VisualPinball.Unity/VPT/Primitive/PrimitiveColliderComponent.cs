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

// ReSharper disable InconsistentNaming

using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity
{
	[PackAs("PrimitiveCollider")]
	[AddComponentMenu("Pinball/Collision/Primitive Collider")]
	public class PrimitiveColliderComponent : ColliderComponent<PrimitiveData, PrimitiveComponent>, IPackable, IColliderMesh
	{
		#region Data

		[Tooltip("If set, a hit event is triggered.")]
		public bool HitEvent = true;

		[Range(0, 100f)]
		[Tooltip("Minimal impact needed in order to trigger a hit event.")]
		public float Threshold = 2f;

		[Min(0f)]
		[Tooltip("Bounciness, also known as coefficient of restitution. Higher is more bouncy.")]
		public float Elasticity = 0.3f;

		[Min(0f)]
		[Tooltip("How much to decrease elasticity for fast impacts.")]
		public float ElasticityFalloff = 0.5f;

		[Min(0)]
		[Tooltip("Friction of the material.")]
		public float Friction = 0.3f;

		[Min(0f)]
		[Tooltip("Dimensionless rolling-resistance coefficient (Crr). Affects sustained rolling, not impacts.")]
		public float RollingResistance;

		[Range(-90f, 90f)]
		[Tooltip("When hit, add a random angle between 0 and this value to the trajectory.")]
		public float Scatter;

		[Range(0, 1f)]
		[Tooltip("Reduces triangles of the collider mesh for better performance. Be sure to verify it's what you want using the debug collider view.")]
		public float CollisionReductionFactor = 0;

		[Tooltip("Ignore the assigned physics material above and use the value below.")]
		public bool OverwritePhysics = true;

		#endregion

		#region Packaging

		public byte[] Pack() => PrimitiveColliderPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => PrimitiveColliderReferencesPackable.PackReferences(this, files);

		public void Unpack(byte[] bytes) => PrimitiveColliderPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) => PrimitiveColliderReferencesPackable.Unpack(data, this, files);

		#endregion

		#region Collider Mesh

		// A primitive's collider is built from its render mesh (GetUnityMesh). When the primitive is
		// visible its mesh is already in table.glb and the collider reuses it. When it's invisible
		// (renderer disabled — the zCol_/uCol_/*.Collider convention), the mesh is NOT in table.glb, so
		// it must travel in colliders.glb instead; expose it through IColliderMesh in that case only,
		// so visible meshes are never duplicated.
		private bool StoresColliderMesh
		{
			get {
				if (!MainComponent || !MainComponent.GetUnityMesh()) {
					return false;
				}
				var mr = MainComponent.GetComponent<MeshRenderer>();
				return !(mr && mr.enabled); // store separately only when not part of the visible export
			}
		}

		public int NumColliderMeshes => StoresColliderMesh ? 1 : 0;

		public Mesh GetColliderMesh(int index) => MainComponent ? MainComponent.GetUnityMesh() : null;

		#endregion

		#region Physics Material

		public override float PhysicsElasticity {
			get => Elasticity;
			set => Elasticity = value;
		}

		public override float PhysicsElasticityFalloff {
			get => ElasticityFalloff;
			set => ElasticityFalloff = value;
		}

		public override float PhysicsFriction {
			get => Friction;
			set => Friction = value;
		}

		public override float PhysicsRollingResistance {
			get => PhysicsMaterialData.SanitizeRollingResistance(RollingResistance);
			set => RollingResistance = PhysicsMaterialData.SanitizeRollingResistance(value);
		}

		public override float PhysicsScatter {
			get => Scatter;
			set => Scatter = value;
		}

		public override bool PhysicsOverwrite {
			get => OverwritePhysics;
			set => OverwritePhysics = value;
		}

		#endregion

		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=>MainComponent.PrimitiveApi ?? new PrimitiveApi(gameObject, player, physicsEngine);

		public override float4x4 GetLocalToPlayfieldMatrixInVpx(float4x4 worldToPlayfield)
			=> base.GetLocalToPlayfieldMatrixInVpx(worldToPlayfield).TransformToVpx();
	}
}
