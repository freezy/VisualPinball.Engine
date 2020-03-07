using Unity.Entities;
using VisualPinball.Unity.Physics.Flipper;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class FlipperApi
	{
		private readonly Flipper _flipper;
		private readonly Entity _entity;
		private readonly EntityManager _manager = World.DefaultGameObjectInjectionWorld.EntityManager;

		public FlipperApi(Flipper flipper, Entity entity)
		{
			_flipper = flipper;
			_entity = entity;
		}

		public void RotateToEnd()
		{
			_manager.SetComponentData(_entity, new SolenoidStateData { Value = true });
		}

		public void RotateToStart()
		{
			_manager.SetComponentData(_entity, new SolenoidStateData { Value = false });
		}
	}
}
