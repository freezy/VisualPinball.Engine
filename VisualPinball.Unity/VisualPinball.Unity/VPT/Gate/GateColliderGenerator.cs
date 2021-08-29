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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity
{
	internal class GateColliderGenerator
	{
		private readonly IGateData _data;
		private readonly IGateColliderData _collData;
		private readonly GateApi _api;

		internal GateColliderGenerator(GateApi gateApi, IGateData data, IGateColliderData collData)
		{
			_api = gateApi;
			_data = data;
			_collData = collData;
		}

		internal void GenerateColliders(float height, List<ICollider> colliders) // var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
		{
			var angleMin = math.min(_collData.AngleMin, _collData.AngleMax); // correct angle inversions
			var angleMax = math.max(_collData.AngleMin, _collData.AngleMax);

			// todo should probably move this to somewhere else
			_collData.AngleMin = angleMin;
			_collData.AngleMax = angleMax;

			var radAngle = math.radians(_data.Rotation);
			var tangent = new float2(math.cos(radAngle), math.sin(radAngle));

			GenerateGateCollider(colliders, height, radAngle);
			GenerateLineCollider(colliders, height, tangent);
			GenerateBracketColliders(colliders, height, tangent);
		}

		private void GenerateGateCollider(ICollection<ICollider> colliders, float height, float radAngle)
		{
			var halfLength = _data.Length * 0.5f;
			var sn = math.sin(radAngle);
			var cs = math.cos(radAngle);
			var v1 = new float2(
				_data.PosX - cs * (halfLength + PhysicsConstants.PhysSkin),
				_data.PosY - sn * (halfLength + PhysicsConstants.PhysSkin)
			);
			var v2 = new float2(
				_data.PosX + cs * (halfLength + PhysicsConstants.PhysSkin),
				_data.PosY + sn * (halfLength + PhysicsConstants.PhysSkin)
			);

			var lineSeg0 = new LineCollider(v1, v2, height, height + 2.0f * PhysicsConstants.PhysSkin, _api.GetColliderInfo());
			var lineSeg1 = new LineCollider(v2, v1, height, height + 2.0f * PhysicsConstants.PhysSkin, _api.GetColliderInfo());

			colliders.Add(new GateCollider(in lineSeg0, in lineSeg1, _api.GetColliderInfo()));
		}

		private void GenerateLineCollider(ICollection<ICollider> colliders, float height, float2 tangent)
		{
			if (_collData.TwoWay) {
				return;
			}

			// oversize by the ball's radius to prevent the ball from clipping through
			var halfLength = _data.Length * 0.5f;
			var center = new float2(_data.PosX, _data.PosY);
			var rgv0 = center + (halfLength + PhysicsConstants.PhysSkin) * tangent;
			var rgv1 = center - (halfLength + PhysicsConstants.PhysSkin) * tangent;

			var info = _api.GetColliderInfo(ItemType.Invalid); // hack to not treat this line seg as gate
			colliders.Add(new LineCollider(rgv0, rgv1, height, height + 2.0f * PhysicsConstants.PhysSkin, info)); //!! = ball diameter
		}

		private void GenerateBracketColliders(ICollection<ICollider> colliders, float height, float2 tangent)
		{
			if (!_data.ShowBracket) {
				return;
			}

			var center = new float2(_data.PosX, _data.PosY);
			var halfLength = _data.Length * 0.5f;
			colliders.Add(new CircleCollider(
				center + tangent * halfLength,
				0.01f,
				height,
				height + _data.Height,
				_api.GetColliderInfo(ItemType.Invalid) // hack to not treat this hit circle as gate
			));

			colliders.Add(new CircleCollider(
				center - tangent * halfLength,
				0.01f,
				height,
				height + _data.Height,
				_api.GetColliderInfo( ItemType.Invalid) // hack to not treat this hit circle as gate
			));
		}
	}
}
