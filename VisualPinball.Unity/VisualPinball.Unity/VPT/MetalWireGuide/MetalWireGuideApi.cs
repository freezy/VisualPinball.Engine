﻿// Visual Pinball Engine
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
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT.MetalWireGuide;

namespace VisualPinball.Unity
{
	public class MetalWireGuideApi : CollidableApi<MetalWireGuideComponent, MetalWireGuideColliderComponent, MetalWireGuideData>,
		IApi, IApiHittable
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		/// <summary>
		/// Event emitted when the ball hits the metal wire gudie.
		/// </summary>
		public event EventHandler<HitEventArgs> Hit;

		internal MetalWireGuideApi(GameObject go, Player player, PhysicsEngine physicsEngine) : base(go, player, physicsEngine)
		{
		}

		#region Collider Generation

		protected override bool FireHitEvents => ColliderComponent.HitEvent;
		protected override float HitThreshold => 2.0f; // hard coded threshold for now

		protected override void CreateColliders(ref ColliderReference colliders, float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			var colliderGenerator = new MetalWireGuideColliderGenerator(this, new MetalWireGuideMeshGenerator(MainComponent), translateWithinPlayfieldMatrix);
			colliderGenerator.GenerateColliders(0, ColliderComponent.HitHeight, MainComponent.Bendradius, MainComponent.PlayfieldDetailLevel, ref colliders, margin);
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

		#endregion
	}
}
