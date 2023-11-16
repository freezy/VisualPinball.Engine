// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	public class SpinnerColliderGenerator
	{
		private readonly SpinnerApi _api;
		private readonly SpinnerComponent _component;
		private readonly float4x4 _matrix;

		public SpinnerColliderGenerator(SpinnerApi spinnerApi, SpinnerComponent component, float4x4 matrix)
		{
			_api = spinnerApi;
			_component = component;
			_matrix = matrix;
		}

		internal void GenerateColliders(ref ColliderReference colliders)
		{
			colliders.Add(new SpinnerCollider(_api.GetColliderInfo()), _matrix);
			if (_component.ShowBracket) {
				GenerateBracketColliders(ref colliders);
			}
		}

		private void GenerateBracketColliders(ref ColliderReference colliders)
		{
			const float h = 30.0f + PhysicsConstants.PhysSkin;

			// extract dimensions from translation matrix
			const float length = 80f; // 80 = size at scale 1
			var height = 0;

			/*add a hit shape for the bracket if shown, just in case if the bracket spinner height is low enough so the ball can hit it*/
			const float halfLength = length * 0.5f + length * 0.1875f;

			colliders.Add(new CircleCollider(
				new float2(halfLength, 0),
				length * 0.075f,
				height,
				height + h,
				_api.GetColliderInfo()
			), _matrix);

			colliders.Add(new CircleCollider(
				new float2( -halfLength, 0),
				length * 0.075f,
				height,
				height + h,
				_api.GetColliderInfo()
			), _matrix);
		}
	}
}
