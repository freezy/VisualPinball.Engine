using NLog;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	public class BallContactSystem : JobComponentSystem
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private float3 _gravity;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
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

		public void SetGravity(float slopeDeg, float strength)
		{
			_gravity.x = 0;
			_gravity.y = math.sin(math.radians(slopeDeg)) * strength;
			_gravity.z = -math.cos(math.radians(slopeDeg)) * strength;
			Logger.Info("Gravity set to {0}", _gravity);
		}
	}
}
