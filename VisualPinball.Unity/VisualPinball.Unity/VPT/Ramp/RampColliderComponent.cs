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
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Ramp Collider")]
	public class RampColliderComponent : ColliderComponent<RampData, RampComponent>, IKinematicColliderComponent
	{
		#region Data

		[Tooltip("If set, a hit event is triggered.")]
		public bool HitEvent;

		[Range(0, 100f)]
		[Tooltip("Minimal impact needed in order to trigger a hit event.")]
		public float Threshold;

		[Min(0)]
		[Tooltip("Collider height of the left wall.")]
		public float LeftWallHeight = 62f;

		[Min(0)]
		[Tooltip("Collider height of the right wall.")]
		public float RightWallHeight = 62f;

		[Tooltip("Ignore the assigned physics material above and use the value below.")]
		public bool OverwritePhysics = true;

		[Min(0f)]
		[Tooltip("Bounciness, also known as coefficient of restitution. Higher is more bouncy.")]
		public float Elasticity;

		[Min(0)]
		[Tooltip("Friction of the material.")]
		public float Friction;

		[Range(-90f, 90f)]
		[Tooltip("When hit, add a random angle between 0 and this value to the trajectory.")]
		public float Scatter;

		[Tooltip("If set, transforming this object will transform the colliders as well.")]
		public bool _isKinematic;

		#endregion

		#region IKinematicColliderComponent

		public bool IsKinematic => _isKinematic;
		public int ItemId => MainComponent.gameObject.GetInstanceID();

		#endregion

		public override PhysicsMaterialData PhysicsMaterialData => GetPhysicsMaterialData(Elasticity, friction: Friction, scatterAngleDeg: Scatter, overwrite: OverwritePhysics);
		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> MainComponent.RampApi ?? new RampApi(gameObject, player, physicsEngine);
	}
}
