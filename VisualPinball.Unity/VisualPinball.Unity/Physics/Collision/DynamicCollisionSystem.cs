using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	public class DynamicCollisionSystem : SystemBase
	{
		[BurstCompile]
		private struct DynamicCollisionJob : IJobParallelFor
		{
			[DeallocateOnJobCompletion]
			public NativeArray<ArchetypeChunk> Chunks;

			public ArchetypeChunkComponentType<BallData> BallDataType;
			public ArchetypeChunkBufferType<MatchedBallColliderBufferElement> MatchedBallBuffer;
			[ReadOnly] public ArchetypeChunkComponentType<CollisionEventData> CollisionEventDataType;
			[ReadOnly] public ArchetypeChunkEntityType EntityType;

			public float HitTime;
			public bool SwapBallCollisionHandling;

			public void Execute(int chunkIndex)
			{
				var chunk = Chunks[chunkIndex];
				var chunkBallData = chunk.GetNativeArray(BallDataType);
				var chunkMatchedBalls = chunk.GetBufferAccessor(MatchedBallBuffer);
				var chunkCollEventData = chunk.GetNativeArray(CollisionEventDataType);
				var chunkEntities = chunk.GetNativeArray(EntityType);
				var instanceCount = chunk.Count;

				// index data for faster access below
				var balls = new NativeHashMap<Entity, BallData>(instanceCount, Allocator.Temp);
				var collisionEvents = new NativeHashMap<Entity, CollisionEventData>(instanceCount, Allocator.Temp);
				for (var i = 0; i < instanceCount; i++) {
					balls.Add(chunkEntities[i], chunkBallData[i]);
					collisionEvents.Add(chunkEntities[i], chunkCollEventData[i]);
				}

				// collide balls
				for (var i = 0; i < instanceCount; i++) {
					if (chunkMatchedBalls[i].Length == 0) {
						continue;
					}

					if (chunkMatchedBalls[i].Length > 1) {
						throw new InvalidOperationException($"Found {chunkMatchedBalls[i].Length} ball collisions but expected 1 (or 0).");
					}

					// pick colling ball
					var collidingEntity = chunkMatchedBalls[i][0].Value;
					var collidingBall = balls[collidingEntity];
					var collidingCollEvent = collisionEvents[collidingEntity];

					// pick ball
					var ballData = chunkBallData[i];
					var collEvent = chunkCollEventData[i];

					// find balls with hit objects and minimum time
					if (collEvent.HitTime <= HitTime) {
						// now collision, contact and script reactions on active ball (object)+++++++++

						//this.activeBall = ball;                         // For script that wants the ball doing the collision

						BallCollider.Collide(ref ballData, ref collidingBall, in collEvent, in collidingCollEvent,
							SwapBallCollisionHandling);
					}

					chunkMatchedBalls[i].Clear();
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
			var rotationsSpeedJob = new DynamicCollisionJob {
				Chunks = _query.CreateArchetypeChunkArray(Allocator.TempJob),
				BallDataType = GetArchetypeChunkComponentType<BallData>(),
				MatchedBallBuffer = GetArchetypeChunkBufferType<MatchedBallColliderBufferElement>(),
				CollisionEventDataType = GetArchetypeChunkComponentType<CollisionEventData>(true),
				EntityType = GetArchetypeChunkEntityType(),
				HitTime = _simulateCycleSystemGroup.HitTime,
				SwapBallCollisionHandling = _simulateCycleSystemGroup.SwapBallCollisionHandling
			};
			Dependency = rotationsSpeedJob.Schedule(rotationsSpeedJob.Chunks.Length,32, Dependency);
		}
	}
}
