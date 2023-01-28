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

namespace VisualPinball.Unity
{
	public class PhysicsEngine : MonoBehaviour
	{
		[NonSerialized] 
		private NativeArray<ulong> _uSecs;
		
		private static ulong NowUsec => (ulong)(Time.timeAsDouble * 1000000);
		
		private void Start()
		{
			_uSecs = new NativeArray<ulong>(3, Allocator.Persistent);
			_uSecs[0] = NowUsec;                                      // start time
			_uSecs[1] = _uSecs[0];                                    // current time frame
			_uSecs[2] = _uSecs[0] + PhysicsConstants.PhysicsStepTime; // next time frame
		}

		private void Update()
		{
			
			var updatePhysics = new UpdatePhysicsJob {
				InitialTimeUsec = NowUsec,
				USecs = _uSecs,
			};
			
			updatePhysics.Run();
		}
		
		private void OnDestroy()
		{
			_uSecs.Dispose();
		}
	}

	[BurstCompile]
	public struct UpdatePhysicsJob : IJob
	{
		[ReadOnly] 
		public ulong InitialTimeUsec;

		public NativeArray<ulong> USecs;
		
		public void Execute()
		{
			var n = 0;
			var startTimeUsec = USecs[0];
			var curPhysicsFrameTime = USecs[1];
			var nextPhysicsFrameTime = USecs[2];
			
			while (curPhysicsFrameTime < InitialTimeUsec)
			{
				var timeMsec = (uint)((curPhysicsFrameTime - startTimeUsec) / 1000);
				var physicsDiffTime = (float)((nextPhysicsFrameTime - curPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime));

				PhysicsCycle.Simulate(physicsDiffTime);
				
				curPhysicsFrameTime = nextPhysicsFrameTime;
				nextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;

				n++;
			}

			USecs[1] = curPhysicsFrameTime;
			USecs[2] = nextPhysicsFrameTime;

			//Debug.Log($"UpdatePhysic {n}x");
		}
	}
}
