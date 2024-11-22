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
	[AddComponentMenu("Visual Pinball/Collision/Trigger Collider")]
	public class TriggerColliderComponent : ColliderComponent<TriggerData, TriggerComponent>, IKinematicColliderComponent
	{
		#region Data

		[Min(0)]
		[Tooltip("Height at which the trigger closes.")]
		public float HitHeight = 50f;

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

		[Tooltip("If set, transforming this object will transform the colliders as well.")]
		public bool _isKinematic;

		#endregion

		public override PhysicsMaterialData PhysicsMaterialData => GetPhysicsMaterialData();
		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> MainComponent.TriggerApi ?? new TriggerApi(gameObject, player, physicsEngine);

		#region IKinematicColliderComponent

		public bool IsKinematic => _isKinematic;
		public int ItemId => MainComponent.gameObject.GetInstanceID();
		public float4x4 TransformationWithinPlayfield => MainComponent.TransformationMatrix;

		#endregion
	}
}
