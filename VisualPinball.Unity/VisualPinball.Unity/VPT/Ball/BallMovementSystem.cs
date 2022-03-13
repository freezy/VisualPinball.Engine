// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

				// Following is the transistion from VP-Physics Ball Orientation to the Unity Ball-Orientation.
				// following statements: when looking at the backglass:
				// The problem here is, that we have 
				//    a right handed universe in VP (X->R, Y->F, Z->U) and
				//    a left handed universe in Unity (X->R, Y->U, Z->F) 
				// The other problem is, that Unity likes quaternions and VP uses Orientation matrices.
				// I THINK!!!:
				//    where x via C0 to C2 describes the right vector,
				//    and y the front vector and z the top vector

				// So we have to transform between these. Not only that the Column-wise Vectors is hard to understand,
				// but also the transition from one universe to another is hard. 

				//very old transformation (looks strange)  (freezy)
				//ballTransform.localRotation = Quaternion.LookRotation(or.c2, or.c1);
				//1st iteration by (looks strange, but less strange)  (cupiii)
				//ballTransform.localRotation = Quaternion.LookRotation(new Vector3(or.c0.x*-1, or.c1.x*-1, or.c2.x), new Vector3(or.c0.z*-1, or.c1.z*-1, or.c2.z));
				//newest iteration (hopefully correct))
				ballTransform.localRotation = Quaternion.LookRotation(new Vector3(or.c0.z*1f, or.c2.z*1f, or.c1.z*1f), new Vector3(or.c0.y*1f, or.c2.y*1f, or.c1.y*1f));

				marker.End();

			}).Run();
		}
	}
}
