using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace VisualPinball.Unity
{
	[BurstCompile(CompileSynchronously = true)]
	internal struct PhysicsPopulateJob : IJob
	{
		[ReadOnly]
		public BlobAssetReference<ColliderBlob> Colliders;
		public NativeOctree<int> Octree;

		public void Execute()
		{
			for (var i = 0; i < Colliders.Value.Colliders.Length; i++) {
				Octree.Insert(Colliders.GetId(i), Colliders.GetAabb(i));
			}
		}
	}
}
