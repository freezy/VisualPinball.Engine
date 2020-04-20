// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Ball
{
	[UpdateInGroup(typeof(UpdateVelocitiesSystemGroup))]
	public class BallVelocitySystem : JobComponentSystem
	{
		private float3 _gravity;

		protected override void OnStartRunning()
		{
			_gravity = Object.FindObjectOfType<Player>().GetGravity();
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var gravity = _gravity;
			return Entities.WithoutBurst().ForEach((ref BallData ball) => {

				if (ball.IsFrozen) {
					return;
				}
				ball.Velocity += gravity * PhysicsConstants.PhysFactor;

			}).Schedule(inputDeps);
		}
	}
}
