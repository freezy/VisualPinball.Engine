using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct BumperRingAnimationData : IComponentData
	{
		public bool IsHit;
		public float DropOffset;
		public float HeightScale;
		public float ScaleZ;
		public bool DoAnimate;
		public bool AnimateDown;
		public float Speed;
		public float Offset;
	}
}
