// ReSharper disable CommentTypo
// ReSharper disable CompareOfFloatsByEqualityOperator

using NLog;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class FlipperMover : IMoverObject
	{
		private readonly FlipperData _data;
		private readonly FlipperState _state;
		private readonly EventProxy _events;
		private readonly TableData _tableData;

		public readonly HitCircle HitCircleBase;                               // m_hitcircleBase
		public readonly float EndRadius;                                       // m_endradius
		public readonly float FlipperRadius;                                   // m_flipperradius

		/// <summary>
		/// Moment of inertia
		/// </summary>
		public float Inertia;                                                  // m_inertia

		/// <summary>
		/// base norms at zero degrees
		/// </summary>
		public readonly Vertex2D ZeroAngNorm = new Vertex2D();                 // m_zeroAngNorm

		/// <summary>
		/// -1,0,1
		/// </summary>
		public short EnableRotateEvent;                                        // m_enableRotateEvent

		public float AngleStart;                                               // m_angleStart
		public float AngleEnd;                                                 // m_angleEnd
		public float AngleSpeed;                                               // m_angleSpeed
		public float ContactTorque;                                            // m_contactTorque

		public bool IsInContact;                                               // m_isInContact
		public bool LastHitFace;                                               // m_lastHitFace

		private readonly bool _direction;                                      // m_direction
		private float _angularMomentum;                                        // m_angularMomentum
		private float _angularAcceleration;                                    // m_angularAcceleration
		private float _curTorque;                                              // m_curTorque

		/// <summary>
		/// is solenoid enabled?
		/// </summary>
		private bool _solState; // m_solState

		public FlipperMover(FlipperData data, FlipperState state, EventProxy events, Table.Table table)
		{
			_data = data;
			_state = state;
			_events = events;
			_tableData = table.Data;

			if (data.FlipperRadiusMin > 0 && data.FlipperRadiusMax > data.FlipperRadiusMin) {
				data.FlipperRadius = data.FlipperRadiusMax - (data.FlipperRadiusMax - data.FlipperRadiusMin) /* m_ptable->m_globalDifficulty*/;
				data.FlipperRadius = MathF.Max(data.FlipperRadius, data.BaseRadius - data.EndRadius + 0.05f);

			} else {
				data.FlipperRadius = data.FlipperRadiusMax;
			}

			EndRadius = MathF.Max(data.EndRadius, 0.01f); // radius of flipper end
			FlipperRadius = MathF.Max(data.FlipperRadius, 0.01f); // radius of flipper arc, center-to-center radius
			AngleStart = MathF.DegToRad(data.StartAngle);
			AngleEnd = MathF.DegToRad(data.EndAngle);

			if (AngleEnd == AngleStart) {
				// otherwise hangs forever in collisions/updates
				AngleEnd += 0.0001f;
			}

			var height = table.GetSurfaceHeight(data.Surface, data.Center.X, data.Center.Y);
			var baseRadius = MathF.Max(data.BaseRadius, 0.01f);
			HitCircleBase = new HitCircle(data.Center, baseRadius, height, height + data.Height, ItemType.Flipper);

			IsInContact = false;
			EnableRotateEvent = 0;
			AngleSpeed = 0;

			_direction = AngleEnd >= AngleStart;
			_solState = false;
			_curTorque = 0.0f;
			_state.Angle = AngleStart;
			_angularMomentum = 0;
			_angularAcceleration = 0;

			var ratio = (baseRadius - EndRadius) / FlipperRadius;

			// model inertia of flipper as that of rod of length flipr around its end
			var mass = _data.GetFlipperMass(_tableData);
			Inertia = (float) (1.0 / 3.0) * mass * (FlipperRadius * FlipperRadius);

			LastHitFace = false; // used to optimize hit face search order

			// F2 Norm, used in Green's transform, in FPM time search  // =  sinf(faceNormOffset)
			ZeroAngNorm.X = MathF.Sqrt(1.0f - ratio * ratio);
			// F1 norm, change sign of x component, i.E -zeroAngNorm.X // = -cosf(faceNormOffset)
			ZeroAngNorm.Y = -ratio;
		}

		public void UpdateDisplacements(float dTime)
		{
			_state.Angle += AngleSpeed * dTime; // move flipper angle

			var angleMin = MathF.Min(AngleStart, AngleEnd);
			var angleMax = MathF.Max(AngleStart, AngleEnd);

			if (_state.Angle > angleMax) {
				_state.Angle = angleMax;
			}

			if (_state.Angle < angleMin) {
				_state.Angle = angleMin;
			}

			if (MathF.Abs(AngleSpeed) < 0.0005) {
				// avoids "jumping balls" when two or more balls held on flipper (and more other balls are in play) //!! make dependent on physics update rate
				return;
			}

			var handleEvent = false;

			if (_state.Angle == angleMax) {
				// hit stop?
				if (AngleSpeed > 0) {
					handleEvent = true;
				}

			} else if (_state.Angle == angleMin) {
				if (AngleSpeed < 0) {
					handleEvent = true;
				}
			}

			if (handleEvent) {
				var angleSpeed = MathF.Abs(MathF.RadToDeg(AngleSpeed));
				_angularMomentum *= -0.3f; // make configurable?
				AngleSpeed = _angularMomentum / Inertia;

				if (EnableRotateEvent > 0) {
					Logger.Info("[{0}] Flipper is up", _data.Name);
					_events.FireVoidEventParam(EventId.LimitEventsEos, angleSpeed); // send EOS event

				} else if (EnableRotateEvent < 0) {
					Logger.Info("[{0}] Flipper is down", _data.Name);
					_events.FireVoidEventParam(EventId.LimitEventsBos, angleSpeed); // send Beginning of Stroke/Park event
				}

				EnableRotateEvent = 0;
			}
		}

		public void UpdateVelocities(PlayerPhysics physics)
		{
			var desiredTorque = _data.GetStrength(_tableData);
			if (!_solState) {
				// this.True solState = button pressed, false = released
				desiredTorque *= -_data.GetReturnRatio(_tableData);
			}

			// hold coil is weaker
			var eosAngle = MathF.DegToRad(_data.GetTorqueDampingAngle(_tableData));
			if (MathF.Abs(_state.Angle - AngleEnd) < eosAngle) {
				// fade in/out damping, depending on angle to end
				var lerp = MathF.Sqrt(MathF.Sqrt(MathF.Abs(_state.Angle - AngleEnd) / eosAngle));
				desiredTorque *= lerp + _data.GetTorqueDamping(_tableData) * (1 - lerp);
			}

			if (!_direction) {
				desiredTorque = -desiredTorque;
			}

			var torqueRampUpSpeed = _data.GetRampUpSpeed(_tableData);
			if (torqueRampUpSpeed <= 0) {
				// set very high for instant coil response
				torqueRampUpSpeed = 1e6f;

			} else {
				torqueRampUpSpeed = MathF.Min(_data.GetStrength(_tableData) / torqueRampUpSpeed, 1e6f);
			}

			// update current torque linearly towards desired torque
			// (simple model for coil hysteresis)
			if (desiredTorque >= _curTorque) {
				_curTorque = MathF.Min(_curTorque + torqueRampUpSpeed * PhysicsConstants.PhysFactor, desiredTorque);

			} else {
				_curTorque = MathF.Max(_curTorque - torqueRampUpSpeed * PhysicsConstants.PhysFactor, desiredTorque);
			}

			// resolve contacts with stoppers
			var torque = _curTorque;
			IsInContact = false;
			if (MathF.Abs(AngleSpeed) <= 1e-2) {
				var angleMin = MathF.Min(AngleStart, AngleEnd);
				var angleMax = MathF.Max(AngleStart, AngleEnd);

				if (_state.Angle >= angleMax - 1e-2 && torque > 0) {
					_state.Angle = angleMax;
					IsInContact = true;
					ContactTorque = torque;
					_angularMomentum = 0;
					torque = 0;

				} else if (_state.Angle <= angleMin + 1e-2 && torque < 0) {
					_state.Angle = angleMin;
					IsInContact = true;
					ContactTorque = torque;
					_angularMomentum = 0;
					torque = 0;
				}
			}

			_angularMomentum += PhysicsConstants.PhysFactor * torque;
			AngleSpeed = _angularMomentum / Inertia;
			_angularAcceleration = torque / Inertia;
		}

		public void SetSolenoidState(bool s)
		{
			_solState = s;
		}

		// rigid body functions
		public Vertex3D SurfaceVelocity(Vertex3D surfP)
		{
			return Vertex3D.CrossZ(AngleSpeed, surfP);
		}

		public float GetHitTime()
		{
			if (AngleSpeed == 0) {
				return -1.0f;
			}

			var angleMin = MathF.Min(AngleStart, AngleEnd);
			var angleMax = MathF.Max(AngleStart, AngleEnd);
			var dist = AngleSpeed > 0
				? angleMax - _state.Angle // >= 0
				: angleMin - _state.Angle; // <= 0

			var hitTime = dist / AngleSpeed;

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0) {
				return -1.0f;
			}

			return hitTime;
		}

		public void ApplyImpulse(Vertex3D rotI)
		{
			_angularMomentum += rotI.Z;                    // only rotation about z axis
			AngleSpeed = _angularMomentum / Inertia;       // figure TODO out moment of inertia
		}

		public Vertex3D SurfaceAcceleration(Vertex3D surfP)
		{
			// tangential acceleration = (0, 0, omega) x surfP
			var tangAcc = Vertex3D.CrossZ(_angularAcceleration, surfP);

			// centripetal acceleration = (0,0,omega) x ( (0,0,omega) x surfP )
			var av2 = AngleSpeed * AngleSpeed;
			var centrAcc = new Vertex3D(-av2 * surfP.X, -av2 * surfP.Y, 0);

			return tangAcc.Add(centrAcc);
		}

		public void SetStartAngle(float r)
		{
			AngleStart = r;
			var angleMin = MathF.Min(AngleStart, AngleEnd);
			var angleMax = MathF.Max(AngleStart, AngleEnd);
			if (_state.Angle > angleMax) {
				_state.Angle = angleMax;
			}

			if (_state.Angle < angleMin) {
				_state.Angle = angleMin;
			}
		}

		public void SetEndAngle(float r)
		{
			AngleEnd = r;
			var angleMin = MathF.Min(AngleStart, AngleEnd);
			var angleMax = MathF.Max(AngleStart, AngleEnd);
			if (_state.Angle > angleMax) {
				_state.Angle = angleMax;
			}

			if (_state.Angle < angleMin) {
				_state.Angle = angleMin;
			}
		}

		public float GetMass()
		{
			//!! also change if wiring of moment of inertia happens (see ctor)
			return 3.0f * Inertia / (FlipperRadius * FlipperRadius);
		}

		public void SetMass(float m)
		{
			//!! also change if wiring of moment of inertia happens (see ctor)
			Inertia = (float) (1.0 / 3.0) * m * (FlipperRadius * FlipperRadius);
		}
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
	}
}
