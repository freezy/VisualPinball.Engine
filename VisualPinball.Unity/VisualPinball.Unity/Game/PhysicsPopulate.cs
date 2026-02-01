using System;
using System.Runtime.InteropServices;
using NativeTrees;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace VisualPinball.Unity
{
	[BurstCompile(FloatPrecision.Medium, FloatMode.Fast, CompileSynchronously = true)]
	internal static class PhysicsPopulate
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void PopulateFn(IntPtr colliders, IntPtr octree);

		[BurstCompile]
		public static unsafe void PopulateUnsafe(IntPtr collidersPtr, IntPtr octreePtr)
		{
			ref var colliders = ref UnsafeUtility.AsRef<NativeColliders>(collidersPtr.ToPointer());
			ref var octree = ref UnsafeUtility.AsRef<NativeOctree<int>>(octreePtr.ToPointer());

			for (var i = 0; i < colliders.Length; i++) {
				octree.Insert(i, colliders.GetAabb(i));
			}
		}


		[BurstCompile]
		public static void Populate(ref NativeColliders colliders, ref NativeOctree<int> octree)
		{
			for (var i = 0; i < colliders.Length; i++) {
				octree.Insert(i, colliders.GetAabb(i));
			}
		}

		public static FunctionPointer<PopulateFn> Ptr;

		public static void Init()
		{
			Ptr = BurstCompiler.CompileFunctionPointer<PopulateFn>(PopulateUnsafe);
		}
	}
}
