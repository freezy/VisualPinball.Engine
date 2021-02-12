// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using Unity.Mathematics;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class DebugBallCreator : IBallCreationPosition
	{
		private float _x;
		private float _y;

		private readonly float _kickAngle;
		private readonly float _kickForce;

		public DebugBallCreator()
		{
			_x = -1;
			_y = -1;
		}

		public DebugBallCreator(float x, float y)
		{
			_x = x;
			_y = y;
		}

		public DebugBallCreator(float x, float y, float kickAngle, float kickForce)
		{
			_x = x;
			_y = y;
			_kickAngle = math.radians(kickAngle);
			_kickForce = kickForce;
		}

		public Vertex3D GetBallCreationPosition(Table table)
		{
			if (_x < 0 || _y < 0) {
				_x = table.Width / 2f;
				_y = table.Height / 3f;

				// _x = Random.Range(table.Width / 6f, table.Width / 6f * 5f);
				// _y = Random.Range(table.Height / 8f, table.Height / 2f);
			}
			return new Vertex3D(_x, _y, 0);
		}

		public Vertex3D GetBallCreationVelocity(Table table)
		{
			return new Vertex3D(
				MathF.Sin(_kickAngle) * _kickForce,
				-MathF.Cos(_kickAngle) * _kickForce,
				0
			);
		}
	}
}
