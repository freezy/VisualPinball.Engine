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

// ReSharper disable CommentTypo
// ReSharper disable CompareOfFloatsByEqualityOperator

using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class FlipperHit : HitObject
	{
		private readonly FlipperMover _mover;
		private readonly FlipperData _data;
		private readonly TableData _tableData;
		private uint _lastHitTime;                                             // m_last_hittime

		public HitCircle HitCircleBase => _mover.HitCircleBase;

		public FlipperHit(FlipperData data, Table.Table table, IItem item) : base(ItemType.Flipper, item)
		{
			data.UpdatePhysicsSettings(table);
			_mover = new FlipperMover(data, table, item);
			_data = data;
			_tableData = table.Data;
			UpdatePhysicsFromFlipper();
		}

		public override void SetIndex(int index, int version)
		{
			base.SetIndex(index, version);
			HitCircleBase.SetIndex(index, version);
		}

		public override void CalcHitBBox()
		{
			// Allow roundoff
			HitBBox = new Rect3D(
				_mover.HitCircleBase.Center.X - _mover.FlipperRadius - _mover.EndRadius - 0.1f,
				_mover.HitCircleBase.Center.X + _mover.FlipperRadius + _mover.EndRadius + 0.1f,
				_mover.HitCircleBase.Center.Y - _mover.FlipperRadius - _mover.EndRadius - 0.1f,
				_mover.HitCircleBase.Center.Y + _mover.FlipperRadius + _mover.EndRadius + 0.1f,
				_mover.HitCircleBase.HitBBox.ZLow,
				_mover.HitCircleBase.HitBBox.ZHigh
			);
		}

		public FlipperMover GetMoverObject()
		{
			return _mover;
		}

		public void UpdatePhysicsFromFlipper()
		{
			ElasticityFalloff = _data.OverridePhysics != 0 || _tableData.OverridePhysicsFlipper && _tableData.OverridePhysics != 0
				? _data.OverrideElasticityFalloff
				: _data.ElasticityFalloff;
			Elasticity = _data.OverridePhysics != 0 || _tableData.OverridePhysicsFlipper && _tableData.OverridePhysics != 0
				? _data.OverrideElasticity
				: _data.Elasticity;
			SetFriction(_data.OverridePhysics != 0 || _tableData.OverridePhysicsFlipper && _tableData.OverridePhysics != 0
				? _data.OverrideFriction
				: _data.Friction);
			Scatter = MathF.DegToRad( _data.OverridePhysics != 0 || _tableData.OverridePhysicsFlipper && _tableData.OverridePhysics != 0
				? _data.OverrideScatterAngle
				: _data.Scatter);
		}
	}
}
