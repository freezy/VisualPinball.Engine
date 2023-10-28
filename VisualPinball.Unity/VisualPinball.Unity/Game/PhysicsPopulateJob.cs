using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace VisualPinball.Unity
{
	[BurstCompile(CompileSynchronously = true)]
	internal struct PhysicsPopulateJob : IJob
	{
		[ReadOnly]
		public NativeColliders Colliders;
		public NativeOctree<int> Octree;

		public void Execute()
		{
			for (var i = 0; i < Colliders.Length; i++) {
				Octree.Insert(i, Colliders.GetAabb(i));
			}
		}
	}
}
