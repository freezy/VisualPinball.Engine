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
			GenerateSpinnerCollider(ref colliders);
			if (_component.ShowBracket) {
				GenerateBracketColliders(ref colliders);
			}
		}

		/// <summary>
		/// The collider that triggers the animation
		/// </summary>
		/// <param name="colliders"></param>
		private void GenerateSpinnerCollider(ref ColliderReference colliders)
		{
			const float halfLength = 40f;

			// note: this has diverged a bit from the vpx code: instead of generating the colliders at the correct
			// position, we generate them relative to the origin and then transform them later.
			var v1 = new float2(
				- (halfLength + PhysicsConstants.PhysSkin), // through the edge of the
				0  // spinner
			);
			var v2 = new float2(
				halfLength + PhysicsConstants.PhysSkin, // oversize by the ball radius
				0  // this will prevent clipping
			);

			// todo probably broke surface
			var lineSeg0 = new LineCollider(v1, v2, -2f * PhysicsConstants.PhysSkin, 0, _api.GetColliderInfo());
			var lineSeg1 = new LineCollider(v2, v1, -2f * PhysicsConstants.PhysSkin, 0, _api.GetColliderInfo());

			colliders.Add(new SpinnerCollider(in lineSeg0, in lineSeg1, _api.GetColliderInfo()), _matrix);
		}

		private void GenerateBracketColliders(ref ColliderReference colliders)
		{
			const float h = 30.0f + PhysicsConstants.PhysSkin;
			const float length = 80f; // 80 = size at scale 1

			/*add a hit shape for the bracket if shown, just in case if the bracket spinner height is low enough so the ball can hit it*/
			const float halfLength = length * 0.5f + length * 0.1875f;

			colliders.Add(new CircleCollider(
				new float2(halfLength, 0),
				length * 0.075f,
				-h,
				0,
				_api.GetColliderInfo()
			), _matrix);

			colliders.Add(new CircleCollider(
				new float2( -halfLength, 0),
				length * 0.075f,
				-h,
				0,
				_api.GetColliderInfo()
			), _matrix);
		}
	}
}
