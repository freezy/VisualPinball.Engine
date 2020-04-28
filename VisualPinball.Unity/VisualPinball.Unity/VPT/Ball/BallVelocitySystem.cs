// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Ball
{
	[UpdateInGroup(typeof(UpdateVelocitiesSystemGroup))]
	public class BallVelocitySystem : SystemBase
	{
		private float3 _gravity;

		protected override void OnStartRunning()
		{
			_gravity = Object.FindObjectOfType<Player>().GetGravity();
		}

		protected override void OnUpdate()
		{
			var gravity = _gravity;
			Entities.WithName("BallVelocityJob").ForEach((ref BallData ball) => {

				if (ball.IsFrozen) {
					return;
				}
				ball.Velocity += gravity * PhysicsConstants.PhysFactor;

			}).ScheduleParallel();
		}
	}
}
