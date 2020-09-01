using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct SolenoidStateData : IComponentData
	{
		public bool Value;
	}
}
