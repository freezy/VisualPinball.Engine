using Unity.Entities;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct LineSlingshotData : IComponentData
	{
		public bool IsDisabled;
		public float Threshold;
	}
}
