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

using UnityEngine;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	[PackAs("PlungerCollider")]
	[AddComponentMenu("Pinball/Collision/Plunger Collider")]
	public class PlungerColliderComponent : ColliderComponent<PlungerData, PlungerComponent>, IPackable
	{
		#region Data

		[Min(0)]
		[Tooltip("How quick the plunger moves back.")]
		public float SpeedPull = 0.5f;

		[Min(0)]
		[Tooltip("How quick the plunger moves back when let go.")]
		public float SpeedFire = 80f;

		[Min(0)]
		public float Stroke = 80f;
		[Min(0)]
		public float ScatterVelocity;

		public bool IsMechPlunger;
		public bool IsAutoPlunger;

		[Min(0)]
		public float MechStrength = 85f;

		[Min(0)]
		public float MomentumXfer = 1f;

		[Range(0, 1f)]
		[Tooltip("At which position the plunger rests.")]
		public float ParkPosition = 0.5f / 3.0f;

		#endregion

		#region Packaging

		public byte[] Pack() => PlungerColliderPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => PlungerColliderPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion

		#region Physics Material

		public override float PhysicsElasticity {
			get => 1;
			set { }
		}

		public override float PhysicsElasticityFalloff
		{
			get => 1;
			set { }
		}

		public override float PhysicsFriction
		{
			get => 0;
			set { }
		}

		public override float PhysicsScatter
		{
			get => 0;
			set { }
		}

		public override bool PhysicsOverwrite
		{
			get => true;
			set { }
		}

		#endregion

		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> MainComponent.PlungerApi ?? new PlungerApi(gameObject, player, physicsEngine);
	}
}
