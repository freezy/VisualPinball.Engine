using Unity.Entities;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Rubber
{
	public class RubberApi : ItemApi<Engine.VPT.Rubber.Rubber, Engine.VPT.Rubber.RubberData>, IApiHittable
	{
		public RubberApi(Engine.VPT.Rubber.Rubber item, Entity entity, Player player) : base(item, entity, player)
		{
		}
	}
}
