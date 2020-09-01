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
	public class FlipperMover
	{
		private readonly FlipperData _data;
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

		public FlipperMover(FlipperData data, EventProxy events, Table.Table table)
		{
			_data = data;
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

		public void SetSolenoidState(bool s)
		{
			_solState = s;
		}

		// rigid body functions
		public Vertex3D SurfaceVelocity(Vertex3D surfP)
		{
			return Vertex3D.CrossZ(AngleSpeed, surfP);
		}
	}
}
