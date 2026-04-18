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
	[PackAs("FlipperCollider")]
	[AddComponentMenu("Pinball/Collision/Flipper Collider")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/flippers.html")]
	public class FlipperColliderComponent : ColliderComponent<FlipperData, FlipperComponent>, IPackable
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

		public int TriggerItemId;

		#endregion

		#region Packaging

		public byte[] Pack() => FlipperColliderPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> FlipperColliderReferencesPackable.PackReferences(this, files);

		public void Unpack(byte[] bytes) => FlipperColliderPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files)
			=> FlipperColliderReferencesPackable.Unpack(data, this, files);

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
			get => true;
			set { }
		}

		#endregion

		#region FlipperTricks
		/// <summary>
		/// If set, apply Flipper Tricks Physics (nFozzy/RothBauerW)
		/// </summary>

		[Tooltip("Enables VPW/RothbauerW flipper tricks: start-of-stroke, end-of-stroke, overshoot, and release-bump behavior.")]
		public bool useFlipperTricksPhysics = false;

		[Min(0f)]
		[Tooltip("Coil ramp-up while the flipper starts its stroke. Lower values make the initial flip force arrive faster.")]
		public float SOSRampUp = 2.5f;

		[Min(0f)]
		[Tooltip("Rubber elasticity multiplier during the start-of-stroke phase. Values below 1 soften early flip impacts.")]
		public float SOSEM = 0.85f;

		[Min(0f)]
		[Tooltip("Return torque multiplier used while the flipper is up and the button is released. Lower values make the flipper drop more softly from EOS.")]
		public float EOSReturn = 0.055f;

		[Min(0f)]
		[Tooltip("Torque applied once the flipper reaches end-of-stroke. This is the hold strength at EOS.")]
		public float EOSTNew = 0.8f;

		[Min(0f)]
		[Tooltip("Angle, in degrees from the end position, where EOS hold torque starts applying.")]
		public float EOSANew = 1.0f;

		[Min(0f)]
		[Tooltip("Ramp-up used for the EOS hold torque. Zero applies the EOS torque immediately.")]
		public float EOSRampup = 0.0f;

		[Min(0f)]
		[Tooltip("How many degrees the flipper may overshoot past its configured end angle before settling back.")]
		public float Overshoot = 3.0f;

		[Min(0f)]
		[Tooltip("Upward ball speed added when releasing the flipper button, used to emulate VPW release-bump behavior.")]
		public float BumpOnRelease = 0.4f;
		#endregion

		#region LiveCatch
		/// <summary>
		/// If set, apply Live Catch (nFozzy/RothBauerW)
		/// </summary>

		[Tooltip("Enables modern VPW live-catch behavior for balls arriving just after the flipper reaches end-of-stroke.")]
		public bool useFlipperLiveCatch = false;

		[Min(0f)]
		[Tooltip("Closest distance from the flipper base where live catch can apply, in VP units. Modern VPW scripts usually use 5.")]
		public float LiveCatchDistanceMin = 5f;

		[Min(0f)]
		[Tooltip("Farthest distance from the flipper base where live catch can apply, in VP units. Modern VPW scripts usually use 114.")]
		public float LiveCatchDistanceMax = 114f;

		[Min(0f)]
		[Tooltip("Minimum impact speed required for live catch processing. This maps to the modern VPW parm > 3 threshold.")]
		public float LiveCatchMinimalBallSpeed = 3f;

		[Unit("ms")]
		[Min(0f)]
		[Tooltip("Maximum time, in milliseconds after the flipper reaches EOS, where live catch can still apply.")]
		public float LiveCatchFullTime = 16;

		[Unit("ms")]
		[Min(0f)]
		[Tooltip("Time window, in milliseconds after EOS, where the catch is considered perfect and no late-catch bounce is added.")]
		public float LiveCatchPerfectTime = 8;

		[Min(0f)]
		[Tooltip("Bounce speed used for a perfect live catch. Modern VPW scripts use 0 so perfect catches can fully deaden the rebound.")]
		public float LiveCatchMinmalBounceSpeedMultiplier = 0f;

		[Min(0f)]
		[Tooltip("Late-catch bounce speed scale. Modern VPW scripts use 32 and scale it by how late the catch is within the full live-catch window.")]
		public float LiveCatchInaccurateBounceSpeedMultiplier = 32f;

		[Min(0f)]
		[Tooltip("Distance from the flipper base separating base dampening from normal live catching. Modern VPW scripts use 30 VP units.")]
		public float LiveCatchBaseDampenDistance = 30f;

		[Min(0f)]
		[Tooltip("Velocity and spin multiplier applied in the base dampen zone. Modern VPW scripts use 0.55.")]
		public float LiveCatchBaseDampen = 0.55f;

		#endregion

		protected override IApiColliderGenerator InstantiateColliderApi(Player player, PhysicsEngine physicsEngine)
			=> MainComponent.FlipperApi ?? new FlipperApi(gameObject, player, physicsEngine);

		public override float4x4 GetLocalToPlayfieldMatrixInVpx(float4x4 worldToPlayfield)
			=> MainComponent.LocalToWorldPhysicsMatrix.GetLocalToPlayfieldMatrixInVpx(worldToPlayfield);
	}
}
