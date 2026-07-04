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
	/// <summary>
	/// Per-table physics environment values that are independent of individual
	/// component state.
	/// </summary>
	/// <remarks>
	/// This struct travels with the physics state through both main-thread and
	/// simulation-thread execution. Nudge and plumb state live here because they
	/// affect every ball and must advance once per one-millisecond physics tick,
	/// not once per Unity frame.
	/// </remarks>
	public struct PhysicsEnv
	{
		public readonly float3 Gravity;
		public readonly ulong StartTimeUsec;
		public ulong CurPhysicsFrameTime;
		public ulong NextPhysicsFrameTime;
		public uint TimeMsec;

		public Random Random;
		public NudgeState Nudge;
		public PlumbState Plumb;

		/// <summary>
		/// Creates the physics environment for a table, including gravity, cabinet
		/// nudge state, and plumb-bob state.
		/// </summary>
		public PhysicsEnv(ulong startTimeUsec, PlayfieldComponent playfield, float gravityStrength,
			KeyboardNudgeMode keyboardNudgeMode = KeyboardNudgeMode.CabModel, float keyboardNudgeStrength = 1f,
			float nudgeTime = 5f, bool simulatedPlumb = true, float plumbDamping = 1f, float plumbThresholdAngle = 2f,
			float keyboardCabinetDamping = CabinetPhysicsState.DefaultKeyboardDampingRatio) : this()
		{
			StartTimeUsec = startTimeUsec;
			CurPhysicsFrameTime = StartTimeUsec;
			NextPhysicsFrameTime = StartTimeUsec + PhysicsConstants.PhysicsStepTime;
			Random = new Random((uint)UnityEngine.Random.Range(1, 100000));
			Gravity = playfield.PlayfieldGravity(gravityStrength);
			Nudge = new NudgeState(keyboardNudgeMode, keyboardNudgeStrength, nudgeTime, keyboardCabinetDamping);
			Plumb = new PlumbState(simulatedPlumb, plumbDamping, plumbThresholdAngle);
		}
	}
}
