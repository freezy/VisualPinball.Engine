// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateVelocitiesSystemGroup))]
	public class BallVelocitySystem : SystemBase
	{
		private float3 _gravity;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("BallVelocitySystem");

		protected override void OnStartRunning()
		{
			_gravity = Object.FindObjectOfType<Player>().GetGravity();
		}

		protected override void OnUpdate()
		{
			var gravity = _gravity;
			var marker = PerfMarker;
			Entities.WithName("BallVelocityJob").ForEach((ref BallData ball) => {

				if (ball.IsFrozen) {
					return;
				}

				marker.Begin();

				ball.Velocity += gravity * PhysicsConstants.PhysFactor;

				marker.End();

			}).Run();
		}
	}
}
