using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Timer;

namespace VisualPinball.Engine.Game
{
	public class PlayerPhysics
	{

		public const double SloMo = 1d; // the lower, the slower
		public readonly List<Ball> Balls = new List<Ball>();
		public Vertex3D Gravity = new Vertex3D();
		public long TimeMsec;

		public bool RecordContacts;
		public List<CollisionEvent> Contacts;
		public Ball ActiveBall;
		public Ball ActiveBallBC;
		public bool SwapBallCollisionHandling;
		public float LastPlungerHit = 0;
		public bool BallControl = false;
		public Vertex3D BcTarget;
		public bool IsPaused = false;

		private readonly Table Table;

		//TODO private readonly PinInput pinInput;
		private List<MoverObject> Movers;
		//TODO private readonly FlipperMover flipperMovers[] = [];

		private readonly List<HitObject> HitObjects = new List<HitObject>();
		private readonly List<HitObject> HitObjectsDynamic;
		private HitPlane HitPlayfield; // HitPlanes cannot be part of octree (infinite size)
		private HitPlane HitTopGlass;

		private bool MeshAsPlayfield = false;
		private HitKd HitOcTreeDynamic = new HitKd();
		private HitQuadTree HitOcTree = new HitQuadTree();
		private List<TimerHit> HitTimers;

		private float MinPhysLoopTime = 0;
		private float LastFlipTime = 0;
		private float LastTimeUsec;
		private float LastFrameDuration;
		private float CFrames;

		private float LastFpsTime;
		private float Fps;
		private float FpsAvg;
		private float FpsCount;
		private long CurPhysicsFrameTime;
		private long NextPhysicsFrameTime;
		private long StartTimeUsec;
		private long PhysPeriod;

		private Ball ActiveBallDebug;
		public readonly List<TimerOnOff> ChangedHitTimers = new List<TimerOnOff>();
		private uint ScriptPeriod;

		/// <summary>
		/// Player physics are instantiated in the Player"s constructor.
		/// </summary>
		/// <param name="table"></param>
		public PlayerPhysics(Table table)
		{
			this.Table = table;
			//this.PinInput = pinInput;
		}

		/// <summary>
		/// This is called in the player"s init().
		/// </summary>
		public void Init()
		{
			var minSlope = this.Table.Data.OverridePhysics
				? PhysicsConstants.DefaultTableMinSlope
				: this.Table.Data.AngleTiltMin;
			var maxSlope = this.Table.Data.OverridePhysics
				? PhysicsConstants.DefaultTableMaxSlope
				: this.Table.Data.AngleTiltMax;
			var slope = minSlope + (maxSlope - minSlope) * this.Table.Data.GlobalDifficulty;

			this.Gravity.X = 0;
			this.Gravity.Y = MathF.Sin(MathF.DegToRad(slope)) * (this.Table.Data.OverridePhysics
				? PhysicsConstants.DefaultTableGravity
				: this.Table.Data.Gravity);
			this.Gravity.Z = -MathF.Cos(MathF.DegToRad(slope)) * (this.Table.Data.OverridePhysics
				? PhysicsConstants.DefaultTableGravity
				: this.Table.Data.Gravity);

			// TODO [vpx-js added] init animation timers
			// foreach (var animatable in this.Table.GetAnimatables()) {
			//         animatable.GetAnimation().Init(this.TimeMsec);
			// }

			this.IndexTableElements();
			this.InitOcTree(this.Table);
		}

		private void IndexTableElements()
		{
			// index movables
			Movers = Table.Movables.Select(m => m.GetMover()).ToList();

			// index hittables
			foreach (var hittable in this.Table.Hittables)
			{
				foreach (var hitObject in hittable.GetHitShapes())
				{
					this.HitObjects.Add(hitObject);
					hitObject.CalcHitBBox();
				}
			}

			// TODO index hit timers
			// for (var scriptable of this.Table.GetScriptables()) {
			//         this.HitTimers.Push(...Scriptable.GetApi()._getTimers());
			// }

			// this.HitObjects.AddRange(Table.GetHitShapes()); // these are the table"s outer borders
			// this.HitPlayfield = this.Table.GeneratePlayfieldHit();
			// this.HitTopGlass = this.Table.GenerateGlassHit();

			// TODO index flippers
			//this.FlipperMovers.AddRange(Table.Flippers.Values.Select(f => f.GetMover()));
		}

