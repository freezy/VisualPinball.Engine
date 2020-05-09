using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Profiling;
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
			public ArchetypeChunkBufferType<OverlappingDynamicBufferElement> OverlappingDynamicBufferType;

			public void Execute()
			{

				Profiler.BeginSample("DynamicBroadPhaseSystem");

				// get bounds for all balls
				var ballBounds = new NativeList<Aabb>(Allocator.Temp);
				for (var j = 0; j < Chunks.Length; j++) {
					var balls = Chunks[j].GetNativeArray(BallType);
					var entities = Chunks[j].GetNativeArray(EntityChunkType);

					//Debug.Log($"We have {balls.Length} ball(s) and ({Chunks.Length} chunk(s)!");

					for (var i = 0; i < Chunks[j].Count; i++) {
						ballBounds.Add(balls[i].GetAabb(entities[i]));
					}
				}

				// create kdtree
				var kdRoot = new KdRoot(ballBounds);

				// find aabb overlaps
				for (var j = 0; j < Chunks.Length; j++) {

					var balls = Chunks[j].GetNativeArray(BallType);
					var entities = Chunks[j].GetNativeArray(EntityChunkType);
					var overlappingBuffers = Chunks[j].GetBufferAccessor(OverlappingDynamicBufferType);

					//Debug.Log($"We have {balls.Length} ball(s) and ({Chunks.Length} chunk(s)!");

					for (var i = 0; i < Chunks[j].Count; i++) {
						var overlappingEntityBuffer = overlappingBuffers[i];
						overlappingEntityBuffer.Clear();
						kdRoot.GetAabbOverlaps(entities[i], balls[i], ref overlappingEntityBuffer);
					}
				}
				kdRoot.Dispose();
				ballBounds.Dispose();

				Profiler.EndSample();
			}
		}

		protected override void OnCreate() {
			_ballQuery = GetEntityQuery(typeof(BallData));
		}

		protected override void OnUpdate()
		{
			new DynamicBroadPhaseJob {
				Chunks = _ballQuery.CreateArchetypeChunkArray(Allocator.TempJob),
				BallType = GetArchetypeChunkComponentType<BallData>(true),
				OverlappingDynamicBufferType = GetArchetypeChunkBufferType<OverlappingDynamicBufferElement>(),
				EntityChunkType = GetArchetypeChunkEntityType()
			}.Run();
		}
	}
}
