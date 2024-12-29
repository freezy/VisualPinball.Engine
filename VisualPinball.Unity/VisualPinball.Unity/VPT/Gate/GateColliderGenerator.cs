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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity
{
	internal class GateColliderGenerator
	{
		private readonly IGateData _data;
		private readonly IGateColliderData _collData;
		private readonly GateApi _api;
		private readonly float4x4 _matrix;

		private readonly float _zLow;
		private readonly float _dist;
		private const float ZHigh = 0;

		internal GateColliderGenerator(GateApi gateApi, IGateData data, IGateColliderData collData, float zLow, float dist, float4x4 matrix)
		{
			_api = gateApi;
			_data = data;
			_collData = collData;
			_matrix = matrix;
			_zLow = zLow;
			_dist = dist;
		}

		internal void GenerateColliders(ref ColliderReference colliders) // var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
		{
			var angleMin = math.min(_collData.AngleMin, _collData.AngleMax); // correct angle inversions
			var angleMax = math.max(_collData.AngleMin, _collData.AngleMax);

			// todo should probably move this to somewhere else
			_collData.AngleMin = angleMin;
			_collData.AngleMax = angleMax;

			var radAngle = math.radians(_data.Rotation);
			var tangent = new float2(math.cos(radAngle), math.sin(radAngle));

			GenerateGateCollider(ref colliders);
			GenerateLineCollider(ref colliders);
			if (_data.ShowBracket) {
				GenerateBracketColliders(ref colliders);
			}
		}

		/// <summary>
		/// The collider that triggers the animation
		/// </summary>
		/// <param name="colliders"></param>
		private void GenerateGateCollider(ref ColliderReference colliders)
		{
			// note: this has diverged a bit from the vpx code: instead of generating the colliders at the correct
			// position, we generate them relative to the origin and then transform them.

			const float halfLength = 10f;
			var v1 = new float2(-(halfLength + PhysicsConstants.PhysSkin), _dist);
			var v2 = new float2(halfLength + PhysicsConstants.PhysSkin, _dist);

			var lineSeg0 = new LineCollider(v1, v2, _zLow, ZHigh, _api.GetColliderInfo());
			var lineSeg1 = new LineCollider(v2, v1, _zLow, ZHigh, _api.GetColliderInfo());

			colliders.Add(new GateCollider(in lineSeg0, in lineSeg1, _api.GetColliderInfo()), _matrix);
		}

		/// <summary>
		/// The collider the blocks the ball, if not two-way
		/// </summary>
		/// <param name="colliders"></param>
		private void GenerateLineCollider(ref ColliderReference colliders)
		{
			if (_collData.TwoWay) {
				return;
			}

			// oversize by the ball's radius to prevent the ball from clipping through
			const float halfLength = 10f;
			var rgv0 = new float2(halfLength + PhysicsConstants.PhysSkin, 0f);
			var rgv1 = new float2(-(halfLength + PhysicsConstants.PhysSkin), 0f);

			var info = _api.GetColliderInfo(ItemType.Invalid); // hack to not treat this line seg as gate
			colliders.Add(new LineCollider(rgv0, rgv1, -2f * PhysicsConstants.PhysSkin, 0, info), _matrix); //!! = ball diameter
		}

		/// <summary>
		/// The colliders left and right to the other colliders, in case the bracket is shown.
		/// </summary>
		/// <param name="colliders"></param>
		private void GenerateBracketColliders(ref ColliderReference colliders)
		{
			const float halfLength = 45f;

			colliders.Add(new CircleCollider(
				new float2(halfLength, 0),
				1f,
				_zLow,
				0,
				_api.GetColliderInfo(ItemType.Invalid) // hack to not treat this hit circle as gate
			), _matrix);

			colliders.Add(new CircleCollider(
				new float2(-halfLength, 0),
				1f,
				_zLow,
				0,
				_api.GetColliderInfo(ItemType.Invalid) // hack to not treat this hit circle as gate
			), _matrix);
		}
	}
}
