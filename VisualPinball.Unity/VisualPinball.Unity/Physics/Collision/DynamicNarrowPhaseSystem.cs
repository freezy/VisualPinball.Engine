using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class DynamicNarrowPhaseSystem : SystemBase
	{
		[BurstCompile]
		private struct DynamicNarrowPhaseJob : IJob
		{
			[DeallocateOnJobCompletion]
			public NativeArray<ArchetypeChunk> Chunks;

			public ArchetypeChunkComponentType<BallData> BallDataType;
			public ArchetypeChunkBufferType<ContactBufferElement> ContactBufferElementType;
			[ReadOnly] public ArchetypeChunkComponentType<CollisionEventData> CollisionEventDataType;
			[ReadOnly] public ArchetypeChunkEntityType EntityType;
			public ArchetypeChunkBufferType<OverlappingDynamicBufferElement> OverlappingDynamicBufferType;

			public void Execute()
			{
				if (Chunks.Length == 0) {
					return;
				}

				// Profiler.BeginSample("DynamicNarrowPhaseSystem");

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

				for (var j = 0; j < Chunks.Length; j++) {

					var chunkBallData = Chunks[j].GetNativeArray(BallDataType);
					var chunkCollisions = Chunks[j].GetBufferAccessor(ContactBufferElementType);
					var chunkCollEventData = Chunks[j].GetNativeArray(CollisionEventDataType);
					var chunkCount = Chunks[j].Count;
					var chunkDynamicEntities = Chunks[j].GetBufferAccessor(OverlappingDynamicBufferType);

					for (var i = 0; i < chunkCount; i++) {

						// pick "current" ball
						var ball = chunkBallData[i];
						var collEvent = chunkCollEventData[i];
						var dynamicEntities = chunkDynamicEntities[i];
						var contacts = chunkCollisions[i];

						// pick "other" ball
						ref var otherEntity = ref collEvent.ColliderEntity;

						for (var k = 0; i < dynamicEntities.Length; i++) {
							var collBallEntity = dynamicEntities[k].Value;
							var chunkCollBallData = Chunks[chunkIndices[otherEntity]].GetNativeArray(BallDataType);
							var collBall = chunkCollBallData[positionIndices[otherEntity]];

							var newCollEvent = new CollisionEventData();
							var newTime = BallCollider.HitTest(ref newCollEvent, ref collBall, in ball, collEvent.HitTime);

							// write back
							chunkCollBallData[positionIndices[otherEntity]] = collBall;
							SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in collBallEntity, newTime);
						}

						chunkDynamicEntities[i].Clear();
					}
				}

				// Profiler.EndSample();
			}

			private static void SaveCollisions(ref CollisionEventData collEvent, ref CollisionEventData newCollEvent,
				ref DynamicBuffer<ContactBufferElement> contacts, in Entity ballEntity, float newTime)
			{
				var validHit = newTime >= 0 && newTime <= collEvent.HitTime;

				if (newCollEvent.IsContact || validHit) {
					newCollEvent.SetCollider(ballEntity);
					newCollEvent.HitTime = newTime;
					if (newCollEvent.IsContact) {
						contacts.Add(new ContactBufferElement(ballEntity, newCollEvent));

					} else {                         // if (validhit)
						collEvent = newCollEvent;
					}
				}
			}
		}

		private EntityQuery _query;

		protected override void OnCreate()
		{
			var queryDesc = new EntityQueryDesc {
				All = new[]{ typeof(BallData), ComponentType.ReadOnly<CollisionEventData>() }
			};
			_query = GetEntityQuery(queryDesc);
		}

		protected override void OnUpdate()
		{
			new DynamicNarrowPhaseJob {
				Chunks = _query.CreateArchetypeChunkArray(Allocator.TempJob),
				BallDataType = GetArchetypeChunkComponentType<BallData>(),
				ContactBufferElementType = GetArchetypeChunkBufferType<ContactBufferElement>(),
				CollisionEventDataType = GetArchetypeChunkComponentType<CollisionEventData>(true),
				EntityType = GetArchetypeChunkEntityType(),
				OverlappingDynamicBufferType = GetArchetypeChunkBufferType<OverlappingDynamicBufferElement>()
			}.Run();
		}
	}
}
