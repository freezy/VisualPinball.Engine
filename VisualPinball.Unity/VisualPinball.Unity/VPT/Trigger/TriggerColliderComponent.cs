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
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity
{
	[PackAs("TriggerCollider")]
	[AddComponentMenu("Pinball/Collision/Trigger Collider")]
	public class TriggerColliderComponent : ColliderComponent<TriggerData, TriggerComponent>, IPackable
	{
		#region Data

		[Min(0)]
		[Tooltip("Height at which the trigger closes.")]
		public float HitHeight = 58f;

		[Min(0)]
		[Tooltip("Radius of the trigger.")]
		public float HitCircleRadius = 25f;

		[NonSerialized]
		internal FlipperComponent ForFlipper;

		[NonSerialized]
		internal float2[] FlipperPolarities;

		[NonSerialized]
		internal float2[] FlipperVelocities;

		[NonSerialized]
		internal uint TimeThresholdMs;

		#endregion

		#region Packaging

		public byte[] Pack() => TriggerColliderPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => PhysicalMaterialPackable.Pack(this, files);

		public void Unpack(byte[] bytes) => TriggerColliderPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) => PhysicalMaterialPackable.Unpack(data, this, files);

		#endregion

		#region Physics Material

		public override float PhysicsElasticity {
			get => 1;
			set { }
		}

		public override float PhysicsElasticityFalloff {
			get => 1;
			set { }
		}

		public override float PhysicsFriction {
			get => 0;
			set { }
		}

		public override float PhysicsScatter {
			get => 0;
			set { }
		}

		public override bool PhysicsOverwrite {
			get => true;
			set { }
		}

		#endregion

		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> MainComponent.TriggerApi ?? new TriggerApi(gameObject, player, physicsEngine);
	}
}
