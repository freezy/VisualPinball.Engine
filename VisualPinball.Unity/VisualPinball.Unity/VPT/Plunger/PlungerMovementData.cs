using Unity.Entities;

namespace VisualPinball.Unity.VPT.Plunger
{
	public struct PlungerMovementData : IComponentData
	{
		public float Speed;
		public bool RetractMotion;
		public float TravelLimit;
		public float Position;
	}
}
