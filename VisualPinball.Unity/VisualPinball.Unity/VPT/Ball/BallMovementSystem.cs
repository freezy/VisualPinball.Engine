using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.VPT.Ball
{
	[AlwaysSynchronizeSystem]
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	public class BallMovementSystem : SystemBase
	{
		private float4x4 _baseTransform;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("BallMovementSystem");

		protected override void OnStartRunning()
		{
			var root = Object.FindObjectOfType<TableBehavior>();
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

				translation.Value = math.transform(ltw, ball.Position);
				var or = ball.Orientation;
				rot.Value = quaternion.LookRotation(or.c2,  or.c1);

				marker.End();

			}).Run();
		}
	}
}
