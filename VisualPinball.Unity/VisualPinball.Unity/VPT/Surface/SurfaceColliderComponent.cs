﻿// Visual Pinball Engine
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

using UnityEngine;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity
{
	[PackAs("SurfaceCollider")]
	[AddComponentMenu("Pinball/Collision/Surface Collider")]
	public class SurfaceColliderComponent : ColliderComponent<SurfaceData, SurfaceComponent>, IPackable
	{
		#region Data

		[Tooltip("If set, a hit event is triggered.")]
		public bool HitEvent;

		[Range(0, 100f)]
		[Tooltip("Minimal impact needed in order to trigger a hit event.")]
		public float Threshold = 2.0f;

		[Tooltip("Whether the bottom of the surface should collide (the top of the wall always collides).")]
		public bool IsBottomSolid;

		[Range(0, 100f)]
		[Tooltip("Minimal impact needed in order to trigger the slingshot segment.")]
		public float SlingshotThreshold;

		[Range(0, 500f)]
		[Tooltip("The force applied to the ball when hitting the slingshot segment.")]
		public float SlingshotForce = 80f;

		[Tooltip("Ignore the assigned physics material above and use the value below.")]
		public bool OverwritePhysics = true;

		[Min(0f)]
		[Tooltip("Bounciness, also known as coefficient of restitution. Higher is more bouncy.")]
		public float Elasticity = 0.8f;

		[Min(0f)]
		[Tooltip("How much to decrease elasticity for fast impacts.")]
		public float ElasticityFalloff;

		[Min(0)]
		[Tooltip("Friction of the material.")]
		public float Friction;

		[Range(-90f, 90f)]
		[Tooltip("When hit, add a random angle between 0 and this value to the trajectory.")]
		public float Scatter;

		#endregion

		#region Packaging

		public byte[] Pack() => SurfaceColliderPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> PhysicalMaterialPackable.Pack(this, files);

		public void Unpack(byte[] bytes) => SurfaceColliderPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files)
			=> PhysicalMaterialPackable.Unpack(data, this, files);

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
			=> MainComponent.SurfaceApi ?? new SurfaceApi(gameObject, player, physicsEngine);
	}
}
