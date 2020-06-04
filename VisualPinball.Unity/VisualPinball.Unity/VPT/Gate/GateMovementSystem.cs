using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Gate;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.VPT.Gate
{
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	public class GateMovementSystem : SystemBase
	{
		private float4x4 _baseTransform;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("GateMovementSystem");

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
			Entities.WithName("GateMovementJob").ForEach((ref Rotation rot, in GateStaticData data, in GateMovementData movementData) => {

				marker.Begin();

				var rotationX = movementData.Angle - math.radians(data.AngleMin);
				rot.Value = quaternion.RotateX(rotationX);

				marker.End();

			}).Run();
		}
	}
}
