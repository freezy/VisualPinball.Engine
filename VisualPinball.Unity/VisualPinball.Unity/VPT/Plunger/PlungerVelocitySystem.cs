using Unity.Entities;
using Unity.Profiling;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateVelocitiesSystemGroup))]
	public class PlungerVelocitySystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("PlungerVelocitySystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities.ForEach((ref PlungerMovementData movementData, ref PlungerVelocityData velocityData,
				in PlungerStaticData staticData) =>
			{
				marker.Begin();

				// figure our current position in relative coordinates (0.0-1.0,
				// where 0.0 is the maximum forward position and 1.0 is the
				// maximum retracted position)
				var pos = (movementData.Position - staticData.FrameEnd) / staticData.FrameLen;

				// todo | If "mech plunger" is enabled, read the mechanical plunger
				// todo | position; otherwise treat it as fixed at 0.
				const float mech = 0f; //staticData.IsMechPlunger ? MechPlunger() : 0.0f;

				// calculate the delta from the last reading
				var dMech = velocityData.Mech0 - mech;

				// Frame-to-frame mech movement threshold for detecting a release
				// motion.  1.0 is the full range of travel, which corresponds
				// to about 3" on a standard pinball plunger.  We want to choose
				// the value here so that it's faster than the player is likely
				// to move the plunger manually, but slower than the plunger
				// typically moves under spring power when released.  It appears
				// from observation that a real plunger moves at something on the
				// order of 3 m/s.  Figure the fastest USB update interval will
				// be 10ms, typical is probably 25ms, and slowest is maybe 40ms;
				// and figure the bracket speed range down to about 1 m/s.  This
				// gives us a distance per USB interval of from 25mm to 100mm.
				// 25mm translates to .32 of our distance units (0.0-1.0 scale).
				// The lower we make this, the more sensitive we'll be at
				// detecting releases, but if we make it too low we might mistake
				// manual movements for releases.  In practice, it seems safe to
				// lower it to about 0.2 - this doesn't seem to cause false
				// positives and seems reliable at identifying actual releases.
				const float releaseThreshold = 0.2f;

				// note if we're acting as an auto plunger
				var autoPlunger = staticData.IsAutoPlunger;

				// check which forces are acting on us
				if (movementData.FireTimer > 0) {
					// Fire mode.  In this mode, we're moving freely under the spring
					// forces at the speed we calculated when we initiated the release.
					// Simply leave the speed unchanged.
					//
					// Decrement the release mode timer.  The mode ends after the
					// timeout elapses, even if the mech plunger hasn't actually
					// come to rest.  This ensures that we don't get stuck in this
					// mode, and also allows us to sync up again with the real
					// plunger after a respectable pause if the user is just
					// moving it around a lot.
					movementData.Speed = movementData.FireSpeed;
					--movementData.FireTimer;

				} else if (velocityData.AutoFireTimer > 0) {
					// The Auto Fire timer is running.  We start this timer when we
					// send a synthetic KeyDown(Return) event to the script to simulate
					// a Launch Ball event when the user pulls back and releases the
					// mechanical plunger and we're operating as an auto plunger.
					// When the timer reaches zero, we'll send the corresponding
					// KeyUp event and cancel the timer.
					if (--velocityData.AutoFireTimer == 0) {
						// todo event
						// if (g_pplayer != 0) {
						// 	g_pplayer->m_ptable->FireKeyEvent(DISPID_GameEvents_KeyUp, g_pplayer->m_rgKeys[ePlungerKey]);
						// }
					}

				} else if (autoPlunger && dMech > releaseThreshold) {
					// Release motion detected in Auto Plunger mode.
					//
					// If we're acting as an auto plunger, and the player performs
					// a pull-and-release motion on the mechanical plunger, simulate
					// a Launch Ball event.
					//
					// An Auto Plunger simulates a solenoid-driven ball launcher
					// on a table like Medieval Madness.  On this type of game,
					// the original machine doesn't have a spring-loaded plunger.
					// for the user to operate manually.  The user-operated control
					// is instead a button of some kind (the physical form varies
					// quite a bit, from big round pushbuttons to gun triggers to
					// levers to rotating knobs, but they all amount to momentary
					// on/off switches in different guises).  But on virtual
					// cabinets, the mechanical plunger doesn't just magically
					// disappear when you load Medieval Madness!  So the idea here
					// is that we can use a mech plunger to simulate a button.
					// It's pretty simple and natural: you just perform the normal
					// action that you're accustomed to doing with a plunger,
					// namely pulling it back and letting it go.  The software
					// observes this gesture, and rather than trying to simulate
					// the motion directly on the software plunger, we simply
					// turn it into a synthetic Launch Ball keyboard event.  This
					// amounts to sending a KeyDown(Return) message to the script,
					// followed a short time later by a KeyUp(Return).  The script
					// will then act exactly like it would if the user had actually
					// pressed the Return key (or, equivalently on a cabinet, the
					// Launch Ball button).

					// Send a KeyDown(Return) to the table script.  This
					// will allow the script to set ROM switch levels or
					// perform any other tasks it normally does when the
					// actual Launch Ball button is pressed.

					// todo event
					// if (g_pplayer != 0) {
					// 	g_pplayer->m_ptable->FireKeyEvent(DISPID_GameEvents_KeyDown, g_pplayer->m_rgKeys[ePlungerKey]);
					// }

					// start the timer to send the corresponding KeyUp in 100ms
					velocityData.AutoFireTimer = 101;

				} else if (velocityData.PullForce != 0.0f) {
					// A "pull" force is in effect.  This is a *simulated* pull, so
					// it overrides the real physical plunger position.
					//
					// Simply update the model speed by applying the acceleration
					// due to the pull force.
					//
					// Force = mass*acceleration -> a = F/m.  Increase the speed
					// by the acceleration.  Technically we're calculating dv = a dt,
					// but we can elide the elapsed time factor because it's
					// effectively a constant that's implicitly folded into the
					// pull force value.
					movementData.Speed += velocityData.PullForce / Engine.VPT.Plunger.Plunger.PlungerMass;

					if (!velocityData.AddRetractMotion) {
						// this is the normal PullBack branch

						// if we're already at the maximum retracted position, stop
						if (movementData.Position > staticData.FrameStart) {
							movementData.Speed = 0.0f;
							movementData.Position = staticData.FrameStart;
						}

						// if we're already at the minimum retracted position, stop
						if (movementData.Position < staticData.FrameEnd + staticData.RestPosition * staticData.FrameLen) {
							movementData.Speed = 0.0f;
							movementData.Position = staticData.FrameEnd + staticData.RestPosition * staticData.FrameLen;
						}

					} else {
						// this is the PullBackandRetract branch

						// after reaching the max. position the plunger should retract until it reaches the min. position and then start again
						// if we're already at the maximum retracted position, reverse
						if (movementData.Position >= staticData.FrameStart && velocityData.PullForce > 0) {
							movementData.Speed = 0.0f;
							movementData.Position = staticData.FrameStart;
							velocityData.RetractWaitLoop++;
							if (velocityData.RetractWaitLoop > 1000) { // 1 sec, related to PHYSICS_STEPTIME
								velocityData.PullForce = -velocityData.InitialSpeed;
								movementData.Position = staticData.FrameStart;
								movementData.RetractMotion = true;
								velocityData.RetractWaitLoop = 0;
							}
						}

						// if we're already at the minimum retracted position, start again
						if (movementData.Position <= staticData.FrameEnd + staticData.RestPosition * staticData.FrameLen && velocityData.PullForce <= 0) {
							movementData.Speed = 0.0f;
							velocityData.PullForce = velocityData.InitialSpeed;
							movementData.Position = staticData.FrameEnd + staticData.RestPosition * staticData.FrameLen;
						}

						// reset retract motion indicator only after the rest position has been left, to avoid ball interactions
						// use a linear pullback motion
						if (movementData.Position > 1.0f + staticData.FrameEnd + staticData.RestPosition * staticData.FrameLen && velocityData.PullForce > 0) {
							movementData.RetractMotion = false;
							movementData.Speed = 3.0f * velocityData.PullForce; // 3 = magic
						}
					}

				} else if (dMech > releaseThreshold) {
					// Normal mode, fast forward motion detected.  Consider this
					// to be a release event.
					//
					// The release motion of a physical plunger is much faster
					// than our sampling rate can keep up with, so we can't just
					// use the joystick readings directly.  The problem is that a
					// real plunger can shoot all the way forward, bounce all the
					// way back, and shoot forward again in the time between two
					// consecutive samples.  A real plunger moves at around 3-5m/s,
					// which translates to 3-5mm/ms, or 30-50mm per 10ms sampling
					// period.  The whole plunger travel distance is ~65mm.
					// So in one reading, we can travel almost the whole range!
					// This means that samples are effectively random during a
					// release motion.  We might happen to get lucky and have
					// our sample timing align perfectly with a release, so that
					// we get one reading at the retracted position just before
					// a release and the very next reading at the full forward
					// position.  Or we might get unlikely and catch one reading
					// halfway down the initial initial lunge and the next reading
					// at the very apex of the bounce back - and if we took those
					// two readings at face value, we'd be fooled into thinking
					// the plunger was stationary at the halfway point!
					//
					// But there's hope.  A real plunger's barrel spring is pretty
					// inelastic, so the rebounds after a release damp out quickly.
					// Observationally, each bounce bounces back to less than half
					// of the previous one.  So even with the worst-case aliasing,
					// we can be confident that we'll see a declining trend in the
					// samples during a release-bounce-bounce-bounce sequence.
					//
					// Our detection strategy is simply to consider any rapid
					// forward motion to be a release.  If we see the plunger move
					// forward by more than the threshold distance, we'll consider
					// it a release.  See the comments above for how we chose the
					// threshold value.

					// Go back through the recent history to find the apex of the
					// release.  Our "threshold" calculation is basically attempting
					// to measure the instantaneous speed of the plunger as the
					// difference in position divided by the time interval.  But
					// the time interval is extremely imprecise, because joystick
					// reports aren't synchronized to our clock.  In practice the
					// time between USB reports is in the 10-30ms range, which gives
					// us a considerable range of error in calculating an instantaneous
					// speed.
					//
					// So instead of relying on the instantaneous speed alone, now
					// that we're pretty sure a release motion is under way, go back
					// through our recent history to find out where it really
					// started.  Scan the history for monotonically ascending values,
					// and take the highest one we find.  That's probably where the
					// user actually released the plunger.
					var apex = velocityData.Mech0;
					if (velocityData.Mech1 > apex) {
						apex = velocityData.Mech1;
						if (velocityData.Mech2 > apex) {
							apex = velocityData.Mech2;
						}
					}

					// trigger a release from the apex position
					PlungerCommands.Fire(apex, ref velocityData, ref movementData, in staticData);

				} else {
					// Normal mode, and NOT firing the plunger.  In this mode, we
					// simply want to make the on-screen plunger sync up with the
					// position of the physical plunger.
					//
					// This isn't as simple as just setting the software plunger's
					// position to magically match that of the physical plunger.  If
					// we did that, we'd break the simulation by making the software
					// plunger move at infinite speed.  This wouldn't rip the fabric
					// of space-time or anything that dire, but it *would* prevent
					// the collision detection code from working properly.
					//
					// So instead, sync up the positions by setting the software
					// plunger in motion on a course for syncing up with the
					// physical plunger, as fast as we can while maintaining a
					// realistic speed in the simulation.

					// for a normal plunger, sync to the mech plunger; otherwise
					// just go to the rest position
					var target = autoPlunger ? staticData.RestPosition : mech;

					// figure the current difference in positions
					var error = target - pos;

					// Model the software plunger as though it were connected to the
					// mechanical plunger by a spring with spring constant 'mech
					// strength'.  The force from a stretched spring is -kx (spring
					// constant times displacement); in this case, the displacement
					// is the distance between the physical and virtual plunger tip
					// positions ('error').  The force from an acceleration is ma,
					// so the acceleration from the spring force is -kx/m.  Apply
					// this acceleration to the current plunger speed.  While we're
					// at it, apply some damping to the current speed to simulate
					// friction.
					//
					// The 'normalize' factor is the table's normalization constant
					// divided by 1300, for historical reasons.  Old versions applied
					// a 1/13 adjustment factor, which appears to have been empirically
					// chosen to get the speed in the right range.  The m_plungerNormalize
					// factor has default value 100 in this version, so we need to
					// divide it by 100 to get a multipler value.
					//
					// The 'dt' factor represents the amount of time that we're applying
					// this acceleration.  This is in "VP 9 physics frame" units, where
					// 1.0 equals the amount of real time in one VP 9 physics frame.
					// The other normalization factors were originally chosen for VP 9
					// timing, so we need to adjust for the new VP 10 time base.  VP 10
					// runs physics frames at roughly 10x the rate of VP 9, so the time
					// per frame is about 1/10 the VP 9 time.
					const float plungerFriction = 0.95f;
					const float normalize = Engine.VPT.Plunger.Plunger.PlungerNormalize / 13.0f / 100.0f;
					const float dt = 0.1f;
					movementData.Speed *= plungerFriction;
					movementData.Speed += error * staticData.FrameLen * velocityData.MechStrength / Engine.VPT.Plunger.Plunger.PlungerMass * normalize * dt;

					// add any reverse impulse to the result
					movementData.Speed += movementData.ReverseImpulse;
				}

				// cancel any reverse impulse
				movementData.ReverseImpulse = 0.0f;

				// Shift the current mech reading into the history list, if it's
				// different from the last reading.  Only keep distinct readings;
				// the physics loop tends to run faster than the USB reporting
				// rate, so we might see the same USB report several times here.
				if (mech != velocityData.Mech0) {
					velocityData.Mech2 = velocityData.Mech1;
					velocityData.Mech1 = velocityData.Mech0;
					velocityData.Mech0 = mech;
				}

				marker.End();

			}).Run();
		}
	}
}
