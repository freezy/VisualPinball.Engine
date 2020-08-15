using Unity.Entities;

namespace VisualPinball.Unity
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
