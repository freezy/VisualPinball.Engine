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
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Kicker Collider")]
	public class KickerColliderComponent : ColliderComponent<KickerData, KickerComponent>
	{
		#region Data

		[Range(-90f, 90f)]
		[Tooltip("How many degrees of randomness is added to the ball trajectory when ejecting.")]
		public float Scatter;

		[Range(0f, 1f)]
		[Tooltip("How fast the ball gets caught by the trigger.")]
		public float HitAccuracy = 0.7f;

		[Tooltip("The height of the collider of the kicker.")]
		public float HitHeight = 40.0f;

		[Tooltip("Whether the ball continues moving through the kicker. If not set, the ball is frozen and kept in the kicker.")]
		public bool FallThrough;

		// don't expose for now
		public bool FallIn = true;

		[Tooltip("Use a better collision model. Currently disabled.")]
		public bool LegacyMode = true;

		#endregion

		private void Awake()
		{
			PhysicsEngine = GetComponentInParent<PhysicsEngine>();
		}

		public override PhysicsMaterialData PhysicsMaterialData => GetPhysicsMaterialData(scatterAngleDeg: Scatter);
		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine) =>
			MainComponent.KickerApi ?? new KickerApi(gameObject, player, physicsEngine);

		public override void OnTransformationChanged(float4x4 currTransformationMatrix)
		{
			// update kicker center, so the internal collision shape is correct
			ref var kickerData = ref PhysicsEngine.KickerState(ItemId);
			kickerData.Static.Center = currTransformationMatrix.c3.xy;
		}
	}
}
