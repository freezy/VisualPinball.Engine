using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct SolenoidStateData : IComponentData
	{
		public bool Value;
	}
}
