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
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using Random = Unity.Mathematics.Random;

namespace VisualPinball.Unity
{
	public struct PhysicsEnv
	{
		public readonly float3 Gravity;
		public readonly ulong StartTimeUsec;
		public ulong CurPhysicsFrameTime;
		public ulong NextPhysicsFrameTime;
		public uint TimeMsec;

		public Random Random;

		public PhysicsEnv(ulong startTimeUsec, PlayfieldComponent playfield, float gravityStrength) : this()
		{
			StartTimeUsec = startTimeUsec;
			CurPhysicsFrameTime = StartTimeUsec;
			NextPhysicsFrameTime = StartTimeUsec + PhysicsConstants.PhysicsStepTime;
			Random = new Random((uint)UnityEngine.Random.Range(1, 100000));
			Gravity = playfield.PlayfieldGravity(gravityStrength);
		}
	}
}
