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

using System;
using UnityEngine;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	[PackAs("DropTargetCollider")]
	[AddComponentMenu("Pinball/Collision/Drop Target Collider")]
	[RequireComponent(typeof(DropTargetComponent))]
	public class DropTargetColliderComponent : ColliderComponent<HitTargetData, TargetComponent>, IPackable, IColliderMesh
	{
		#region Data

		[Tooltip("The collider mesh that will be used for the front side of the target and triggers the drop target.")]
		public Mesh FrontColliderMesh;

		[Tooltip("The collider mesh that will be used for the back side of the collider and doesn't trigger anything.")]
		public Mesh BackColliderMesh;

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

		[Tooltip("If set, send \"dropped\" and \"raised\" hit events.")]
		public bool UseHitEvent = true;

		#endregion

		#region Packaging

		public byte[] Pack() => DropTargetColliderPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> DropTargetColliderReferencesPackable.PackReferences(this, files);

		public void Unpack(byte[] bytes) => DropTargetColliderPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files)
			=> DropTargetColliderReferencesPackable.Unpack(data, this, files);

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
			=> (MainComponent as DropTargetComponent)?.DropTargetApi ?? new DropTargetApi(gameObject, player, physicsEngine);

		public int NumColliderMeshes => 2;
		public Mesh GetColliderMesh(int index)
		{
			return index switch {
				0 => FrontColliderMesh,
				1 => BackColliderMesh,
				_ => throw new ArgumentException($"Must be smaller than {NumColliderMeshes}")
			};
		}
	}
}
