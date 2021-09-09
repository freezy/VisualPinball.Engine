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

using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace VisualPinball.Unity
{
	[AlwaysSynchronizeSystem]
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	internal class BallMovementSystem : SystemBase
	{
		private float4x4 _baseTransform;
		private Player _player;

		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("BallMovementSystem");

		protected override void OnStartRunning()
		{
			var root = Object.FindObjectOfType<PlayfieldComponent>();
			var ltw = root.gameObject.transform.localToWorldMatrix;
			_baseTransform = new float4x4(
				ltw.m00, ltw.m01, ltw.m02, ltw.m03,
				ltw.m10, ltw.m11, ltw.m12, ltw.m13,
				ltw.m20, ltw.m21, ltw.m22, ltw.m23,
				ltw.m30, ltw.m31, ltw.m32, ltw.m33
			);
			_player = Object.FindObjectOfType<Player>();
		}

		protected override void OnUpdate()
		{
			var ltw = _baseTransform;
			var marker = PerfMarker;
			Entities.WithoutBurst().WithName("BallMovementJob").ForEach((Entity entity, in BallData ball) => {

				marker.Begin();

				if (!_player.Balls.ContainsKey(entity)) {
					marker.End();
					return;
				}

				// calculate/adapt height of ball
				var zHeight = !ball.IsFrozen ? ball.Position.z : ball.Position.z - ball.Radius;

				var or = ball.Orientation;
				var ballTransform = _player.Balls[entity].transform;
				ballTransform.localPosition = new Vector3(ball.Position.x, ball.Position.y, zHeight);
				ballTransform.localRotation = Quaternion.LookRotation(or.c2, or.c1);

				marker.End();

			}).Run();
		}
	}
}
