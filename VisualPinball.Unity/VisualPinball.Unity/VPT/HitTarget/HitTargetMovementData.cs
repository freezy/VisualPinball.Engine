using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct HitTargetMovementData : IComponentData
	{
		public const float DropTargetLimit = 52.0f;

		public float ZOffset;
		public float XRotation;
	}
}
