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
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision/Flipper Collider")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/flippers.html")]
	public class FlipperColliderComponent : ColliderComponent<FlipperData, FlipperComponent>, IKinematicColliderComponent
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

		public override PhysicsMaterialData PhysicsMaterialData => GetPhysicsMaterialData(Elasticity, ElasticityFalloff, Friction, Scatter);

		#region IKinematicColliderComponent

		[Tooltip("If set, transforming this object during gameplay will transform the colliders as well.")]
		public bool _isKinematic;

		public bool IsKinematic => _isKinematic;
		public int ItemId => MainComponent.gameObject.GetInstanceID();
		public float4x4 TransformationWithinPlayfield => MainComponent.LocalToWorldPhysicsMatrix;

		#endregion

		#region FlipperTricks
		/// <summary>
		/// If set, apply Flipper Tricks Physics (nFozzy/RothBauerW)
		/// </summary>

		[Tooltip("The Rothbauerw's Flipper Tricks Physics")]
		public bool useFlipperTricksPhysics = false;

		[Min(0f)]
		[Tooltip("Start of stroke RampUp")]
		public float SOSRampUp = 2.5f;

		[Min(0f)]
		[Tooltip("Start of Elasticity multiplier")]
		public float SOSEM = 0.85f;

		[Min(0f)]
		[Tooltip("EOSReturnTorque modifier (Torque on depress is original Torque * EOSReturn / Flipper Return Strength)")]
		public float EOSReturn = 0.055f;

		[Min(0f)]
		[Tooltip("End of stroke Torque")]
		public float EOSTNew = 0.8f;

		[Min(0f)]
		[Tooltip("End of stroke Torque Angle")]
		public float EOSANew = 1.0f;

		[Min(0f)]
		[Tooltip("End of stroke RampUp")]
		public float EOSRampup = 0.0f;

		[Min(0f)]
		[Tooltip("Degrees of Overshoot above End Angle")]
		public float Overshoot = 3.0f;

		[Min(0f)]
		[Tooltip("Bump Ball vertically on release button (speed, up)")]
		public float BumpOnRelease = 0.4f;
		#endregion

		#region LiveCatch
		/// <summary>
		/// If set, apply Live Catch (nFozzy/RothBauerW)
		/// </summary>

		[Tooltip("The nFozzy's LiveCatch Physics")]
		public bool useFlipperLiveCatch = false;

		[Min(0f)]
		[Tooltip("Minimum distance in vp units from flipper base live catch dampening will occur")]
		public float LiveCatchDistanceMin = 40f;

		[Min(0f)]
		[Tooltip("Maxium distance in vp units from flipper base live catch dampening will occur")]
		public float LiveCatchDistanceMax = 100f;

		[Min(0f)]
		[Tooltip("Minimal ball speed for live catch")]
		public float LiveCatchMinimalBallSpeed = 6f;

		[Unit("ms")]
		[Min(0f)]
		[Tooltip("Maximum Time in for (perfect or imperfect) live catch")]
		public float LiveCatchFullTime = 16;

		[Unit("ms")]
		[Min(0f)]
		[Tooltip("Maximum Time for a perfect live catch")]
		public float LiveCatchPerfectTime = 8;

		[Min(0f)]
		[Tooltip("Minimum bounce speed multiplier for a live catch (0 allows perfect live catches)")]
		public float LiveCatchMinmalBounceSpeedMultiplier = 0.1f;

		[Min(0f)]
		[Tooltip("Maximum bounce speed multiplier for an inaccurate live catch")]
		public float LiveCatchInaccurateBounceSpeedMultiplier = 1.0f;

		#endregion

		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> MainComponent.FlipperApi ?? new FlipperApi(gameObject, player, physicsEngine);

		public override float4x4 TranslateWithinPlayfieldMatrix(float4x4 worldToPlayfield)
			=> MainComponent.LocalToWorldPhysicsMatrix.LocalToWorldTranslateWithinPlayfield(worldToPlayfield);
	}
}
