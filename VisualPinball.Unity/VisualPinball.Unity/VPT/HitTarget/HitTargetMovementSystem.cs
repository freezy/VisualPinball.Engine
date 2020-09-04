// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using Unity.Transforms;
using UnityEngine;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	internal class HitTargetMovementSystem : SystemBase
	{
		private float4x4 _baseTransform;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("HitTargetMovementSystem");

		protected override void OnStartRunning()
		{
			var root = Object.FindObjectOfType<TableAuthoring>();
			var ltw = root.gameObject.transform.localToWorldMatrix;
			_baseTransform = new float4x4(
				ltw.m00, ltw.m01, ltw.m02, ltw.m03,
				ltw.m10, ltw.m11, ltw.m12, ltw.m13,
				ltw.m20, ltw.m21, ltw.m22, ltw.m23,
				ltw.m30, ltw.m31, ltw.m32, ltw.m33
			);
		}

		protected override void OnUpdate()
		{
			var ltw = _baseTransform;
			var marker = PerfMarker;
			Entities.WithName("HitTargetMovementJob").ForEach((ref Translation translation, ref Rotation rotation,
				in HitTargetMovementData data, in HitTargetStaticData staticData) =>
			{

				marker.Begin();

				var t = math.transform(math.inverse(ltw), translation.Value);
				translation.Value = math.transform(ltw, new float3(t.x, t.y, data.ZOffset));

				var localRot = quaternion.EulerXYZ(math.radians(data.XRotation), math.radians(staticData.RotZ), 0);
				var m = math.mul(float4x4.TRS(float3.zero, localRot,  new float3(1f, 1f, 1f)), ltw);
				rotation.Value = quaternion.LookRotationSafe(
					m.c2.xyz,
					m.c1.xyz
				);

				marker.End();

			}).Run();
		}

	}
}
