// ReSharper disable CommentTypo
// ReSharper disable CompareOfFloatsByEqualityOperator

using VisualPinball.Engine.Game;
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
		private readonly EventProxy _events;
		private uint _lastHitTime;                                             // m_last_hittime

		public HitCircle HitCircleBase => _mover.HitCircleBase;

		public FlipperHit(FlipperData data, EventProxy events, Table.Table table) : base(ItemType.Flipper)
		{
			data.UpdatePhysicsSettings(table);
			_events = events;
			_mover = new FlipperMover(data, events, table);
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
