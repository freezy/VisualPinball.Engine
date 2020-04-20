using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.VPT.Ball
{
	[AlwaysSynchronizeSystem]
	[UpdateInGroup(typeof(TransformMeshesSystemGroup))]
	public class BallMovementSystem : JobComponentSystem
	{
		private float4x4 _baseTransform;

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

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var ltw = _baseTransform;
			Entities.WithoutBurst().ForEach((ref Translation translation, in BallData ball) => {
				translation.Value = math.transform(ltw, ball.Position);
			}).Run();

			return default;
		}
	}
}
