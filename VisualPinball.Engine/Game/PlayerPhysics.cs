#region ReSharper
// ReSharper disable CommentTypo
// ReSharper disable MemberCanBePrivate.Global
#endregion

using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Timer;

namespace VisualPinball.Engine.Game
{
	public class PlayerPhysics
	{
		/// <summary>
		/// The lower, the slower
		/// </summary>
		private const double SlowMotion = 1d;

		public readonly List<Ball> Balls = new List<Ball>();                             // m_vball
		public readonly Vertex3D Gravity = new Vertex3D();                               // m_gravity
		public uint TimeMsec;                                                            // m_time_msec

		/// <summary>
		/// Flag for DoHitTest
		/// </summary>
		public bool RecordContacts;                                                      // m_recordContacts
		public readonly List<CollisionEvent> Contacts = new List<CollisionEvent>();      // m_contacts
		public Ball ActiveBall;                                                          // m_pactiveball

		/// <summary>
		/// Swaps the order of ball-ball collision handling around each physics
		/// cycle (in regard to the RLC comment block in quadtree)
		/// </summary>
		public bool SwapBallCollisionHandling;                                           // m_swap_ball_collision_handling

		/// <summary>
		/// Pauses and unpauses the physics loop
		/// </summary>
		public bool IsPaused = false;
		public readonly List<TimerOnOff> ChangedHitTimers = new List<TimerOnOff>();      // m_changed_vht

		private readonly Table _table;

		//TODO private readonly PinInput pinInput;
		private List<IMoverObject> _movers;                                              // m_vmover
		private FlipperMover[] _flipperMovers;

		private readonly List<HitObject> _hitObjects = new List<HitObject>();            // m_vho
		private readonly List<HitObject> _hitObjectsDynamic = new List<HitObject>();     // m_vho_dynamic

		/// <summary>
		/// HitPlanes cannot be part of octree (infinite size)
		/// </summary>
		private HitPlane _hitPlayfield;                                        // m_hitPlayfield
		private HitPlane _hitTopGlass;                                         // m_hitTopGlass

		private readonly HitKd _hitOcTreeDynamic = new HitKd();                // m_hitoctree_dynamic
		private HitQuadTree _hitOcTree;                                        // m_hitoctree
		private readonly List<TimerHit> _hitTimers = new List<TimerHit>();     // m_vht

		private long _lastTimeUsec;                                            // m_lastTime_usec
		private uint _lastFrameDuration;                                       // m_lastFrameDuration
		private uint _cFrames;                                                 // m_cframes
		private uint _scriptPeriod;                                            // m_script_period

		private long _startTimeUsec;                                           // m_StartTime_usec

		/// <summary>
		/// Time when the last frame was drawn
		/// </summary>
		private long _curPhysicsFrameTime;                                     // m_curPhysicsFrameTime

		/// <summary>
		/// time at which the next physics update should be
		/// </summary>
		private long _nextPhysicsFrameTime;                                    // m_nextPhysicsFrameTime

		private uint _lastFpsTime;                                             // m_lastfpstime
		private float _fps;                                                    // m_fps
		private float _fpsAvg;                                                 // m_fpsAvg
		private uint _fpsCount;                                                // m_fpsCount

		/// <summary>
		/// Player physics are instantiated in the Player"s constructor.
		/// </summary>
		/// <param name="table"></param>
		public PlayerPhysics(Table table)
		{
			_table = table;
			//this.PinInput = pinInput;
		}

		/// <summary>
		/// This is called in the player"s init().
		/// </summary>
		public void Init()
		{
			var minSlope = _table.Data.OverridePhysics != 0 ? PhysicsConstants.DefaultTableMinSlope : _table.Data.AngleTiltMin;
			var maxSlope = _table.Data.OverridePhysics != 0 ? PhysicsConstants.DefaultTableMaxSlope : _table.Data.AngleTiltMax;
			var slope = minSlope + (maxSlope - minSlope) * _table.Data.GlobalDifficulty;

			Gravity.X = 0;
			Gravity.Y = MathF.Sin(MathF.DegToRad(slope)) * (_table.Data.OverridePhysics != 0 ? PhysicsConstants.DefaultTableGravity : _table.Data.Gravity);
			Gravity.Z = -MathF.Cos(MathF.DegToRad(slope)) * (_table.Data.OverridePhysics != 0 ? PhysicsConstants.DefaultTableGravity : _table.Data.Gravity);

			// TODO [vpx-js added] init animation timers
			// foreach (var animatable in this.Table.GetAnimatables()) {
			//         animatable.GetAnimation().Init(this.TimeMsec);
			// }

			IndexTableElements();
			InitOcTree(_table);
		}

