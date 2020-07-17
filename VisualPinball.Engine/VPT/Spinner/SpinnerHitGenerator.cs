using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Spinner
{
	public class SpinnerHitGenerator
	{
		private readonly SpinnerData _data;

		public SpinnerHitGenerator(SpinnerData data)
		{
			_data = data;
		}

		public HitCircle[] GetHitCircles(float height) {

			var h = _data.Height + 30.0f;

			if (_data.ShowBracket) {
				/*add a hit shape for the bracket if shown, just in case if the bracket spinner height is low enough so the ball can hit it*/
				var halfLength = _data.Length * 0.5f + _data.Length * 0.1875f;
				var radAngle = MathF.DegToRad(_data.Rotation);
				var sn = MathF.Sin(radAngle);
				var cs = MathF.Cos(radAngle);

				return new[] {
					new HitCircle(
						new Vertex2D(_data.Center.X + cs * halfLength, _data.Center.Y + sn * halfLength),
						_data.Length * 0.075f,
						height + _data.Height,
						height + h,
						ItemType.Spinner
					),
					new HitCircle(
						new Vertex2D(_data.Center.X - cs * halfLength, _data.Center.Y - sn * halfLength),
						_data.Length * 0.075f,
						height + _data.Height,
						height + h,
						ItemType.Spinner
					)
				};
			}
			return new HitCircle[0];
		}
	}
}
