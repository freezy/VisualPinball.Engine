using Unity.Entities;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT
{
	public abstract class ItemApi<T, TData> where T : Item<TData> where TData : ItemData
	{
		protected readonly T Item;
		protected readonly Player Player;
		internal readonly Entity Entity;

		protected readonly EntityManager EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

		protected ItemApi(T item, Entity entity, Player player)
		{
			Item = item;
			Entity = entity;
			Player = player;
		}
	}
}
