// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Flipper Collider")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/flippers.html")]
	public class FlipperColliderAuthoring : ItemColliderAuthoring<Flipper, FlipperData, FlipperAuthoring>
	{
		#region Data

		[Min(0f)]
		[Tooltip("Mass of the flipper. 1 = 80g (ball weight).")]
		public float Mass = 1f;

		[Min(0f)]
		[Tooltip("This is the force (actually torque) with which the solenoid accelerates the flipper.")]
		public float Strength = 2200f;

		[Min(0f)]
		[Tooltip("Bounciness (coefficient of restitution) of the flipper.")]
		public float Elasticity = 0.8f;

		[Min(0f)]
		[Tooltip("How much to decrease elasticity for fast impacts.")]
		public float ElasticityFalloff = 0.43f;

		[Min(0f)]
		[Tooltip("How much the rubber \"grips\" the ball.")]
		public float Friction = 0.6f;

		[Min(0f)]
		[Tooltip("The force of the return spring that pulls the flipper back down.")]
		public float Return = 0.058f;

		[Min(0f)]
		[Tooltip("How long it takes the flipper to reach full force. In 10s of milliseconds, e.g. a value of 3 means 30ms.")]
		public float RampUp = 3f;

		[Min(0f)]
		[Tooltip("The force that holds the flipper up once it reached the end position.")]
		public float TorqueDamping = 0.75f;

		[Min(0f)]
		[Tooltip("How many degrees from the end position the EOS torque force is applied.")]
		public float TorqueDampingAngle = 6f;

		[Range(-90f, 90f)]
		[Tooltip("How many degrees of randomness is added to the ball trajectory.")]
		public float Scatter;

		/// <summary>
		/// If set, apply flipper correction (aka nFozzy)
		/// </summary>
		[Tooltip("The infamous nFozzy flipper correction. Choose a preset or create your own.")]
		public FlipperCorrectionAsset FlipperCorrection;

		#endregion

		public static readonly Type[] ValidParentTypes = new Type[0];
		public override IEnumerable<Type> ValidParents => ValidParentTypes;
		protected override IApiColliderGenerator InstantiateColliderApi(Player player, Entity entity, Entity parentEntity)
			=> new FlipperApi(Item, gameObject, entity, parentEntity, player);

	}
}
