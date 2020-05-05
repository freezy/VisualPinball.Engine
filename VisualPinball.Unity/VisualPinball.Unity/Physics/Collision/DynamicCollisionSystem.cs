using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Profiling;
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

				Profiler.BeginSample("DynamicCollisionSystem");

				// index data for faster access below
				var numEntities = Chunks.Length * Chunks[0].Count;
				var chunkIndices = new NativeHashMap<Entity, int>(numEntities, Allocator.Temp);
				var positionIndices = new NativeHashMap<Entity, int>(numEntities, Allocator.Temp);

				for (var i = 0; i < Chunks.Length; i++) {
					var chunkEntities = Chunks[i].GetNativeArray(EntityType);
					var chunkCount = Chunks[i].Count;

					for (var j = 0; j < chunkCount; j++) {
						chunkIndices.Add(chunkEntities[j], i);
						positionIndices.Add(chunkEntities[j], j);
					}
				}

				// collide balls
				for (var j = 0; j < Chunks.Length; j++) {

					var chunkBallData = Chunks[j].GetNativeArray(BallDataType);
					var chunkCollEventData = Chunks[j].GetNativeArray(CollisionEventDataType);
					var chunkCount = Chunks[j].Count;

					for (var i = 0; i < chunkCount; i++) {

						// pick "current" ball
						var ball = chunkBallData[i];
						var collEvent = chunkCollEventData[i];

						// pick "other" ball
						ref var otherEntity = ref collEvent.ColliderEntity;

						// find balls with hit objects and minimum time
						if (otherEntity != Entity.Null && collEvent.HitTime <= HitTime) {

							var chunkOtherBallData = Chunks[chunkIndices[otherEntity]].GetNativeArray(BallDataType);
							var chunkOtherCollEventData = Chunks[chunkIndices[otherEntity]].GetNativeArray(CollisionEventDataType);

							var otherBall = chunkOtherBallData[positionIndices[otherEntity]];
							var otherCollEvent = chunkOtherCollEventData[positionIndices[otherEntity]];

							// now collision, contact and script reactions on active ball (object)+++++++++

							//this.activeBall = ball;                         // For script that wants the ball doing the collision

							if (BallCollider.Collide(
								ref otherBall, ref ball,
								in collEvent, in otherCollEvent,
								SwapBallCollisionHandling
							)) {
								chunkBallData[i] = ball;
								chunkOtherBallData[positionIndices[otherEntity]] = otherBall;
							}

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

				Profiler.EndSample();
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