		private void IndexTableElements()
		{
			// index movables
			_movers = _table.Movables.Select(m => m.GetMover()).ToList();

			// index hittables
			foreach (var hittable in _table.Hittables) {
				foreach (var hitObject in hittable.GetHitShapes()) {
					_hitObjects.Add(hitObject);
					hitObject.CalcHitBBox();
				}
			}

			// TODO index hit timers
			// for (var scriptable of this.Table.GetScriptables()) {
			//         this.HitTimers.Push(...Scriptable.GetApi()._getTimers());
			// }

			_hitObjects.AddRange(_table.GetHitShapes()); // these are the table's outer borders
			_hitPlayfield = _table.GeneratePlayfieldHit();
			_hitTopGlass = _table.GenerateGlassHit();

			// index flippers
			_flipperMovers = _table.GetAll<Flipper>().Select(f => f.FlipperMover).ToArray();
		}

		private void InitOcTree(Table table)
		{
			_hitOcTree = new HitQuadTree(_hitObjects, table.Data.BoundingBox);

			// initialize hit structure for dynamic objects
			_hitOcTreeDynamic.FillFromVector(_hitObjectsDynamic);
		}

		public void PhysicsSimulateCycle(float dTime)
		{
			// maximum number of static counts
			var staticCnts = PhysicsConstants.StaticCnts;

			// it's okay to have this code outside of the inner loop, as the
			// ball hitrects already include the maximum distance they can
			// travel in that timespan
			_hitOcTreeDynamic.Update();

			while (dTime > 0) {
				var hitTime = dTime;

				// find earliest time where a flipper collides with its stop
				foreach (var flipperMover in _flipperMovers) {
					var flipperHitTime = flipperMover.GetHitTime();
					if (flipperHitTime > 0 && flipperHitTime < hitTime) {
						//!! >= 0.F causes infinite loop
						hitTime = flipperHitTime;
					}
				}

				RecordContacts = true;
				Contacts.Clear();

				foreach (var ball in Balls) {
					var ballHit = ball.Hit;

					// don't play with frozen balls
					if (!ball.State.IsFrozen) {

						// search upto current hit time
						ballHit.Coll.HitTime = hitTime;
						ballHit.Coll.Clear();

						// always check for playfield and top glass
						if (!_table.HasMeshAsPlayfield) {
							_hitPlayfield.DoHitTest(ball, ball.Coll, this);
						}

						_hitTopGlass.DoHitTest(ball, ball.Coll, this);

						// swap order of dynamic and static obj checks randomly
						if (MathF.Random() < 0.5) {
							_hitOcTreeDynamic.HitTestBall(ball, ball.Coll, this); // dynamic objects
							_hitOcTree.HitTestBall(ball, ball.Coll, this);        // find the hit objects and hit times

						} else {
							_hitOcTree.HitTestBall(ball, ball.Coll, this);        // find the hit objects and hit times
							_hitOcTreeDynamic.HitTestBall(ball, ball.Coll, this); // dynamic objects
						}

						// this ball's hit time
						var htz = ball.Coll.HitTime;

						if (htz < 0) {
							// no negative time allowed
							ball.Coll.Clear();
						}

						if (ball.Coll.HasHit) {
							// smaller hit time?
							if (htz <= hitTime) {
								// record actual event time
								hitTime = htz;

								// less than static time interval
								if (htz < PhysicsConstants.StaticTime) {

									if (--staticCnts < 0) {
										staticCnts = 0; // keep from wrapping
										hitTime = PhysicsConstants.StaticTime;
									}
								}
							}
						}
					}
				} // end loop over all balls

				RecordContacts = false;

				// hit time now set... or full frame if no hit
				// now update displacements to collide-contact or end of physics frame

				if (hitTime > PhysicsConstants.StaticTime) {
					// allow more zeros next round
					staticCnts = PhysicsConstants.StaticCnts;
				}

				foreach (var mover in _movers) {
					// step 2: move the objects about according to velocities
					// (spinner, gate, flipper, plunger, ball)
					mover.UpdateDisplacements(hitTime);
				}

				// find balls that need to be collided and scripted (generally
				// there will be one, but more are possible)
				for (var i = 0; i < Balls.Count; i++) {
					var ball = Balls[i];
					var hitObject = ball.Coll.Obj; // object that ball hit in trials

					// find balls with hit objects and minimum time
					if (hitObject != null && ball.Coll.HitTime <= hitTime) {

						// now collision, contact and script reactions on active ball (object)
						ActiveBall = ball;                           // For script that wants the ball doing the collision
						hitObject.Collide(ball.Coll, this);   // collision on active ball
						ball.Coll.Clear();                           // remove trial hit object pointer

						// Collide may have changed the velocity of the ball,
						// and therefore the bounding box for the next hit cycle
						if (Balls[i] != ball) {                      // Ball still exists? may have been deleted from list

							// collision script deleted the ball, back up one count
							--i;

						} else {
							ball.Hit.CalcHitBBox(); // do new boundings
						}
					}
				}

				// Now handle contacts.
				//
				// At this point UpdateDisplacements() was already called, so the state is different
				// from that at HitTest(). However, contacts have zero relative velocity, so
				// hopefully nothing catastrophic has happened in the meanwhile.
				//
				// Maybe a two-phase setup where we first process only contacts, then only collisions
				// could also work.
				if (MathF.Random() < 0.5) {  // swap order of contact handling randomly
					foreach (var ce in Contacts) {
						ce.Obj.Contact(ce, hitTime, this);
					}

				} else {
					for (var i = Contacts.Count - 1; i != -1; --i) {
						Contacts[i].Obj.Contact(Contacts[i], hitTime, this);
					}
				}

				Contacts.Clear();

				// TODO C_BALL_SPIN_HACK

				// new delta .. i.e. time remaining
				dTime -= hitTime;

				// swap order of ball-ball collisions
				SwapBallCollisionHandling = !SwapBallCollisionHandling;
			}
		}

