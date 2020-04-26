using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{

	[DisableAutoCreation]
//[AlwaysSynchronizeSystem]
	public class BallDynamicBroadPhaseSystem : JobComponentSystem
	{
		public EntityQuery BallQuery;

		[BurstCompile]
		private struct BallDynamicBroadPhaseJob : IJob {

			[DeallocateOnJobCompletion]
			public NativeArray<ArchetypeChunk> Chunks;

			[ReadOnly]
			public ArchetypeChunkComponentType<BallData> BallType;
			public ArchetypeChunkBufferType<MatchedBallColliderBufferElement> MatchedBallColliderType;

			public void Execute()
			{
				var ballBounds = new NativeList<Aabb>(Allocator.Temp);
				for (var j = 0; j < Chunks.Length; j++) {
					var balls = Chunks[j].GetNativeArray(BallType);
					//Debug.Log($"We have {balls.Length} ball(s) and ({Chunks.Length} chunk(s)!");

					for (var i = 0; i < Chunks[j].Count; i++) {
						ballBounds.Add(balls[i].Aabb);
					}
				}

				var kdRoot = new KdRoot(ballBounds.ToArray());

				for (var j = 0; j < Chunks.Length; j++) {

					var balls = Chunks[j].GetNativeArray(BallType);
					var matchedColliderIdBuffers = Chunks[j].GetBufferAccessor(MatchedBallColliderType);

					//Debug.Log($"We have {balls.Length} ball(s) and ({Chunks.Length} chunk(s)!");

					for (var i = 0; i < Chunks[j].Count; i++) {
						var matchedColliderIdBuffer = matchedColliderIdBuffers[i];
						kdRoot.GetAabbOverlaps(balls[i], ref matchedColliderIdBuffer);
					}
				}
			}
		}

		protected override void OnCreate() {
			BallQuery = GetEntityQuery(typeof(BallData));
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			return new BallDynamicBroadPhaseJob {
				Chunks = BallQuery.CreateArchetypeChunkArray(Allocator.TempJob),
				BallType = GetArchetypeChunkComponentType<BallData>(true),
				MatchedBallColliderType = GetArchetypeChunkBufferType<MatchedBallColliderBufferElement>()
			}.Schedule(inputDeps);
		}
	}
}
