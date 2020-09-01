using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct LineSlingshotData : IComponentData
	{
		public bool IsDisabled;
		public float Threshold;
	}
}
