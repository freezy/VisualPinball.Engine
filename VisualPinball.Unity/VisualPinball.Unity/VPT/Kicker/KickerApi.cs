using Unity.Entities;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.VPT.Kicker
{
	public class KickerApi : ItemApi<Engine.VPT.Kicker.Kicker, KickerData>
	{
		public KickerApi(Engine.VPT.Kicker.Kicker item, Entity entity, Player player) : base(item, entity, player)
		{
		}

		public BallApi CreateBall()
		{
			return Player.CreateBall(Item);
		}

		public BallApi CreateSizedBallWithMass(float radius, float mass)
		{
			return Player.CreateBall(Item, radius, mass);
		}

		public BallApi CreateSizedBall(float radius)
		{
			return Player.CreateBall(Item, radius);
		}
	}
}
