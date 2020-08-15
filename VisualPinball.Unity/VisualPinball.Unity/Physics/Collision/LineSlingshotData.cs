using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct LineSlingshotData : IComponentData
	{
		public bool IsDisabled;
		public float Threshold;
	}
}
