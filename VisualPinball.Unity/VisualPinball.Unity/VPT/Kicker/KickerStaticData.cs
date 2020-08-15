using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public struct KickerStaticData : IComponentData
	{
		public bool LegacyMode;
		public bool FallThrough;
		public float2 Center;
		public float ZLow;
		public float HitAccuracy;
	}
}
