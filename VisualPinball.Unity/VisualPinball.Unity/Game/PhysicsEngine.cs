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
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.VisualPinball.Unity.Game;

namespace VisualPinball.Unity
{
	public class PhysicsEngine : MonoBehaviour
	{
		[NonSerialized] 
		private NativeArray<PhysicsState> _physicsState;
		
		private static ulong NowUsec => (ulong)(Time.timeAsDouble * 1000000);
		
		private void Start()
		{
			_physicsState = new NativeArray<PhysicsState>(1, Allocator.Persistent);
			_physicsState[0] = new PhysicsState(NowUsec);
		}

		private void Update()
		{
			var updatePhysics = new UpdatePhysicsJob {
				InitialTimeUsec = NowUsec,
				PhysicsState = _physicsState,
			};
			
			updatePhysics.Run();
		}
		
		private void OnDestroy()
		{
			_physicsState.Dispose();
		}
	}

	[BurstCompile]
	public struct UpdatePhysicsJob : IJob
	{
		[ReadOnly] 
		public ulong InitialTimeUsec;

		public NativeArray<PhysicsState> PhysicsState;
		
		public void Execute()
		{
			var n = 0;
			var state = PhysicsState[0];
			
			while (state.CurPhysicsFrameTime < InitialTimeUsec)
			{
				var timeMsec = (uint)((state.CurPhysicsFrameTime - state.StartTimeUsec) / 1000);
				var physicsDiffTime = (float)((state.NextPhysicsFrameTime - state.CurPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime));

				PhysicsCycle.Simulate(physicsDiffTime, ref state);
				
				state.CurPhysicsFrameTime = state.NextPhysicsFrameTime;
				state.NextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;

				n++;
			}

			PhysicsState[0] = state;

			//Debug.Log($"UpdatePhysic {n}x");
		}
	}
}
