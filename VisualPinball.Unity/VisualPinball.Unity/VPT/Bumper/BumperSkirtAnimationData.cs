using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity.VPT.Bumper
{
	public struct BumperSkirtAnimationData : IComponentData
	{
		// dynamic
		public bool IsHit;
		public float3 BallPosition;
		public bool EnableAnimation;
		public float AnimationCounter;
		public bool DoAnimate;
		public bool DoUpdate;
		public float2 Rotation;

		// static
		public float2 Center;

	}
}
