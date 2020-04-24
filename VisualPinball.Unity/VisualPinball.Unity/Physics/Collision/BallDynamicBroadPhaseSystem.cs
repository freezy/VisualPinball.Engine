using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
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

			public void Execute()
			{
				var ballBounds = new NativeList<Aabb>(Allocator.Temp);
				for (var j = 0; j < Chunks.Length; j++) {
					var balls = Chunks[j].GetNativeArray(BallType);
					//Debug.Log($"We have {balls.Length} ball(s) and ({Chunks.Length} chunk(s)!");

					for (int i = 0; i < Chunks[j].Count; i++) {
						ballBounds.Add(balls[i].Aabb);
					}
				}

				var kdRoot = new KdRoot(ballBounds.ToArray());
			}
		}

		protected override void OnCreate() {
			BallQuery = GetEntityQuery(typeof(BallData));
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps) {
			return new BallDynamicBroadPhaseJob {
				Chunks = BallQuery.CreateArchetypeChunkArray(Allocator.TempJob),
				BallType = GetArchetypeChunkComponentType<BallData>(true)
			}.Schedule(inputDeps);
		}

		// EntityQuery ballChunkQuery;
		// protected override void OnCreate()
		// {
		// 	ballChunkQuery = GetEntityQuery(new EntityQueryDesc {
		// 		All = new[] { ComponentType.ReadOnly<BallData>() }
		// 	});
		// }
		//
		// [BurstCompile]
		// struct BallDynamicBroadPhaseJob : IJobChunk
		// {
		// 	[ReadOnly] public ArchetypeChunkComponentType<BallData> BallDataTypeInfo;
		//
		// 	public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
		// 	{
		// 		var balls = chunk.GetNativeArray<BallData>(BallDataTypeInfo);
		// 		Debug.Log($"We have {balls.Length} balls!");
		// 	}
		// }
		//
		// protected override void OnUpdate()
		// {
		// 	var job = new BallDynamicBroadPhaseJob {
		// 		BallDataTypeInfo = GetArchetypeChunkComponentType<BallData>(true)
		// 	};
		// 	Dependency = job.Schedule(ballChunkQuery, this.Dependency);
		//
		// 	//
		// 	// var balls = new List<BallData>();
		// 	// Entities.WithoutBurst().ForEach((in BallData ballData) => {
		// 	// 	balls.Add(ballData);
		// 	// }).Run();
		// 	// Debug.Log($"We have {balls.Count} balls!");
		// }
	}
}
