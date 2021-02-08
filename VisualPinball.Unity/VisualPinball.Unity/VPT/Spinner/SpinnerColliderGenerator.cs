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

using System.Collections.Generic;
using Unity.Mathematics;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class SpinnerColliderGenerator
	{
		private readonly SpinnerApi _api;
		private readonly SpinnerData _data;

		public SpinnerColliderGenerator(SpinnerApi spinnerApi)
		{
			_api = spinnerApi;
			_data = spinnerApi.Data;
		}

		internal void GenerateColliders(Table table, List<ICollider> colliders)
		{
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			colliders.Add(new SpinnerCollider(_data, height, _api.GetColliderInfo(table)));
			if (_data.ShowBracket) {
				GenerateBracketColliders(table, colliders);
			}
		}

		private void GenerateBracketColliders(Table table, ICollection<ICollider> colliders)
		{
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			var h = _data.Height + 30.0f;

			/*add a hit shape for the bracket if shown, just in case if the bracket spinner height is low enough so the ball can hit it*/
			var halfLength = _data.Length * 0.5f + _data.Length * 0.1875f;
			var radAngle = math.radians(_data.Rotation);
			var sn = math.sin(radAngle);
			var cs = math.cos(radAngle);

			colliders.Add(new CircleCollider(
				new float2(_data.Center.X + cs * halfLength, _data.Center.Y + sn * halfLength),
				_data.Length * 0.075f,
				height + _data.Height,
				height + h,
				_api.GetColliderInfo(table)
			));

			colliders.Add(new CircleCollider(
				new float2(_data.Center.X - cs * halfLength, _data.Center.Y - sn * halfLength),
				_data.Length * 0.075f,
				height + _data.Height,
				height + h,
				_api.GetColliderInfo(table)
			));
		}
	}
}
