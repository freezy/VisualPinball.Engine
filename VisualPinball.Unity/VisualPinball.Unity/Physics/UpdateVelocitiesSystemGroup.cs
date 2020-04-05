using Unity.Entities;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.Physics
{
	[DisableAutoCreation]
	[UpdateInGroup(typeof(VisualPinballSimulationSystemGroup))]
	public class UpdateVelocitiesSystemGroup : ComponentSystemGroup
	{
	}
}
