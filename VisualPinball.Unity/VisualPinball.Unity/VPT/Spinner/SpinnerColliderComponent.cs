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
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity
{
	[PackAs("SpinnerCollider")]
	[AddComponentMenu("Visual Pinball/Collision/Spinner Collider")]
	public class SpinnerColliderComponent : ColliderComponent<SpinnerData, SpinnerComponent>, IPackable
	{
		#region Data

		[Min(0f)]
		[Tooltip("Bounciness (coefficient of restitution) of the spinner bracket.")]
		public float Elasticity = 0.3f;

		[Tooltip("Collider z-position relative to the spinner.")]
		public float ZPosition;

		#endregion

		#region Packaging

		public byte[] Pack() => SpinnerColliderPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => PhysicalMaterialPackable.Pack(this, files);

		public void Unpack(byte[] bytes) => SpinnerColliderPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) => PhysicalMaterialPackable.Unpack(data, this, files);

		#endregion

		#region Physics Material

		public override float PhysicsElasticity {
			get => Elasticity;
			set => Elasticity = value;
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
			=> MainComponent.SpinnerApi ?? new SpinnerApi(gameObject, player, physicsEngine);
	}
}
