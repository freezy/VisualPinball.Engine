using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct PlungerAnimationData : IComponentData
	{
		public int CurrentFrame;
		public bool IsDirty;

	}
}
