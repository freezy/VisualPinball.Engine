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
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	[PackAs("HitTargetCollider")]
	[AddComponentMenu("Pinball/Collision/Hit Target Collider")]
	[RequireComponent(typeof(HitTargetComponent))]
	public class HitTargetColliderComponent : ColliderComponent<HitTargetData, TargetComponent>, IPackable, IColliderMesh
	{
		#region Data

		[Tooltip("The mesh that will be used for the collider.")]
		public Mesh ColliderMesh;

		[Min(0f)]
		[Tooltip("Bounciness, also known as coefficient of restitution. Higher is more bouncy.")]
		public float Elasticity = 0.35f;

		[Min(0f)]
		[Tooltip("How much to decrease elasticity for fast impacts.")]
		public float ElasticityFalloff = 0.5f;

		[Min(0)]
		[Tooltip("Friction of the material.")]
		public float Friction = 0.2f;

		[Range(-90f, 90f)]
		[Tooltip("When hit, add a random angle between 0 and this value to the trajectory.")]
		public float Scatter = 5f;

		[Tooltip("Ignore the assigned physics material above and use the value below.")]
		public bool OverwritePhysics = true;

		[Range(0, 100f)]
		[Tooltip("Minimal impact needed in order to trigger a hit event.")]
		public float Threshold = 2.0f;

		#endregion

		#region Packaging

		public byte[] Pack() => HitTargetColliderPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) =>
			HitTargetColliderReferencesPackable.PackReferences(this, files);

		public void Unpack(byte[] bytes) => HitTargetColliderPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) => HitTargetColliderReferencesPackable.Unpack(data, this, files);

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
			=> (MainComponent as HitTargetComponent)?.HitTargetApi ?? new HitTargetApi(gameObject, player, physicsEngine);

		public override float4x4 GetLocalToPlayfieldMatrixInVpx(float4x4 worldToPlayfield)
			=> base.GetLocalToPlayfieldMatrixInVpx(worldToPlayfield).TransformToVpx();

		public Mesh GetColliderMesh() => ColliderMesh;
	}
}
