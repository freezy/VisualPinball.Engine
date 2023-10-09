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

using Unity.Entities;
using Unity.Profiling;
using VisualPinball.Unity;
using VisualPinball.Unity.VisualPinball.Unity.Game;

namespace VisualPinballUnity
{
	[DisableAutoCreation]
	internal partial class DynamicCollisionSystem : SystemBaseStub
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("DynamicCollisionSystem");
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystemManaged<SimulateCycleSystemGroup>();
		}

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			var hitTime = _simulateCycleSystemGroup.HitTime;
			var swapBallCollisionHandling = _simulateCycleSystemGroup.SwapBallCollisionHandling;
			var balls = GetComponentLookup<BallData>();
			var collEvents = GetComponentLookup<CollisionEventData>(true);

			// fixme job
			// Entities
			// 	.WithName("DynamicCollisionJob")
			// 	.WithNativeDisableParallelForRestriction(balls)
			// 	.WithReadOnly(collEvents)
			// 	.ForEach((ref BallData ball, ref CollisionEventData collEvent) => {
			//
			// 		
			// 		// pick "other" ball
			// 		ref var otherId = ref collEvent.BallId;
			// 		
			// 		// find balls with hit objects and minimum time
			// 		if (otherId != 0 && collEvent.HitTime <= hitTime) {
			// 		
			// 			marker.Begin();
			// 		
			// 			var otherBall = balls[otherId];
			// 			var otherCollEvent = collEvents[otherId];
			// 		
			// 			// now collision, contact and script reactions on active ball (object)+++++++++
			// 		
			// 			//this.activeBall = ball;                         // For script that wants the ball doing the collision
			// 		
			// 			if (BallCollider.Collide(ref otherBall, ref ball,in otherCollEvent, in collEvent, swapBallCollisionHandling)) {
			// 				balls[otherId] = otherBall;
			// 			}
			// 		
			// 			// remove trial hit object pointer
			// 			collEvent.ClearCollider();
			// 		
			// 			marker.End();
			// 		}
			// 	}).Run();
		}
	}
}