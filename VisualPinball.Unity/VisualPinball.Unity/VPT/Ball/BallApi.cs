using Unity.Entities;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Ball
{
	public class BallApi
	{
		private readonly Entity Entity;
		private readonly Player Player;

		protected readonly EntityManager EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

		public BallApi(Entity entity, Player player)
		{
			Entity = entity;
			Player = player;
		}
	}
}
