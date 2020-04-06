using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity.VPT.Ball
{
	public struct BallData : IComponentData
	{
		public float3 Position;
		public float Radius;
		public bool IsFrozen;
	}
}