		public void UpdatePhysics()
		{
			UpdatePhysics(NowUsec());
		}

		public void UpdatePhysics(long initialTimeUsec)
		{
			if (IsPaused) {
				// Shift whole game forward in time
				_startTimeUsec += initialTimeUsec - _curPhysicsFrameTime;
				_nextPhysicsFrameTime += initialTimeUsec - _curPhysicsFrameTime;
				_curPhysicsFrameTime = initialTimeUsec; // 0 time frame
			}

			//#ifdef FPS
			_lastFrameDuration = (uint)(initialTimeUsec - _lastTimeUsec);
			if (_lastFrameDuration > 1000000) {
				_lastFrameDuration = 0;
			}
			_lastTimeUsec = initialTimeUsec;

			_cFrames++;
			if (TimeMsec - _lastFpsTime > 1000) {
				_fps = (float)(_cFrames * 1000.0 / (TimeMsec - _lastFpsTime));
				_lastFpsTime = TimeMsec;
				_fpsAvg += _fps;
				_fpsCount++;
				_cFrames = 0;
			}
			//#endif

			_scriptPeriod = 0;
			var physIterations = 0u;

			// loop here until current (real) time matches the physics (simulated) time
			while (_curPhysicsFrameTime < initialTimeUsec) {
				// Get time in milliseconds for timers
				TimeMsec = (uint)((_curPhysicsFrameTime - _startTimeUsec) / 1000);
				physIterations++;

				// Get the time until the next physics tick is done, and get the time
				// until the next frame is done
				// If the frame is the next thing to happen, update physics to that
				// point next update acceleration, and continue loop
				var physicsDiffTime = (float) ((_nextPhysicsFrameTime - _curPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime));

				// one could also do this directly in the while loop condition instead (so that the while loop will really match with the current time), but that leads to some stuttering on some heavy frames
				var curTimeUsec = NowUsec();

				// TODO fix code below, breaks the test.
				// hung in the physics loop over 200 milliseconds or the number of physics iterations to catch up on is high (i.E. very low/unplayable FPS)
				// if (NowUsec() - initialTimeUsec > 200000) || (this.PhysIterations > (Table.Data.PhysicsMaxLoops == 0 || (this.Table.Data!.PhysicsMaxLoops == 0xFFFFFFFF) ? 0xFFFFFFFF : (this.Table.Data!.PhysicsMaxLoops * (10000 / PHYSICS_STEPTIME))))) {
				//      // can not keep up to real time
				//      this.CurPhysicsFrameTime  = initialTimeUsec;                             // skip physics forward ... slip-cycles -> "slowed" down physics
				//      this.NextPhysicsFrameTime = initialTimeUsec + PHYSICS_STEPTIME;
				//      break; // go draw frame
				// }

				// TODO update keys, hid, plumb, nudge, timers, etc
				//this.PinInput.ProcessKeys();

				// do the en/disable changes for the timers that piled up
				foreach (var hitTimer in ChangedHitTimers) {
					if (hitTimer.Enabled) {
						// add the timer?
						if (!_hitTimers.Contains(hitTimer.Timer)) {
							_hitTimers.Add(hitTimer.Timer);
						}

					} else {
						// delete the timer?
						if (_hitTimers.Contains(hitTimer.Timer)) {
							_hitTimers.Remove(hitTimer.Timer);
						}
					}
				}
				ChangedHitTimers.Clear();

				var oldActiveBall = ActiveBall;
				ActiveBall = null; // No ball is the active ball for timers/key events

				// if overall script time per frame exceeded, skip
				if (_scriptPeriod <= 1000 * PhysicsConstants.MaxTimersMsecOverall) {

					var timeCur = (uint)((_curPhysicsFrameTime - _startTimeUsec) / 1000); // milliseconds
					foreach (var hitTimer in _hitTimers) {
						if (hitTimer.Interval >= 0 && hitTimer.NextFire <= timeCur || hitTimer.Interval < 0) {
							var curNextFire = hitTimer.NextFire;
							hitTimer.Events.FireGroupEvent(EventId.TimerEventsTimer);
							// Only add interval if the next fire time hasn't changed since the event was run.
							if (curNextFire == hitTimer.NextFire) {
								hitTimer.NextFire += (uint)hitTimer.Interval;
							}
						}
					}

					_scriptPeriod += (uint)(NowUsec() - curTimeUsec);
				}

				ActiveBall = oldActiveBall;

				// todo NudgeUpdate, MechPlungerUpdate

				UpdateVelocities();

				// primary physics loop
				PhysicsSimulateCycle(physicsDiffTime);                         // main simulator call

				_curPhysicsFrameTime = _nextPhysicsFrameTime;                  // new cycle, on physics frame boundary
				_nextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;     // advance physics position
			} // end while (m_curPhysicsFrameTime < initial_time_usec)
		}

