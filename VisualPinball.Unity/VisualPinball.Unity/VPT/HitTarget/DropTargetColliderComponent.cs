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

		[Tooltip("An optional dedicated collision mesh used by advanced drop-target physics.")]
		public Mesh CollisionColliderMesh;

		[Tooltip("Selects legacy, Roth-compatible, or mechanical drop-target physics. Existing content defaults to Legacy.")]
		public DropTargetPhysicsMode PhysicsMode = DropTargetPhysicsMode.Legacy;

		[Tooltip("Reusable mechanical parameters. The local overrides are used when this is empty or overrides are enabled.")]
		public DropTargetPhysicsProfile MechanicalProfile;

		[Tooltip("Use the local mechanical configuration instead of the assigned profile.")]
		public bool OverrideMechanicalProfile;

		public DropTargetMechanicalConfig MechanicalOverrides = DropTargetMechanicalConfig.Default;
		public DropTargetRothConfig RothConfig = DropTargetRothConfig.Default;

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

		public DropTargetMechanicalConfig ResolvedMechanicalConfig
			=> MechanicalProfile != null && !OverrideMechanicalProfile
				? MechanicalProfile.Config
				: MechanicalOverrides;

		private void OnValidate()
		{
			// Newly added serialized struct fields can be all-zero on existing scene
			// components. Zero mass/travel is not a valid authored profile, so use it
			// as the migration sentinel before an author opts into an advanced mode.
			if (MechanicalOverrides.EffectiveFaceMass <= 0f || MechanicalOverrides.DropTravel <= 0f) {
				MechanicalOverrides = DropTargetMechanicalConfig.Default;
			}
			if (RothConfig.TargetMass <= 0f || RothConfig.DropTravel <= 0f) {
				RothConfig = DropTargetRothConfig.Default;
			}
		}

		public int NumColliderMeshes => 3;
		public Mesh GetColliderMesh(int index)
		{
			return index switch {
				0 => FrontColliderMesh,
				1 => BackColliderMesh,
				2 => CollisionColliderMesh,
				_ => throw new ArgumentException($"Must be smaller than {NumColliderMeshes}")
			};
		}
	}
}
