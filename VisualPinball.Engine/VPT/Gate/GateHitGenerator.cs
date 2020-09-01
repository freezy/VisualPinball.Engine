using VisualPinball.Engine.Common;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Gate
{
	public class GateHitGenerator
	{
		private readonly GateData _gateData;

		public GateHitGenerator(GateData gateData)
		{
			_gateData = gateData;
		}

		public LineSeg[] GenerateLineSegs(float height, Vertex2D tangent, IItem item)
		{
			if (_gateData.TwoWay) {
				return new LineSeg[0];
			}

			var halfLength = _gateData.Length * 0.5f;
			var angleMin = MathF.Min(_gateData.AngleMin, _gateData.AngleMax); // correct angle inversions
			var angleMax = MathF.Max(_gateData.AngleMin, _gateData.AngleMax);

			_gateData.AngleMin = angleMin;
			_gateData.AngleMax = angleMax;

			// oversize by the ball's radius to prevent the ball from clipping through
			var rgv = new[] {
				_gateData.Center.Clone().Add(tangent.Clone().MultiplyScalar(halfLength + PhysicsConstants.PhysSkin)),
				_gateData.Center.Clone().Sub(tangent.Clone().MultiplyScalar(halfLength + PhysicsConstants.PhysSkin)),
			};
			var lineSeg = new LineSeg(rgv[0], rgv[1], height, height + 2.0f * PhysicsConstants.PhysSkin, ItemType.Gate, item); //!! = ball diameter

			lineSeg.SetElasticity(_gateData.Elasticity);
			lineSeg.SetFriction(_gateData.Friction);

			return new[] {lineSeg};
		}

		public GateHit GenerateGateHit(float height, IItem item)
		{
			var hit = new GateHit(_gateData, height, item) {
				TwoWay = _gateData.TwoWay,
				FireEvents = true,
				IsEnabled = _gateData.IsCollidable
			};
			return hit;
		}

		public HitCircle[] GenerateBracketHits(float height, Vertex2D tangent, IItem item)
		{
			var halfLength = _gateData.Length * 0.5f;
			if (_gateData.ShowBracket) {
				return new[] {
					new HitCircle(_gateData.Center.Clone().Add(tangent.Clone().MultiplyScalar(halfLength)), 0.01f,
						height, height + _gateData.Height, ItemType.Gate, item),
					new HitCircle(_gateData.Center.Clone().Sub(tangent.Clone().MultiplyScalar(halfLength)), 0.01f,
						height, height + _gateData.Height, ItemType.Gate, item)
				};
			}

			return new HitCircle[0];
		}
	}
}