		public void UpdateVelocities()
		{
			foreach (var mover in _movers) {
				// always on integral physics frame boundary (spinner, gate, flipper, plunger, ball)
				mover.UpdateVelocities(this);
			}
		}

		public Ball CreateBall(IBallCreationPosition ballCreator, Player player, float radius = 25f, float mass = 1f)
		{
			var ballId = Ball.IdCounter++;
			var data = new BallData(ballId, radius, mass, _table.Data.DefaultBulbIntensityScaleOnBall);
			var state = new BallState("Ball${ballId}", ballCreator.GetBallCreationPosition(_table));
			state.Pos.Z += data.Radius;

			var ball = new Ball(data, state, ballCreator.GetBallCreationVelocity(_table), player, _table);

			ballCreator.OnBallCreated(this, ball);

			Balls.Add(ball);
			_movers.Add(ball.Mover); // balls are always added separately to this list!

			_hitObjectsDynamic.Add(ball.Hit);
			_hitOcTreeDynamic.FillFromVector(_hitObjectsDynamic);

			return ball;
		}

		public void DestroyBall(Ball ball)
		{
			if (ball == null) {
				return;
			}

			Balls.Remove(ball);
			_movers.Remove(ball.Mover);
			_hitObjectsDynamic.Remove(ball.Hit);
			_hitOcTreeDynamic.FillFromVector(_hitObjectsDynamic);
		}

		private static long NowUsec()
		{
			return (long)(Functions.NowUsec() * SlowMotion);
		}

		public void SetGravity(float slopeDeg, float strength)
		{
			Gravity.X = 0;
			Gravity.Y = MathF.Sin(MathF.DegToRad(slopeDeg)) * strength;
			Gravity.Z = -MathF.Cos(MathF.DegToRad(slopeDeg)) * strength;
		}
	}
}
