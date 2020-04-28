using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class DynamicBroadPhaseSystem : SystemBase
	{
		private EntityQuery _ballQuery;

		[BurstCompile]
		private struct DynamicBroadPhaseJob : IJob {

			[DeallocateOnJobCompletion]
			public NativeArray<ArchetypeChunk> Chunks;

			[ReadOnly]
			public ArchetypeChunkComponentType<BallData> BallType;
			[ReadOnly]
			public ArchetypeChunkEntityType EntityChunkType;
			public ArchetypeChunkBufferType<MatchedBallColliderBufferElement> MatchedBallColliderType;

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

					var balls = Chunks[j].GetNativeArray(BallType);
					var entities = Chunks[j].GetNativeArray(EntityChunkType);
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

		protected override void OnUpdate()
		{
			Dependency = new DynamicBroadPhaseJob {
				Chunks = _ballQuery.CreateArchetypeChunkArray(Allocator.TempJob),
				BallType = GetArchetypeChunkComponentType<BallData>(true),
				MatchedBallColliderType = GetArchetypeChunkBufferType<MatchedBallColliderBufferElement>(),
				EntityChunkType = GetArchetypeChunkEntityType()
			}.Schedule(Dependency);
		}
	}
}
