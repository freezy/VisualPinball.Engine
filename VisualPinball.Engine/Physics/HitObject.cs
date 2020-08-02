using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Physics
{
	public abstract class HitObject
	{
		public int Id;

		/// <summary>
		/// Base object pointer.
		///
		/// Mainly used as IFireEvents, but also as HitTarget or Primitive or
		/// Trigger or Kicker or Gate.
		/// </summary>
		public EventProxy Obj;                                                 // m_obj

		/// <summary>
		/// Threshold for firing an event (usually (always??) normal dot
		/// ball-velocity)
		/// </summary>
		public float Threshold = 0f;                                           // m_threshold
		public Rect3D HitBBox = new Rect3D(true);                                  // m_hitBBox

		public float Elasticity = 0.3f;                                        // m_elasticity
		public float ElasticityFalloff;                                        // m_elasticityFalloff
		public float Friction = 0.3f;                                          // m_friction

		/// <summary>
		/// Scatter in Radians
		/// </summary>
		public float Scatter;                                                  // m_scatter

		public readonly ItemType ObjType;
		public bool IsEnabled = true;                                          // m_enabled

		/// <summary>
		/// FireEvents for m_obj?
		/// </summary>
		public bool FireEvents = false;                                        // m_fe

		/// <summary>
		/// currently only used to determine which HitTriangles/HitLines/HitPoints
		/// are being part of the same Primitive element m_obj, to be able to early
		/// out intersection traversal if primitive is flagged as not collidable
		/// </summary>
		public bool E = false;                                                 // m_e

		public int ItemIndex;
		public int ItemVersion;

		protected HitObject(ItemType objType)
		{
			ObjType = objType;
		}

		public abstract void CalcHitBBox();

		public abstract float HitTest(Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics);

		public abstract void Collide(CollisionEvent coll, PlayerPhysics physics);

		protected const float HardScatter = 0.0f;

		/// <summary>
		/// Apply contact forces for the given time interval. Ball, Spinner and
		/// Gate do nothing here, Flipper has a specialized handling
		/// </summary>
		/// <param name="coll"></param>
		/// <param name="dTime"></param>
		/// <param name="physics"></param>
		public virtual void Contact(CollisionEvent coll, float dTime, PlayerPhysics physics)
		{
			coll.Ball.Hit.HandleStaticContact(coll, Friction, dTime, physics);
		}

		public HitObject SetFriction(float friction)
		{
			Friction = friction;
			return this;
		}

		public HitObject SetScatter(float scatter)
		{
			Scatter = scatter;
			return this;
		}

		public void FireHitEvent(Ball ball)
		{
			if (Obj != null && FireEvents && IsEnabled)
			{
				// is this the same place as last event? if same then ignore it
				var posDiff = ball.Hit.EventPos.Clone().Sub(ball.State.Pos);
				var distLs = posDiff.LengthSq();

				// remember last collide position
				ball.Hit.EventPos.Set(ball.State.Pos.X, ball.State.Pos.Y, ball.State.Pos.Z);

				// hit targets when used with a captured ball have always a too small distance
				var normalDist = ObjType == ItemType.HitTarget ? 0.0f : 0.25f; // magic distance

				if (distLs > normalDist) {
					// must be a new place if only by a little
					Obj.FireGroupEvent(EventId.HitEventsHit);
				}
			}
		}

		public HitObject SetElasticity(float elasticity)
		{
			Elasticity = elasticity;
			return this;
		}

		public HitObject SetElasticity(float elasticity, float elasticityFalloff)
		{
			Elasticity = elasticity;
			ElasticityFalloff = elasticityFalloff;
			return this;
		}

		public HitObject SetZ(float zLow, float zHigh)
		{
			HitBBox.ZLow = zLow;
			HitBBox.ZHigh = zHigh;
			return this;
		}

		public void SetEnabled(bool isEnabled)
		{
			IsEnabled = isEnabled;
		}

		public void DoHitTest(Ball ball, CollisionEvent coll, PlayerPhysics physics)
		{
			if (ball == null) {
				return;
			}

			if (Obj?.AbortHitTest != null && Obj.AbortHitTest()) {
				return;
			}

			var newColl = new CollisionEvent(ball);
			var newTime = HitTest(ball, coll.HitTime, !physics.RecordContacts ? coll : newColl, physics);
			var validHit = newTime >= 0 && newTime <= coll.HitTime;

			if (!physics.RecordContacts) {
				// simply find first event
				if (validHit) {
					coll.Ball = ball;
					coll.Obj = this;
					coll.HitTime = newTime;
				}

			} else {
				// find first collision, but also remember all contacts
				if (newColl.IsContact || validHit) {
					newColl.Ball = ball;
					newColl.Obj = this;

					if (newColl.IsContact) {
						physics.Contacts.Add(newColl);

					} else {
						// if (validhit)
						coll.Set(newColl);
						coll.HitTime = newTime;
					}

				}
			}
		}

		public void ApplyPhysics(IPhysicalData data, Table table)
		{
			var mat = table.GetMaterial(data.GetPhysicsMaterial());
			if (mat != null && !data.GetOverwritePhysics()) {
				SetElasticity(mat.Elasticity, mat.ElasticityFalloff);
				SetFriction(mat.Friction);
				SetScatter(MathF.DegToRad(mat.ScatterAngle));

			} else {
				SetElasticity(data.GetElasticity(), data.GetElasticityFalloff());
				SetFriction(data.GetFriction());
				SetScatter(MathF.DegToRad(data.GetScatter()));
			}

			SetEnabled(data.GetIsCollidable());
		}

		public virtual void SetIndex(int index, int version)
		{
			ItemIndex = index;
			ItemVersion = version;
		}
	}
}
