using Unity.Entities;

namespace VisualPinball.Unity.VPT.Trigger
{
	public struct TriggerStaticData : IComponentData
	{
		public int Shape;
		public float Radius;
		public float AnimSpeed;

		// table data
		public float TableScaleZ;
	}
}
