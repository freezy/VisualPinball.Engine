using Unity.Entities;
using Unity.Transforms;

namespace VisualPinball.Unity.Game
{
	[UpdateBefore(typeof(TransformSystemGroup))]
	public class VisualPinballSimulationSystemGroup : ComponentSystemGroup
	{

	}
}
