using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

namespace VisualPinball.Unity
{
	[AlwaysSynchronizeSystem]
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	public class BallMovementSystem : SystemBase
	{
		private float4x4 _baseTransform;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("BallMovementSystem");

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
			Entities.WithName("BallMovementJob").ForEach((ref Translation translation, ref Rotation rot, in BallData ball) => {

				marker.Begin();

				// calculate/adapt height of ball
				var zHeight = !ball.IsFrozen ? ball.Position.z : ball.Position.z - ball.Radius;

				translation.Value = math.transform(ltw, new float3(ball.Position.x, ball.Position.y, zHeight));
				var or = ball.Orientation;
				rot.Value = quaternion.LookRotation(or.c2,  or.c1);

				marker.End();

			}).Run();
		}
	}
}
