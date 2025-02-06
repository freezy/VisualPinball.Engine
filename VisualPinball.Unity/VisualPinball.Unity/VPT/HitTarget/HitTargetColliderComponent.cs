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
using VisualPinball.Unity.Packaging;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Hit Target Collider")]
	[RequireComponent(typeof(HitTargetComponent))]
	public class HitTargetColliderComponent : ColliderComponent<HitTargetData, TargetComponent>
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

		public byte[] PackReferences(Transform root, PackNameLookup lookup, PackagedFiles files) =>
			PhysicalMaterialPackable.Pack(Elasticity, ElasticityFalloff, Friction, Scatter, OverwritePhysics, PhysicsMaterial, files);

		public void Unpack(byte[] bytes) => HitTargetColliderPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackNameLookup lookup, PackagedFiles files)
		{
			var mat = PhysicalMaterialPackable.Unpack(data);
			Elasticity = mat.Elasticity;
			ElasticityFalloff = mat.ElasticityFalloff;
			Friction = mat.Friction;
			Scatter = mat.Scatter;
			OverwritePhysics = mat.Overwrite;
			PhysicsMaterial = files.GetAsset<PhysicsMaterialAsset>(mat.AssetRef);
		}

		#endregion

		#region Physics Material

		protected override float PhysicsElasticity => Elasticity;
		protected override float PhysicsElasticityFalloff => ElasticityFalloff;
		protected override float PhysicsFriction => Friction;
		protected override float PhysicsScatter => Scatter;
		protected override bool PhysicsOverwrite => OverwritePhysics;

		#endregion

		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> (MainComponent as HitTargetComponent)?.HitTargetApi ?? new HitTargetApi(gameObject, player, physicsEngine);

		public override float4x4 GetLocalToPlayfieldMatrixInVpx(float4x4 worldToPlayfield)
			=> base.GetLocalToPlayfieldMatrixInVpx(worldToPlayfield).TransformToVpx();
	}
}
