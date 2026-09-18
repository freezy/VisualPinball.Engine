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

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity
{
	/// <summary>
	/// One deflection of a compliant free span, as the physics thread reported it.
	/// </summary>
	public class RubberSpanImpactEventArgs : EventArgs
	{
		/// <summary>Dense runtime id of the span, see <see cref="PhysicsEngine.TryGetRubberSpan"/>.</summary>
		public readonly int SpanId;

		public readonly int BallId;

		/// <summary>Simulation time of the impact, in milliseconds since the start.</summary>
		public readonly float SimulationTimeMs;

		/// <summary>Span coordinate of the deepest point, <c>u_h</c>.</summary>
		public readonly float ApexFraction;

		/// <summary>Blended depth <c>delta_eff</c>, in VPX units.</summary>
		public readonly float EffectiveDepth;

		public RubberSpanImpactEventArgs(int spanId, int ballId, float simulationTimeMs,
			float apexFraction, float effectiveDepth)
		{
			SpanId = spanId;
			BallId = ballId;
			SimulationTimeMs = simulationTimeMs;
			ApexFraction = apexFraction;
			EffectiveDepth = effectiveDepth;
		}
	}

	public class RubberApi : CollidableApi<RubberComponent, RubberColliderComponent, RubberData>,
		IApi, IApiHittable, IApiRubberSpanImpact
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the rubber.
		/// </summary>
		public event EventHandler<HitEventArgs> Hit;

		/// <summary>
		/// Event emitted when one of this rubber's compliant free spans was deflected.
		/// </summary>
		public event EventHandler<RubberSpanImpactEventArgs> SpanImpact;

		internal RubberApi(GameObject go, Player player, PhysicsEngine physicsEngine) : base(go, player, physicsEngine)
		{
		}

		#region Collider Generation

		protected override bool FireHitEvents => ColliderComponent.HitEvent;
		protected override float HitThreshold => 2.0f; // hard coded threshold for now

		protected override void CreateColliders(ref ColliderReference colliders, float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			if (ColliderComponent.Mode == RubberColliderMode.Physical) {
				if (!ColliderComponent.CanUsePhysical) {
					Debug.LogWarning($"Physical rubber '{MainComponent.name}' has no usable serialized guided path; falling back to Legacy collision.",
						MainComponent);
				} else {
					// The collector is only set while the physics engine creates its
					// colliders; the editor's collider preview leaves it null, so this
					// path stays free of side effects there.
					var physicalGenerator = new RubberPhysicalColliderGenerator(this, MainComponent,
						translateWithinPlayfieldMatrix,
						ColliderComponent.GuideDistanceElasticitySettings,
						ColliderComponent,
						PhysicsEngine ? PhysicsEngine.RubberSpanCollector : null);
					physicalGenerator.GenerateColliders(ColliderComponent.ZOffset, ref colliders, margin);
					return;
				}
			}

			var colliderGenerator = new RubberColliderGenerator(
				this,
				new RubberMeshGenerator(MainComponent),
				translateWithinPlayfieldMatrix
			);
			colliderGenerator.GenerateColliders(ColliderComponent.ZOffset, MainComponent.PlayfieldDetailLevel, ref colliders, margin);
		}

		#endregion

		#region Events

		void IApi.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
		}

		void IApiHittable.OnHit(int ballId, bool _)
		{
			Hit?.Invoke(this, new HitEventArgs(ballId));
		}

		void IApiRubberSpanImpact.OnSpanImpact(int spanId, int ballId, float simulationTimeMs,
			float apexFraction, float effectiveDepth)
		{
			SpanImpact?.Invoke(this, new RubberSpanImpactEventArgs(spanId, ballId,
				simulationTimeMs, apexFraction, effectiveDepth));
		}

		#endregion
	}
}
