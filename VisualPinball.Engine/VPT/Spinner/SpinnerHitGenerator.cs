// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

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

		public HitCircle[] GetHitCircles(float height, IItem item) {

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
						ItemType.Spinner,
						item
					),
					new HitCircle(
						new Vertex2D(_data.Center.X - cs * halfLength, _data.Center.Y - sn * halfLength),
						_data.Length * 0.075f,
						height + _data.Height,
						height + h,
						ItemType.Spinner,
						item
					)
				};
			}
			return new HitCircle[0];
		}
	}
}