		private void InitOcTree(Table table)
		{
			foreach (var hitObject in this.HitObjects)
			{
				this.HitOcTree.AddElement(hitObject);
			}

			var tableBounds = table.Data.BoundingBox;
			this.HitOcTree.Initialize(tableBounds);
			// initialize hit structure for dynamic objects
			this.HitOcTreeDynamic.FillFromVector(this.HitObjectsDynamic);
		}

		public void PhysicsSimulateCycle(float dTime)
		{
			var staticCnts = PhysicsConstants.StaticCnts; // maximum number of static counts

			// it"s okay to have this code outside of the inner loop, as the ball hitrects already include the maximum distance they can travel in that timespan
			this.HitOcTreeDynamic.Update();

			while (dTime > 0)
			{
				var hitTime = dTime;

				// TODO find earliest time where a flipper collides with its stop
				// foreach (var flipperMover in this.FlipperMovers)
				// {
				// 	var flipperHitTime = flipperMover.GetHitTime();
				// 	if (flipperHitTime > 0 && flipperHitTime < hitTime)
				// 	{
				// 		//!! >= 0.F causes infinite loop
				// 		hitTime = flipperHitTime;
				// 	}
				// }

				this.RecordContacts = true;
				this.Contacts.Clear();

				foreach (var ball in this.Balls) {
					var ballHit = ball.Hit;

					if (!ball.State.IsFrozen) {
						// don"t play with frozen balls

						ballHit.Coll.HitTime = hitTime; // search upto current hit time
						ballHit.Coll.Clear();

						// always check for playfield and top glass
						if (!this.MeshAsPlayfield) {
							this.HitPlayfield.DoHitTest(ball, ball.Coll, this);
						}

						this.HitTopGlass.DoHitTest(ball, ball.Coll, this);

						// swap order of dynamic and static obj checks randomly
						if (MathF.Random() < 0.5) {
							this.HitOcTreeDynamic.HitTestBall(ball, ball.Coll, this); // dynamic objects
							this.HitOcTree.HitTestBall(ball, ball.Coll, this); // find the hit objects and hit times

						} else {
							this.HitOcTree.HitTestBall(ball, ball.Coll, this); // find the hit objects and hit times
							this.HitOcTreeDynamic.HitTestBall(ball, ball.Coll, this); // dynamic objects
						}

						var htz = ball.Coll.HitTime; // this ball"s hit time

						if (htz < 0) {
							// no negative time allowed
							ball.Coll.Clear();
						}

						if (ball.Coll.Obj != null) {
							///////////////////////////////////////////////////////////////////////////
							if (htz <= hitTime) {
								hitTime = htz; // record actual event time

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

				this.RecordContacts = false;

				// hittime now set ... or full frame if no hit
				// now update displacements to collide-contact or end of physics frame
				// !!!!! 2) move objects to hittime

				if (hitTime > PhysicsConstants.StaticTime) {
					// allow more zeros next round
					staticCnts = PhysicsConstants.StaticCnts;
				}

				foreach (var mover in this.Movers) {
					mover.UpdateDisplacements(hitTime);
				}

				// find balls that need to be collided and script"ed (generally there will be one, but more are possible)
				for (var i = 0; i < this.Balls.Count; i++) {
					var ball = this.Balls[i];
					var pho = ball.Coll.Obj; // object that ball hit in trials

					// find balls with hit objects and minimum time
					if (pho != null && ball.Coll.HitTime <= hitTime) {
						// now collision, contact and script reactions on active ball (object)+++++++++

						this.ActiveBall = ball; // For script that wants the ball doing the collision
						pho.Collide(ball.Coll, this); // !!!!! 3) collision on active ball
						ball.Coll.Clear(); // remove trial hit object pointer

						// Collide may have changed the velocity of the ball,
						// and therefore the bounding box for the next hit cycle
						if (this.Balls[i] != ball) {
							// Ball still exists? may have been deleted from list

							// collision script deleted the ball, back up one count
							--i;

						} else {
							ball.Hit.CalcHitBBox(); // do new boundings
						}
					}
				}

				/*
				 * Now handle contacts.
				 *
				 * At this point UpdateDisplacements() was already called, so the state is different
				 * from that at HitTest(). However, contacts have zero relative velocity, so
				 * hopefully nothing catastrophic has happened in the meanwhile.
				 *
				 * Maybe a two-phase setup where we first process only contacts, then only collisions
				 * could also work.
				 */
				if (MathF.Random() < 0.5) {
					// swap order of contact handling randomly
					// tslint:disable-next-line:prefer-for-of
					foreach (var ce in this.Contacts) {
						ce.Obj.Contact(ce, hitTime, this);
					}

				} else {
					for (var i = this.Contacts.Count - 1; i != -1; --i) {
						this.Contacts[i].Obj.Contact(this.Contacts[i], hitTime, this);
					}
				}

				this.Contacts.Clear();

				// fixme ballspinhack

				dTime -= hitTime;
				this.SwapBallCollisionHandling = !this.SwapBallCollisionHandling; // swap order of ball-ball collisions
			}
		}

		public float UpdatePhysics()
		{
			var initialTimeUsec = NowUsec();

			if (this.IsPaused) {
				// Shift whole game forward in time
				this.StartTimeUsec += initialTimeUsec - this.CurPhysicsFrameTime;
				this.NextPhysicsFrameTime += initialTimeUsec - this.CurPhysicsFrameTime;
				this.CurPhysicsFrameTime = initialTimeUsec; // 0 time frame
			}

			//#ifdef FPS
			this.LastFrameDuration = initialTimeUsec - this.LastTimeUsec;
			if (this.LastFrameDuration > 1000000) {
				this.LastFrameDuration = 0;
			}

			this.LastTimeUsec = initialTimeUsec;

			this.CFrames++;
			if (this.TimeMsec - this.LastFpsTime > 1000) {
				this.Fps = this.CFrames * 1000.0f / (this.TimeMsec - this.LastFpsTime);
				this.LastFpsTime = this.TimeMsec;
				this.FpsAvg += this.Fps;
				this.FpsCount++;
				this.CFrames = 0;
			}
			//#endif

			this.ScriptPeriod = 0;
			var physIterations = 0;

			// loop here until current (real) time matches the physics (simulated) time
			while (this.CurPhysicsFrameTime < initialTimeUsec) {
				// Get time in milliseconds for timers
				this.TimeMsec = (this.CurPhysicsFrameTime - this.StartTimeUsec) / 1000;
				physIterations++;

				// Get the time until the next physics tick is done, and get the time
				// until the next frame is done
				// If the frame is the next thing to happen, update physics to that
				// point next update acceleration, and continue loop
				var physicsDiffTime = (float) ((NextPhysicsFrameTime - CurPhysicsFrameTime) *
				                               (1.0 / PhysicsConstants.DefaultStepTime));

				// one could also do this directly in the while loop condition instead (so that the while loop will really match with the current time), but that leads to some stuttering on some heavy frames
				var curTimeUsec = this.NowUsec();

				// TODO fix code below, breaks the test.
				// hung in the physics loop over 200 milliseconds or the number of physics iterations to catch up on is high (i.E. very low/unplayable FPS)
				// if ((this.Now() - initialTimeUsec > 200000) || (this.PhysIterations > ((this.Table.Data!.PhysicsMaxLoops == 0) || (this.Table.Data!.PhysicsMaxLoops == 0xFFFFFFFF) ? 0xFFFFFFFF : (this.Table.Data!.PhysicsMaxLoops * (10000 / PHYSICS_STEPTIME))))) {
				//      // can not keep up to real time
				//      this.CurPhysicsFrameTime  = initialTimeUsec;                             // skip physics forward ... slip-cycles -> "slowed" down physics
				//      this.NextPhysicsFrameTime = initialTimeUsec + PHYSICS_STEPTIME;
				//      break; // go draw frame
				// }

				// TODO update keys, hid, plumb, nudge, timers, etc
				//this.PinInput.ProcessKeys();

				// do the en/disable changes for the timers that piled up
				foreach (var changedHitTimer in this.ChangedHitTimers) {
					if (changedHitTimer.Enabled) {
						// add the timer?
						if (this.HitTimers.IndexOf(changedHitTimer.Timer) < 0) {
							this.HitTimers.Add(changedHitTimer.Timer);
						}

					} else {
						// delete the timer?
						var idx = this.HitTimers.IndexOf(changedHitTimer.Timer);
						if (idx >= 0) {
							this.HitTimers.RemoveAt(idx);
						}
					}
				}

				this.ChangedHitTimers.Clear();

				var oldActiveBall = this.ActiveBall;
				this.ActiveBall = null; // No ball is the active ball for timers/key events

				if (this.ScriptPeriod <= 1000 * PhysicsConstants.MaxTimersMsecOverall) {
					// if overall script time per frame exceeded, skip
					var timeCur = (this.CurPhysicsFrameTime - this.StartTimeUsec) / 1000; // milliseconds

					foreach (var pht in this.HitTimers) {
						if ((pht.Interval >= 0 && pht.NextFire <= timeCur) || pht.Interval < 0) {
							var curNextFire = pht.NextFire;
							pht.Events.FireGroupEvent(Event.TimerEventsTimer);
							// Only add interval if the next fire time hasn't changed since the event was run.
							if (curNextFire == pht.NextFire) {
								pht.NextFire += pht.Interval;
							}
						}
					}

					this.ScriptPeriod += (uint)(this.NowUsec() - curTimeUsec);
				}

				this.ActiveBall = oldActiveBall;

				// emulator loop
				// if (this.Emu) {
				// 	var deltaTimeMs = physicsDiffTime * 10;
				// 	this.Emu.EmuSimulateCycle(deltaTimeMs);
				// }

				this.UpdateVelocities();

				// primary physics loop
				this.PhysicsSimulateCycle(physicsDiffTime); // main simulator call

				this.CurPhysicsFrameTime = this.NextPhysicsFrameTime; // new cycle, on physics frame boundary
				this.NextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime; // advance physics position
			} // end while (m_curPhysicsFrameTime < initial_time_usec)

			this.PhysPeriod = (this.NowUsec() * 1000) - initialTimeUsec;
			return physIterations;
		}

		public void UpdateVelocities()
		{
			foreach (var mover in this.Movers) {
				// always on integral physics frame boundary (spinner, gate, flipper, plunger, ball)
				mover.UpdateVelocities(this);
			}
		}

		public Ball CreateBall(IBallCreationPosition ballCreator, Player player, float radius = 25f, float mass = 1f)
		{
			var data = new BallData(radius, mass, this.Table.Data.DefaultBulbIntensityScaleOnBall);
			var ballId = Ball.IdCounter++;
			var state = new BallState("Ball${ballId}", ballCreator.GetBallCreationPosition(this.Table));
			state.Pos.Z += data.Radius;

			var ball = new Ball(ballId, data, state, ballCreator.GetBallCreationVelocity(this.Table), player, this.Table);

			ballCreator.OnBallCreated(this, ball);

			this.Balls.Add(ball);
			this.Movers.Add(ball.Mover); // balls are always added separately to this list!

			this.HitObjectsDynamic.Add(ball.Hit);
			this.HitOcTreeDynamic.FillFromVector(this.HitObjectsDynamic);

			return ball;
		}

		public void DestroyBall(Ball ball)
		{
			if (ball == null) {
				return;
			}

			bool activeBall;
			if (this.ActiveBallBC == ball) {
				activeBall = true;
				this.ActiveBall = null;

			} else {
				activeBall = false;
			}

			bool debugBall;
			if (this.ActiveBallDebug == ball) {
				debugBall = true;
				this.ActiveBallDebug = null;

			} else {
				debugBall = false;
			}

			if (this.ActiveBallBC == ball) {
				this.ActiveBallBC = null;
			}

			this.Balls.Remove(ball);
			this.Movers.Remove(ball.Mover);
			this.HitObjectsDynamic.Remove(ball.Hit);
			this.HitOcTreeDynamic.FillFromVector(this.HitObjectsDynamic);

			//m_vballDelete.Push_back(pball);

			if (debugBall && this.Balls.Count > 0) {
				this.ActiveBallDebug = this.Balls[0];
			}

			if (activeBall && this.Balls.Count > 0) {
				this.ActiveBall = this.Balls[0];
			}
		}

		private long NowUsec()
		{
			return (long)(Functions.NowUsec() * SloMo);
		}

		public void SetGravity(float slopeDeg, float strength)
		{
			this.Gravity.X = 0;
			this.Gravity.Y = MathF.Sin(MathF.DegToRad(slopeDeg)) * strength;
			this.Gravity.Z = -MathF.Cos(MathF.DegToRad(slopeDeg)) * strength;
		}
	}

	public interface IBallCreationPosition {

		Vertex3D GetBallCreationPosition(Table table);

		Vertex3D GetBallCreationVelocity(Table table);

		void OnBallCreated(PlayerPhysics physics, Ball ball);
	}
}
