using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity.VPT.Bumper
{
	public struct BumperSkirtAnimationData : IComponentData
	{
		public bool IsHit;
		public float3 BallPosition;
	}
}
