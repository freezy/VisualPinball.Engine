using Unity.Entities;

namespace VisualPinball.Unity.VPT.Plunger
{
	public struct PlungerAnimationData : IComponentData
	{
		public int CurrentFrame;
		public bool IsDirty;

	}
}
