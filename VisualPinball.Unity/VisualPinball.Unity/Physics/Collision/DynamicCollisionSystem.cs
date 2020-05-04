using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class DynamicCollisionSystem : SystemBase
	{
		[BurstCompile]
		private struct DynamicCollisionJob : IJob
		{
			[DeallocateOnJobCompletion]
			public NativeArray<ArchetypeChunk> Chunks;

			public ArchetypeChunkComponentType<BallData> BallDataType;
			[ReadOnly] public ArchetypeChunkComponentType<CollisionEventData> CollisionEventDataType;
			[ReadOnly] public ArchetypeChunkEntityType EntityType;

			public float HitTime;
			public bool SwapBallCollisionHandling;

			public void Execute()
			{
				if (Chunks.Length == 0) {
					return;
				}

				// index data for faster access below
				var numEntities = Chunks.Length * Chunks[0].Count;
				var indices = new NativeHashMap<Entity, int>(numEntities, Allocator.Temp);

				for (var j = 0; j < Chunks.Length; j++) {
					var chunkEntities = Chunks[j].GetNativeArray(EntityType);
					var chunkCount = Chunks[j].Count;

					for (var i = 0; i < chunkCount; i++) {
						indices.Add(chunkEntities[i], i);
					}
				}

				// collide balls
				for (var j = 0; j < Chunks.Length; j++) {

					var chunkBallData = Chunks[j].GetNativeArray(BallDataType);
					var chunkCollEventData = Chunks[j].GetNativeArray(CollisionEventDataType);
					var chunkCount = Chunks[j].Count;

					for (var i = 0; i < chunkCount; i++) {

						// pick "current" ball
						var ballData = chunkBallData[i];
						var collEvent = chunkCollEventData[i];

						// pick "other" ball
						ref var otherEntity = ref collEvent.ColliderEntity;

						// find balls with hit objects and minimum time
						if (otherEntity != Entity.Null && collEvent.HitTime <= HitTime) {

							var otherBall = chunkBallData[indices[otherEntity]];
							var otherCollEvent = chunkCollEventData[indices[otherEntity]];

							// now collision, contact and script reactions on active ball (object)+++++++++

							//this.activeBall = ball;                         // For script that wants the ball doing the collision

							BallCollider.Collide(
								ref otherBall, ref ballData,
								in collEvent, in otherCollEvent,
								SwapBallCollisionHandling
							);

							// remove trial hit object pointer
							collEvent.ClearCollider();

							// todo fix below (probably just delete)
							// Collide may have changed the velocity of the ball,
							// and therefore the bounding box for the next hit cycle
							// if (this.balls[i] !== ball) { // Ball still exists? may have been deleted from list
							//
							// 	// collision script deleted the ball, back up one count
							// 	--i;
							//
							// } else {
							// 	ball.hit.calcHitBBox(); // do new boundings
							// }
						}
					}
				}
			}
		}

		private EntityQuery _query;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;

		protected override void OnCreate()
		{
			var queryDesc = new EntityQueryDesc {
				All = new[]{ typeof(BallData), ComponentType.ReadOnly<CollisionEventData>() }
			};

			_query = GetEntityQuery(queryDesc);
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override void OnUpdate()
		{
			new DynamicCollisionJob {
				Chunks = _query.CreateArchetypeChunkArray(Allocator.TempJob),
				BallDataType = GetArchetypeChunkComponentType<BallData>(),
				CollisionEventDataType = GetArchetypeChunkComponentType<CollisionEventData>(true),
				EntityType = GetArchetypeChunkEntityType(),
				HitTime = _simulateCycleSystemGroup.HitTime,
				SwapBallCollisionHandling = _simulateCycleSystemGroup.SwapBallCollisionHandling
			}.Run();
		}
	}
}
