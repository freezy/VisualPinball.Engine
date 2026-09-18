// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Section 6.2 of the static rebound plan: the slingshot's switch zones, its two
	/// coil sources and their ordering, the kick rule and the fault cases.
	/// </summary>
	/// <remarks>
	/// The whole tick contract is exercised through the real state map, the real tick
	/// hook and the real impact entry point, so nothing here re-implements the rules.
	/// </remarks>
	public class RubberSlingshotLogicTests
	{
		private const int SlingshotItemId = 900;
		private const int SpanId = 0;
		private const int BallId = 7;
		private const ulong Millisecond = 1000;

		private const float SpanLength = (float)RubberSpanReboundFixture.LengthVpx;
		private const float CordRadius = 4f;
		private const float BallRadius = 25f;

		#region Switch zones

		[Test]
		public void CentreQuarterAndNearEndHitsShouldCloseZonesButRigidEndsShouldNot()
		{
			foreach (var (name, apex, depth, expected) in new[] {
				("centre", 0.5f, 5f, true),
				("quarter", 0.25f, 5f, true),
				("near end", 0.1f, 5f, true),
				("rigid end", 0.5f, 0f, false),
				("too shallow", 0.5f, 0.5f, false),
			}) {
				using var harness = new PhysicsStateHarness();
				harness.RubberSlingshotStates[SlingshotItemId] = State();
				var state = harness.CreateState();
				state.TickTimeUsec = 100 * Millisecond;

				Impact(harness, ref state, apex, depth);

				Assert.That(harness.RubberSlingshotStates[SlingshotItemId].SwitchLevel != 0,
					Is.EqualTo(expected), name);
				var events = Drain(harness);
				Assert.That(events.Count, Is.EqualTo(expected ? 1 : 0), name);
				if (expected) {
					Assert.That(events[0].EventId, Is.EqualTo(EventId.SwitchEventsClosed), name);
					Assert.That(events[0].BallId, Is.EqualTo(BallId), name);
					Assert.That(events[0].FloatParam, Is.EqualTo(100f).Within(1e-3f), name);
				}
			}
		}

		[Test]
		public void TheDeflectionProfileShouldBeTheVOfTheEffectiveDepth()
		{
			Assert.That(RubberSlingshotPhysics.Deflection(6f, 0.5f, 0.5f), Is.EqualTo(6f));
			Assert.That(RubberSlingshotPhysics.Deflection(6f, 0.5f, 0.25f), Is.EqualTo(3f));
			Assert.That(RubberSlingshotPhysics.Deflection(6f, 0.5f, 0.75f), Is.EqualTo(3f));
			Assert.That(RubberSlingshotPhysics.Deflection(6f, 0.25f, 0.75f),
				Is.EqualTo(6f * 0.25f / 0.75f).Within(1e-5f));
			Assert.That(RubberSlingshotPhysics.Deflection(6f, 0.5f, 0f), Is.Zero);
			Assert.That(RubberSlingshotPhysics.Deflection(6f, 0.5f, 1f), Is.Zero);
		}

		[Test]
		public void ZonesShouldDwellThenOpenAndOnlyEdgesShouldBeEnqueued()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSlingshotStates[SlingshotItemId] = State();
			var state = harness.CreateState();

			// a centre hit closes both zones for their eight millisecond dwell
			state.TickTimeUsec = 10 * Millisecond;
			Impact(harness, ref state, 0.5f, 5f);
			Assert.That(Kinds(Drain(harness)), Is.EqualTo(new[] { EventId.SwitchEventsClosed }));

			// a second hit inside the dwell does not enqueue another edge
			state.TickTimeUsec = 12 * Millisecond;
			Impact(harness, ref state, 0.5f, 5f);
			Assert.That(Drain(harness), Is.Empty);

			// still closed one tick before the extended dwell ends
			Run(harness, ref state, 13 * Millisecond, 19 * Millisecond);
			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].SwitchLevel, Is.Not.Zero);

			// the dwell of the second hit ends at 20 ms
			Run(harness, ref state, 20 * Millisecond, 20 * Millisecond);
			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].SwitchLevel, Is.Zero);
			Assert.That(Kinds(Drain(harness)),
				Does.Contain(EventId.SwitchEventsOpened));

			// and stays open without further edges
			Run(harness, ref state, 21 * Millisecond, 40 * Millisecond);
			foreach (var e in Drain(harness)) {
				Assert.That(e.EventId, Is.Not.EqualTo(EventId.SwitchEventsOpened));
			}
		}

		[Test]
		public void TheLogicalSwitchShouldBeTheOrOfItsZones()
		{
			using var harness = new PhysicsStateHarness();
			var slingshot = State();
			// only the first zone can ever close: the second needs an impossible depth
			RubberSlingshotState.Zone(ref slingshot, 1).CloseDeflection = 1000f;
			// and give the zones different dwells so the OR is visible
			RubberSlingshotState.Zone(ref slingshot, 0).DwellUsec = 5 * Millisecond;
			harness.RubberSlingshotStates[SlingshotItemId] = slingshot;
			var state = harness.CreateState();

			state.TickTimeUsec = 0;
			Impact(harness, ref state, 0.3f, 5f);
			var closed = harness.RubberSlingshotStates[SlingshotItemId];
			Assert.That(closed.Zone0.IsClosed, Is.Not.Zero);
			Assert.That(closed.Zone1.IsClosed, Is.Zero);
			Assert.That(closed.SwitchLevel, Is.Not.Zero, "one closed zone closes the switch");

			Run(harness, ref state, Millisecond, 5 * Millisecond);
			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].SwitchLevel, Is.Zero);
		}

		#endregion

		#region Pulse delivery

		[Test]
		public void ACloseAndOpenInOneFrameShouldReachTheEngineAsAMinimumPulse()
		{
			var timer = new SlingshotSwitchPulseTimer(0.03f);

			Assert.That(timer.OnClose(1.000f), Is.True, "the close is delivered at once");
			Assert.That(timer.OnOpen(1.001f), Is.False, "the open is held back");
			Assert.That(timer.IsOpenPending, Is.True);
			Assert.That(timer.Poll(1.020f), Is.False, "still inside the window");
			Assert.That(timer.Poll(1.030f), Is.True, "released exactly at the minimum");
			Assert.That(timer.IsEngineClosed, Is.False);
			Assert.That(timer.Poll(1.100f), Is.False, "released only once");
		}

		[Test]
		public void ASecondCloseInsideTheWindowShouldExtendTheDeliveredPulse()
		{
			var timer = new SlingshotSwitchPulseTimer(0.03f);
			timer.OnClose(1.000f);
			timer.OnOpen(1.005f);

			Assert.That(timer.OnClose(1.010f), Is.False,
				"the engine already sees the switch closed");
			Assert.That(timer.IsOpenPending, Is.False);
			Assert.That(timer.Poll(1.035f), Is.False, "the window restarted at 1.010");

			Assert.That(timer.OnOpen(1.020f), Is.False);
			Assert.That(timer.Poll(1.039f), Is.False);
			Assert.That(timer.Poll(1.040f), Is.True);
		}

		[Test]
		public void AnOpenBeyondTheWindowShouldBeDeliveredImmediately()
		{
			var timer = new SlingshotSwitchPulseTimer(0.03f);
			timer.OnClose(1.000f);

			Assert.That(timer.OnOpen(1.050f), Is.True);
			Assert.That(timer.IsOpenPending, Is.False);
			Assert.That(timer.IsEngineClosed, Is.False);
			Assert.That(timer.OnOpen(1.060f), Is.False, "an open without a close does nothing");
		}

		#endregion

		#region Sources and edges

		[Test]
		public void ASelfWirePulseShouldRiseAfterTheWireDelayAndFallAfterItsLength()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSlingshotStates[SlingshotItemId] = State();
			var state = harness.CreateState();

			state.TickTimeUsec = 0;
			Impact(harness, ref state, 0.5f, 5f);
			Drain(harness);

			// nothing until the wire delay of eight milliseconds elapsed
			Run(harness, ref state, Millisecond, 7 * Millisecond);
			Assert.That(Drain(harness), Is.Empty);
			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].CoilLevel, Is.Zero);

			Run(harness, ref state, 8 * Millisecond, 8 * Millisecond);
			Assert.That(Kinds(Drain(harness)),
				Is.EqualTo(new[] { EventId.SwitchEventsOpened, EventId.CoilEventsEnergized }));
			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].CoilLevel, Is.Not.Zero);
			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].KickCount, Is.EqualTo(1));

			// and falls twenty milliseconds later
			Run(harness, ref state, 9 * Millisecond, 27 * Millisecond);
			Assert.That(Drain(harness), Is.Empty);
			Run(harness, ref state, 28 * Millisecond, 28 * Millisecond);
			Assert.That(Kinds(Drain(harness)),
				Is.EqualTo(new[] { EventId.CoilEventsReleased }));
			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].PulseCount, Is.Zero);
		}

		[Test]
		public void APulseWhoseRiseAndFallWereBothSkippedShouldKickThenFallInOneTick()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSlingshotStates[SlingshotItemId] = State();
			var state = harness.CreateState();

			state.TickTimeUsec = 0;
			Impact(harness, ref state, 0.5f, 5f);
			Drain(harness);

			// the engine skipped past the whole pulse
			Run(harness, ref state, 100 * Millisecond, 100 * Millisecond);

			Assert.That(Kinds(Drain(harness)), Is.EqualTo(new[] {
				EventId.SwitchEventsOpened,
				EventId.CoilEventsEnergized,
				EventId.CoilEventsReleased,
			}));
			var slingshot = harness.RubberSlingshotStates[SlingshotItemId];
			Assert.That(slingshot.KickCount, Is.EqualTo(1));
			Assert.That(slingshot.CoilLevel, Is.Zero);
			Assert.That(slingshot.PulseCount, Is.Zero);
		}

		[Test]
		public void AFallShouldBeAppliedBeforeARiseOfTheSameTime()
		{
			using var harness = new PhysicsStateHarness();
			var slingshot = State(selfWired: false);
			slingshot.PulseCount = 2;
			slingshot.SelfWireLevel = 1;
			slingshot.CoilLevel = 1;
			slingshot.Pulse0 = new RubberSlingshotPulse {
				RiseUsec = 0, FallUsec = 10 * Millisecond, Risen = 1,
			};
			slingshot.Pulse1 = new RubberSlingshotPulse {
				RiseUsec = 10 * Millisecond, FallUsec = 30 * Millisecond, Risen = 0,
			};
			harness.RubberSlingshotStates[SlingshotItemId] = slingshot;
			var state = harness.CreateState();

			Run(harness, ref state, 10 * Millisecond, 10 * Millisecond);

			// the fall first, so the re-armed pulse produces a fresh rising edge
			Assert.That(Kinds(Drain(harness)), Is.EqualTo(new[] {
				EventId.CoilEventsReleased,
				EventId.CoilEventsEnergized,
			}));
			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].KickCount, Is.EqualTo(1));
		}

		[Test]
		public void RepeatedEnablesShouldNotRetrigger()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSlingshotStates[SlingshotItemId] = State(selfWired: false);
			var state = harness.CreateState();

			Command(harness, true, 0);
			Command(harness, true, 0);
			Command(harness, true, 0);
			Run(harness, ref state, 0, 0);

			Assert.That(Kinds(Drain(harness)),
				Is.EqualTo(new[] { EventId.CoilEventsEnergized }));
			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].KickCount, Is.EqualTo(1));
			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].CommandCount, Is.Zero);
		}

		[Test]
		public void CommandedOnOffAndOnOffOnShouldProduceEveryEdge()
		{
			foreach (var selfWireHigh in new[] { false, true }) {
				using var harness = new PhysicsStateHarness();
				var slingshot = State(selfWired: false);
				if (selfWireHigh) {
					slingshot.SelfWireLevel = 1;
					slingshot.CoilLevel = 1;
				}
				harness.RubberSlingshotStates[SlingshotItemId] = slingshot;
				var state = harness.CreateState();

				Command(harness, true, 0);
				Command(harness, false, 0);
				Run(harness, ref state, 0, 0);
				var onOff = Kinds(Drain(harness));

				Command(harness, true, Millisecond);
				Command(harness, false, Millisecond);
				Command(harness, true, Millisecond);
				Run(harness, ref state, Millisecond, Millisecond);
				var onOffOn = Kinds(Drain(harness));

				if (selfWireHigh) {
					// the self wire holds the coil, so no aggregate edge ever happens
					Assert.That(onOff, Is.Empty);
					Assert.That(onOffOn, Is.Empty);
					Assert.That(harness.RubberSlingshotStates[SlingshotItemId].CoilLevel,
						Is.Not.Zero);
				} else {
					Assert.That(onOff, Is.EqualTo(new[] {
						EventId.CoilEventsEnergized, EventId.CoilEventsReleased,
					}));
					Assert.That(onOffOn, Is.EqualTo(new[] {
						EventId.CoilEventsEnergized,
						EventId.CoilEventsReleased,
						EventId.CoilEventsEnergized,
					}));
				}
			}
		}

		[Test]
		public void ASelfWireFallWhileTheCommandIsHighShouldNotReleaseTheCoil()
		{
			using var harness = new PhysicsStateHarness();
			var slingshot = State();
			slingshot.SelfWireLevel = 1;
			slingshot.CoilLevel = 1;
			slingshot.CommandedLevel = 1;
			slingshot.PulseCount = 1;
			slingshot.Pulse0 = new RubberSlingshotPulse {
				RiseUsec = 0, FallUsec = 5 * Millisecond, Risen = 1,
			};
			harness.RubberSlingshotStates[SlingshotItemId] = slingshot;
			var state = harness.CreateState();

			Run(harness, ref state, 5 * Millisecond, 5 * Millisecond);

			Assert.That(Drain(harness), Is.Empty);
			var after = harness.RubberSlingshotStates[SlingshotItemId];
			Assert.That(after.SelfWireLevel, Is.Zero);
			Assert.That(after.CoilLevel, Is.Not.Zero);
			Assert.That(after.KickCount, Is.Zero);
		}

		[Test]
		public void ACommandShouldBeConsumedAtTheFirstTickAtOrAfterItsStamp()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSlingshotStates[SlingshotItemId] = State(selfWired: false);
			var state = harness.CreateState();

			Command(harness, true, 5 * Millisecond);
			Run(harness, ref state, 0, 4 * Millisecond);
			Assert.That(Drain(harness), Is.Empty);

			Run(harness, ref state, 5 * Millisecond, 5 * Millisecond);
			Assert.That(Kinds(Drain(harness)),
				Is.EqualTo(new[] { EventId.CoilEventsEnergized }));
		}

		#endregion

		#region Kick

		[Test]
		public void TheKickShouldApplyOnceAlongTheOutwardNormalWithTheArmProfile()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSpanRebounds[SpanId] =
				RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			harness.RubberSlingshotStates[SlingshotItemId] = State(selfWired: false);
			harness.Balls[1] = Ball(1, new float3(0.5f * SpanLength, 40f, 0f));
			harness.Balls[2] = Ball(2, new float3(0.625f * SpanLength, 40f, 0f));
			var state = harness.CreateState();

			Command(harness, true, 0);
			Run(harness, ref state, 0, 0);

			// x = 0 at the arm, x = 0.5 half a width away
			Assert.That(harness.Balls[1].Velocity.y, Is.EqualTo(30f).Within(1e-3f));
			Assert.That(harness.Balls[2].Velocity.y, Is.EqualTo(30f * 0.75f).Within(1e-3f));

			// a held coil does not kick again
			Command(harness, true, Millisecond);
			Run(harness, ref state, Millisecond, 10 * Millisecond);
			Assert.That(harness.Balls[1].Velocity.y, Is.EqualTo(30f).Within(1e-3f));
			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].KickCount, Is.EqualTo(1));
		}

		[Test]
		public void OnlyBallsInReachOnTheOutwardSideAndInsideTheArmShouldBeKicked()
		{
			var cases = new (string Name, float3 Position, bool Frozen, bool Kicked)[] {
				("in reach", new float3(75f, 40f, 0f), false, true),
				("at the reach limit", new float3(75f, 45f, 0f), false, true),
				("out of reach", new float3(75f, 50f, 0f), false, false),
				("behind the rubber", new float3(75f, -40f, 0f), false, false),
				("outside the arm width", new float3(20f, 40f, 0f), false, false),
				("above the rubber", new float3(75f, 40f, 40f), false, false),
				("frozen", new float3(75f, 40f, 0f), true, false),
			};
			foreach (var (name, position, frozen, kicked) in cases) {
				using var harness = new PhysicsStateHarness();
				harness.RubberSpanRebounds[SpanId] =
					RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
				harness.RubberSlingshotStates[SlingshotItemId] = State(selfWired: false);
				var ball = Ball(1, position);
				ball.IsFrozen = frozen;
				harness.Balls[1] = ball;
				var state = harness.CreateState();

				Command(harness, true, 0);
				Run(harness, ref state, 0, 0);

				Assert.That(harness.Balls[1].Velocity.y > 1e-3f, Is.EqualTo(kicked), name);
			}
		}

		[Test]
		public void TheCausalBallShouldBeKickedInsideItsWindowEvenOutOfReach()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSpanRebounds[SpanId] =
				RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			harness.RubberSlingshotStates[SlingshotItemId] = State();
			// out of reach (50 > 16 + 25 + 4), but it is the ball that closed the switch
			harness.Balls[BallId] = Ball(BallId, new float3(75f, 50f, 0f));
			var state = harness.CreateState();

			state.TickTimeUsec = 0;
			Impact(harness, ref state, 0.5f, 5f);
			Run(harness, ref state, Millisecond, 8 * Millisecond);

			Assert.That(harness.Balls[BallId].Velocity.y, Is.EqualTo(30f).Within(1e-3f));
		}

		[Test]
		public void TheCausalBallShouldNotBeKickedOnceItsWindowElapsed()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSpanRebounds[SpanId] =
				RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var slingshot = State();
			slingshot.WireDelayUsec = 40 * Millisecond; // beyond the 30 ms kick window
			harness.RubberSlingshotStates[SlingshotItemId] = slingshot;
			harness.Balls[BallId] = Ball(BallId, new float3(75f, 50f, 0f));
			var state = harness.CreateState();

			state.TickTimeUsec = 0;
			Impact(harness, ref state, 0.5f, 5f);
			Run(harness, ref state, Millisecond, 45 * Millisecond);

			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].KickCount, Is.EqualTo(1),
				"the coil still fires");
			Assert.That(harness.Balls[BallId].Velocity.y, Is.Zero, "but the ball is gone");
		}

		[Test]
		public void ACommandedEdgeShouldUseReachOnly()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSpanRebounds[SpanId] =
				RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			harness.RubberSlingshotStates[SlingshotItemId] = State(selfWired: false);
			harness.Balls[BallId] = Ball(BallId, new float3(75f, 50f, 0f));
			var state = harness.CreateState();

			state.TickTimeUsec = 0;
			Impact(harness, ref state, 0.5f, 5f);
			Command(harness, true, 0);
			Run(harness, ref state, 0, 0);

			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].KickCount, Is.EqualTo(1));
			Assert.That(harness.Balls[BallId].Velocity.y, Is.Zero);
		}

		#endregion

		#region Fault cases

		[Test]
		public void AHeldCoilAndARapidRetriggerShouldStayBounded()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSlingshotStates[SlingshotItemId] = State();
			var state = harness.CreateState();

			// the gamelogic engine holds the coil while the ball keeps hammering the span
			Command(harness, true, 0);
			for (var tick = 0ul; tick <= 200; tick++) {
				Run(harness, ref state, tick * Millisecond, tick * Millisecond);
				Impact(harness, ref state, 0.5f, 5f);
				Command(harness, true, tick * Millisecond);
			}

			var slingshot = harness.RubberSlingshotStates[SlingshotItemId];
			Assert.That(slingshot.KickCount, Is.EqualTo(1), "one kick per aggregate edge");
			Assert.That(slingshot.PulseCount, Is.LessThanOrEqualTo(RubberSlingshotState.MaxPulses));
			Assert.That(slingshot.CommandCount,
				Is.LessThanOrEqualTo(RubberSlingshotState.MaxCommands));
			Assert.That(slingshot.DroppedPulseCount, Is.Zero,
				"a closure while a pulse is pending schedules nothing");
			var energized = 0;
			foreach (var e in Drain(harness)) {
				if (e.EventId == EventId.CoilEventsEnergized) {
					energized++;
				}
			}
			Assert.That(energized, Is.EqualTo(1));
		}

		[Test]
		public void AStuckSwitchShouldNeverArmASecondPulse()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSlingshotStates[SlingshotItemId] = State();
			var state = harness.CreateState();

			for (var tick = 0ul; tick <= 300; tick++) {
				Run(harness, ref state, tick * Millisecond, tick * Millisecond);
				Impact(harness, ref state, 0.5f, 5f);
			}

			var slingshot = harness.RubberSlingshotStates[SlingshotItemId];
			Assert.That(slingshot.SwitchLevel, Is.Not.Zero, "the switch never opens");
			Assert.That(slingshot.SwitchCloseCount, Is.EqualTo(1));
			Assert.That(slingshot.KickCount, Is.EqualTo(1));
		}

		[Test]
		public void TheCommandRingShouldDropTheOldestAndCountIt()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSlingshotStates[SlingshotItemId] = State(selfWired: false);

			for (var i = 0; i < RubberSlingshotState.MaxCommands + 3; i++) {
				Command(harness, i % 2 == 0, 0);
			}

			var slingshot = harness.RubberSlingshotStates[SlingshotItemId];
			Assert.That(slingshot.CommandCount, Is.EqualTo(RubberSlingshotState.MaxCommands));
			Assert.That(slingshot.DroppedCommandCount, Is.EqualTo(3));
		}

		#endregion

		#region Isolation

		[Test]
		public void AnImpactOnAForeignSpanShouldLeaveTheSlingshotAlone()
		{
			using var harness = new PhysicsStateHarness();
			harness.RubberSlingshotStates[SlingshotItemId] = State();
			var state = harness.CreateState();
			state.TickTimeUsec = 0;

			var events = harness.EventQueue.AsParallelWriter();
			RubberSlingshotPhysics.ApplyImpact(SpanId + 1, BallId, 0.5f, 5f, ref state,
				ref events, true);

			Assert.That(harness.RubberSlingshotStates[SlingshotItemId].SwitchLevel, Is.Zero);
			Assert.That(Drain(harness), Is.Empty);
		}

		[Test]
		public void TheCompliantResponseShouldNotNeedASlingshotStateMap()
		{
			// A rubber without a slingshot runs with the map uncreated (unit fixtures) or
			// created but empty (a table without slingshots); neither may change the ball.
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var material = new PhysicsMaterialData { Elasticity = 0.8f, Friction = 0.3f };
			var collEvent = new CollisionEventData {
				HitNormal = new float3(0f, 1f, 0f),
				HitDistance = 0f,
			};

			var bare = Ball(1, new float3(75f, 29f, 0f));
			bare.Velocity = new float3(0f, -18f, 0f);
			var bareState = new PhysicsState();
			RubberSpanRebound.Respond(ref bare, in material, in collEvent, in record, 0.5f, 1f,
				ref bareState, out var bareOutcome);

			using var harness = new PhysicsStateHarness();
			var state = harness.CreateState();
			state.TickTimeUsec = 0;
			var events = harness.EventQueue.AsParallelWriter();
			var mapped = Ball(1, new float3(75f, 29f, 0f));
			mapped.Velocity = new float3(0f, -18f, 0f);
			RubberSpanRebound.Respond(ref mapped, in material, in collEvent, in record, 0.5f, 1f,
				ref state, SpanId, ref events, true, out var mappedOutcome);

			Assert.That(mapped.Velocity, Is.EqualTo(bare.Velocity));
			Assert.That(mappedOutcome.EffectiveDepth, Is.EqualTo(bareOutcome.EffectiveDepth));

			// the impact event is emitted all the same, with the packed payload
			var drained = Drain(harness);
			Assert.That(drained, Has.Count.EqualTo(1));
			Assert.That(drained[0].EventId, Is.EqualTo(EventId.RubberSpanImpact));
			Assert.That(drained[0].ItemId, Is.EqualTo(RubberSpanReboundFixture.ItemId));
			Assert.That(drained[0].IntParam, Is.EqualTo(SpanId));
			RubberSpanImpactPayload.Unpack(drained[0].FloatParam2, out var apex, out var depth);
			Assert.That(apex, Is.EqualTo(mappedOutcome.ApexFraction).Within(1e-2f));
			Assert.That(depth, Is.EqualTo(mappedOutcome.EffectiveDepth).Within(0.05f));
		}

		[Test]
		public void TheImpactPayloadShouldRoundTripThroughHalfPrecision()
		{
			foreach (var apex in new[] { 0.02f, 0.25f, 0.5f, 0.98f }) {
				foreach (var depth in new[] { 0.5f, 4f, 37.5f, -12f }) {
					RubberSpanImpactPayload.Unpack(RubberSpanImpactPayload.Pack(apex, depth),
						out var readApex, out var readDepth);
					Assert.That(readApex, Is.EqualTo(apex).Within(1e-3f));
					Assert.That(readDepth, Is.EqualTo(depth).Within(math.abs(depth) * 1e-2f + 1e-3f));
				}
			}
		}

		#endregion

		#region Helpers

		/// <summary>The default slingshot of the plan's 3.3, bound to <see cref="SpanId"/>.</summary>
		private static RubberSlingshotState State(bool selfWired = true)
		{
			var slingshot = new RubberSlingshotState {
				SpanId = SpanId,
				RubberItemId = RubberSpanReboundFixture.ItemId,
				PathElementIndex = 0,
				SelfWired = (byte)(selfWired ? 1 : 0),
				WireDelayUsec = 8 * Millisecond,
				SelfWirePulseUsec = 20 * Millisecond,
				KickWindowUsec = 30 * Millisecond,
				KickSpeed = 30f,
				ArmPosition01 = 0.5f,
				ArmHalfWidth01 = 0.25f,
				ArmReach = 16f,
				ZoneCount = 2,
			};
			RubberSlingshotState.Zone(ref slingshot, 0) = new RubberSlingshotZone {
				Position01 = 0.3f, CloseDeflection = 2f, DwellUsec = 8 * Millisecond,
			};
			RubberSlingshotState.Zone(ref slingshot, 1) = new RubberSlingshotZone {
				Position01 = 0.7f, CloseDeflection = 2f, DwellUsec = 8 * Millisecond,
			};
			return slingshot;
		}

		private static BallState Ball(int id, float3 position)
			=> new BallState {
				Id = id,
				Position = position,
				Radius = BallRadius,
				Mass = 1f,
			};

		private static void Impact(PhysicsStateHarness harness, ref PhysicsState state,
			float apexFraction, float effectiveDepth)
		{
			var events = harness.EventQueue.AsParallelWriter();
			RubberSlingshotPhysics.ApplyImpact(SpanId, BallId, apexFraction, effectiveDepth,
				ref state, ref events, true);
		}

		private static void Command(PhysicsStateHarness harness, bool level, ulong stampUsec)
		{
			ref var slingshot = ref harness.RubberSlingshotStates.GetValueByRef(SlingshotItemId);
			RubberSlingshotPhysics.EnqueueCommand(ref slingshot, level, stampUsec);
		}

		private static void Run(PhysicsStateHarness harness, ref PhysicsState state,
			ulong fromUsec, ulong toUsec)
		{
			for (var now = fromUsec; now <= toUsec; now += Millisecond) {
				RubberSlingshotPhysics.UpdateSlingshots(ref state, now);
			}
		}

		private static List<EventData> Drain(PhysicsStateHarness harness)
		{
			var events = new List<EventData>();
			while (harness.EventQueue.TryDequeue(out var e)) {
				events.Add(e);
			}
			return events;
		}

		private static EventId[] Kinds(List<EventData> events)
		{
			var kinds = new EventId[events.Count];
			for (var i = 0; i < events.Count; i++) {
				kinds[i] = events[i].EventId;
			}
			return kinds;
		}

		#endregion
	}
}
