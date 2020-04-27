using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class BallDynamicBroadPhaseSystem : JobComponentSystem
	{
		private EntityQuery _ballQuery;

		[BurstCompile]
		private struct BallDynamicBroadPhaseJob : IJob {

			[DeallocateOnJobCompletion]
			public NativeArray<ArchetypeChunk> Chunks;

			[ReadOnly]
			public ArchetypeChunkComponentType<BallData> BallType;
			public ArchetypeChunkBufferType<MatchedBallColliderBufferElement> MatchedBallColliderType;
			[ReadOnly]
			public ArchetypeChunkEntityType EntityChunkType;

			public void Execute()
			{
				var ballBounds = new NativeList<Aabb>(Allocator.Temp);
				for (var j = 0; j < Chunks.Length; j++) {
					var balls = Chunks[j].GetNativeArray(BallType);
					var entities = Chunks[j].GetNativeArray(EntityChunkType);

					//Debug.Log($"We have {balls.Length} ball(s) and ({Chunks.Length} chunk(s)!");

					for (var i = 0; i < Chunks[j].Count; i++) {
						ballBounds.Add(balls[i].GetAabb(entities[i]));
					}
				}

				var kdRoot = new KdRoot(ballBounds.ToArray()); // todo fix, copies data
				for (var j = 0; j < Chunks.Length; j++) {

					var entities = Chunks[j].GetNativeArray(EntityChunkType);
					var balls = Chunks[j].GetNativeArray(BallType);
					var matchedColliderIdBuffers = Chunks[j].GetBufferAccessor(MatchedBallColliderType);

					//Debug.Log($"We have {balls.Length} ball(s) and ({Chunks.Length} chunk(s)!");

					for (var i = 0; i < Chunks[j].Count; i++) {
						var matchedColliderIdBuffer = matchedColliderIdBuffers[i];
						kdRoot.GetAabbOverlaps(entities[i], balls[i], ref matchedColliderIdBuffer);
					}
				}
			}
		}

		protected override void OnCreate() {
			_ballQuery = GetEntityQuery(typeof(BallData));
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			return new BallDynamicBroadPhaseJob {
				Chunks = _ballQuery.CreateArchetypeChunkArray(Allocator.TempJob),
				BallType = GetArchetypeChunkComponentType<BallData>(true),
				MatchedBallColliderType = GetArchetypeChunkBufferType<MatchedBallColliderBufferElement>(),
				EntityChunkType = GetArchetypeChunkEntityType()
			}.Schedule(inputDeps);
		}
	}
}
