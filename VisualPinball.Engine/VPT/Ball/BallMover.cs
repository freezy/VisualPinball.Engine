using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Ball
{
	public class BallMover : IMoverObject
	{
		private readonly BallState _state;
		private readonly BallHit _hit;

		public BallMover(BallState state, BallHit hit)
		{
			_state = state;
			_hit = hit;
		}

		public void UpdateDisplacements(float dTime)
		{
			if (!_state.IsFrozen) {
				_state.Pos.Add(_hit.Vel.Clone().MultiplyScalar(dTime));
				_hit.CalcHitBBox();

				var mat3 = new Matrix2D().CreateSkewSymmetric(_hit.AngularVelocity);

				var addedOrientation = new Matrix2D();
				addedOrientation.MultiplyMatrix(mat3, _state.Orientation);
				addedOrientation.MultiplyScalar(dTime);

				_state.Orientation.AddMatrix(addedOrientation, _state.Orientation);
				_state.Orientation.OrthoNormalize();

				_hit.AngularVelocity.Set(_hit.AngularMomentum.Clone().DivideScalar(_hit.Inertia));
			}
		}

		public void UpdateVelocities(PlayerPhysics physics)
		{
			if (!_state.IsFrozen) {
				_hit.Vel.Add(physics.Gravity.Clone().MultiplyScalar(PhysicsConstants.PhysFactor));

				// todo nudge
				// _hit.Vel.X += player.NudgeX; // depends TODO on STEPTIME
				// _hit.Vel.Y += player.NudgeY;
				// _hit.Vel.Sub(player.TableVelDelta);
			}
			_hit.CalcHitBBox();
		}
	}
}
