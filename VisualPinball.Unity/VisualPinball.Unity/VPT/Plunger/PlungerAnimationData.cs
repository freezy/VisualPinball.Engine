using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct PlungerAnimationData : IComponentData
	{
		public int CurrentFrame;
		public bool IsDirty;

	}
}
