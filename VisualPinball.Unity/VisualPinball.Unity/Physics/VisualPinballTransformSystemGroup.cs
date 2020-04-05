using Unity.Entities;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.Physics
{
	[UpdateInGroup(typeof(VisualPinballSimulationSystemGroup))]
	[UpdateAfter(typeof(VisualPinballSimulatePhysicsCycleSystemGroup))]
	public class VisualPinballTransformSystemGroup : ComponentSystemGroup
	{

	}
}
