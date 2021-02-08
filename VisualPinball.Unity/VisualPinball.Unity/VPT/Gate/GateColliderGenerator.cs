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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	internal class GateColliderGenerator
	{
		private readonly GateData _data;
		private readonly GateApi _api;

		internal GateColliderGenerator(GateApi gateApi)
		{
			_api = gateApi;
			_data = gateApi.Data;
		}

		internal void GenerateColliders(Table table, List<ICollider> colliders)
		{
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			var radAngle = math.radians(_data.Rotation);
			var tangent = new float2(math.cos(radAngle), math.sin(radAngle));

			GenerateGateCollider(table, colliders, height);
			GenerateLineCollider(table, colliders, height, tangent);
			GenerateBracketColliders(table, colliders, height, tangent);
		}

		private void GenerateGateCollider(Table table, ICollection<ICollider> colliders, float height)
		{
			var halfLength = _data.Length * 0.5f;
			var radAngle = math.radians(_data.Rotation);
			var sn = math.sin(radAngle);
			var cs = math.cos(radAngle);
			var v1 = new float2(
				_data.Center.X - cs * (halfLength + PhysicsConstants.PhysSkin),
				_data.Center.Y - sn * (halfLength + PhysicsConstants.PhysSkin)
			);
			var v2 = new float2(
				_data.Center.X + cs * (halfLength + PhysicsConstants.PhysSkin),
				_data.Center.Y + sn * (halfLength + PhysicsConstants.PhysSkin)
			);

			var lineSeg0 = new LineCollider(v1, v2, height, height + 2.0f * PhysicsConstants.PhysSkin, _api.GetColliderInfo(table));
			var lineSeg1 = new LineCollider(v2, v1, height, height + 2.0f * PhysicsConstants.PhysSkin, _api.GetColliderInfo(table));

			colliders.Add(new GateCollider(in lineSeg0, in lineSeg1, _api.GetColliderInfo(table)));
		}

		private void GenerateLineCollider(Table table, ICollection<ICollider> colliders, float height, float2 tangent)
		{
			if (_data.TwoWay) {
				return;
			}

			var halfLength = _data.Length * 0.5f;
			var angleMin = math.min(_data.AngleMin, _data.AngleMax); // correct angle inversions
			var angleMax = math.max(_data.AngleMin, _data.AngleMax);

			_data.AngleMin = angleMin;
			_data.AngleMax = angleMax;

			// oversize by the ball's radius to prevent the ball from clipping through
			var rgv0 = _data.Center.ToUnityFloat2() + tangent * (halfLength + PhysicsConstants.PhysSkin);
			var rgv1 = _data.Center.ToUnityFloat2() - tangent * (halfLength + PhysicsConstants.PhysSkin);
			var info = _api.GetColliderInfo(table);
			colliders.Add(new LineCollider(rgv0, rgv1, height, height + 2.0f * PhysicsConstants.PhysSkin, info)); //!! = ball diameter
		}

		private void GenerateBracketColliders(Table table, ICollection<ICollider> colliders, float height, float2 tangent)
		{
			if (!_data.ShowBracket) {
				return;
			}

			var halfLength = _data.Length * 0.5f;
			colliders.Add(new CircleCollider(
				_data.Center.ToUnityFloat2() + tangent * halfLength,
				0.01f,
				height,
				height + _data.Height,
				_api.GetColliderInfo(table)
			));

			colliders.Add(new CircleCollider(
				_data.Center.ToUnityFloat2() - tangent * halfLength,
				0.01f,
				height,
				height + _data.Height,
				_api.GetColliderInfo(table)
			));
		}
	}
}
