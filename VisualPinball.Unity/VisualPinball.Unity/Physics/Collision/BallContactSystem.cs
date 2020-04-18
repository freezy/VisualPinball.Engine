using NLog;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;
using Logger = NLog.Logger;
using Random = Unity.Mathematics.Random;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class BallContactSystem : JobComponentSystem
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private float3 _gravity;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override void OnStartRunning()
		{
			_gravity = Object.FindObjectOfType<Player>().GetGravity();
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var rnd = new Random(666);
			var hitTime = _simulateCycleSystemGroup.DTime;
			var gravity = _gravity;

			return Entities.ForEach((ref BallData ballData, ref DynamicBuffer<ContactBufferElement> contacts) => {

				if (rnd.NextBool()) { // swap order of contact handling randomly
					// tslint:disable-next-line:prefer-for-of
					for (var i = 0; i < contacts.Length; i++) {
						var contact = contacts[i];
						contact.Collider.Contact(ref ballData, in contact.CollisionEvent, hitTime, gravity);
					}
				} else {
					for (var i = contacts.Length - 1; i != -1; --i) {
						var contact = contacts[i];
						contact.Collider.Contact(ref ballData, in contact.CollisionEvent, hitTime, gravity);
					}
				}

			}).Schedule(inputDeps);
		}
	}
}
