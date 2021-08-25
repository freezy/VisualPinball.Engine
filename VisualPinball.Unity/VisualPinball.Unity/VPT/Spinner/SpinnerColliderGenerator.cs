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

namespace VisualPinball.Unity
{
	public class SpinnerColliderGenerator
	{
		private readonly SpinnerApi _api;
		private readonly SpinnerAuthoring _component;

		public SpinnerColliderGenerator(SpinnerApi spinnerApi, SpinnerAuthoring component)
		{
			_api = spinnerApi;
			_component = component;
		}

		internal void GenerateColliders(float height, List<ICollider> colliders)
		{
			colliders.Add(new SpinnerCollider(_component, height - _component.Height, _api.GetColliderInfo()));
			if (_component.ShowBracket) {
				GenerateBracketColliders(height, colliders);
			}
		}

		private void GenerateBracketColliders(float height, ICollection<ICollider> colliders)
		{
			const float h = 30.0f;

			/*add a hit shape for the bracket if shown, just in case if the bracket spinner height is low enough so the ball can hit it*/
			var halfLength = _component.Length * 0.5f + _component.Length * 0.1875f;
			var radAngle = math.radians(_component.Rotation);
			var sn = math.sin(radAngle);
			var cs = math.cos(radAngle);

			colliders.Add(new CircleCollider(
				new float2(_component.Position.x + cs * halfLength, _component.Position.y + sn * halfLength),
				_component.Length * 0.075f,
				height,
				height + h,
				_api.GetColliderInfo()
			));

			colliders.Add(new CircleCollider(
				new float2(_component.Position.x - cs * halfLength, _component.Position.y - sn * halfLength),
				_component.Length * 0.075f,
				height,
				height + h,
				_api.GetColliderInfo()
			));
		}
	}
}
