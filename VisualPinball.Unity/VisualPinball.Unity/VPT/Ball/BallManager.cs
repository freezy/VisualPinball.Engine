using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.Ball
{
	public class BallManager
	{
		private int _id = 0;

		private EntityArchetype _archetype = World.DefaultGameObjectInjectionWorld.EntityManager
			.CreateArchetype(
				typeof(Parent),
				typeof(LocalToWorld),
				typeof(Translation),
				typeof(Rotation),
				typeof(RenderMesh)
			);


		public BallApi CreateBall(IBallCreationPosition ballCreator, float radius, float mass)
		{
			return null;
		}
	}
}
