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

using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The physical slingshot's tick contract of the plan's section 3: switch zones
	/// from span impacts, the self-wire and commanded coil sources, and the kick.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Everything here runs on the physics thread and is Burst-compatible: no managed
	/// objects, no allocations, no LINQ. <see cref="UpdateSlingshots"/> is called once
	/// per tick from <see cref="PhysicsUpdate"/> before the cycle;
	/// <see cref="ApplyImpact"/> is called from the collide pass, through the compliant
	/// rebound's response.
	/// </para>
	/// <para>
	/// <b>Impact recording.</b> Zones are evaluated in the collide pass, at the impact
	/// itself, rather than through a queue the tick hook drains. The collide pass
	/// already carries the dense span id and the response outcome, the slingshot state
	/// map is tiny (one entry per slingshot), and closing at the impact instant keeps
	/// the wire delay measured from the real closure instead of from the next tick
	/// boundary. The self-wire pulse it arms is consumed by the next tick hook whose
	/// time has reached the rise, so the ordering contract of 3.1 is unaffected.
	/// </para>
	/// </remarks>
	internal static class RubberSlingshotPhysics
	{
		/// <summary>Lower clamp of the apex fraction, per the plan's 2.3.</summary>
		internal const float MinimumApexFraction = 0.02f;

		/// <summary>Upper clamp of the apex fraction, per the plan's 2.3.</summary>
		internal const float MaximumApexFraction = 0.98f;

		private const int TransitionNone = 0;
		private const int TransitionFall = 1;
		private const int TransitionRise = 2;
		private const int TransitionCommand = 3;

		private const byte PulseArmed = 0;
		private const byte PulseRisen = 1;
		private const byte PulseConsumed = 2;

		#region Tick hook

		/// <summary>
		/// Advances every slingshot by one tick, before the cycle: expire the zone
		/// dwells, then apply every due source transition one at a time in the total
		/// order of the plan's 3.1, handling each aggregate coil edge immediately.
		/// </summary>
		internal static void UpdateSlingshots(ref PhysicsState state, ulong nowUsec)
		{
			state.TickTimeUsec = nowUsec;
			if (!state.RubberSlingshotStates.IsCreated) {
				return;
			}
			var timeMs = SimulationTimeMs(in state, nowUsec);
			using var enumerator = state.RubberSlingshotStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				var itemId = enumerator.Current.Key;
				ref var slingshot = ref enumerator.Current.Value;
				Update(itemId, ref slingshot, ref state, nowUsec, timeMs);
			}
		}

		private static void Update(int itemId, ref RubberSlingshotState slingshot,
			ref PhysicsState state, ulong nowUsec, float timeMs)
		{
			ExpireZones(itemId, ref slingshot, ref state, nowUsec, timeMs);

			// the rings are bounded, so this loop is too
			var budget = RubberSlingshotState.MaxPulses * 2 + RubberSlingshotState.MaxCommands;
			for (var i = 0; i < budget; i++) {
				var kind = NextTransition(ref slingshot, nowUsec, out var pulseSlot);
				if (kind == TransitionNone) {
					return;
				}
				ApplyTransition(itemId, ref slingshot, ref state, nowUsec, timeMs, kind, pulseSlot);
			}
		}

		private static void ExpireZones(int itemId, ref RubberSlingshotState slingshot,
			ref PhysicsState state, ulong nowUsec, float timeMs)
		{
			var anyClosed = false;
			for (var i = 0; i < slingshot.ZoneCount; i++) {
				ref var zone = ref RubberSlingshotState.Zone(ref slingshot, i);
				if (zone.IsClosed != 0 && nowUsec >= zone.ClosedUntilUsec) {
					zone.IsClosed = 0;
				}
				anyClosed |= zone.IsClosed != 0;
			}
			if (anyClosed || slingshot.SwitchLevel == 0) {
				return;
			}
			slingshot.SwitchLevel = 0;
			state.EventQueue.Enqueue(new EventData(EventId.SwitchEventsOpened, itemId, 0, timeMs));
		}

		/// <summary>
		/// The next due transition in the total order of 3.1: by due time, then falls
		/// before rises before commands, then insertion order. A pulse's fall only
		/// becomes due once its own rise has been applied, which is what puts a rise
		/// before its own fall inside one drain.
		/// </summary>
		private static int NextTransition(ref RubberSlingshotState slingshot, ulong nowUsec,
			out int pulseSlot)
		{
			pulseSlot = -1;
			var kind = TransitionNone;
			var bestTime = ulong.MaxValue;
			var bestRank = int.MaxValue;
			for (var i = 0; i < slingshot.PulseCount; i++) {
				var slot = slingshot.PulseHead + i;
				ref var pulse = ref RubberSlingshotState.Pulse(ref slingshot, slot);
				if (pulse.Risen == PulseConsumed) {
					continue;
				}
				var due = pulse.Risen == PulseRisen ? pulse.FallUsec : pulse.RiseUsec;
				var rank = pulse.Risen == PulseRisen ? 0 : 1;
				if (due > nowUsec || !IsBetter(due, rank, bestTime, bestRank)) {
					continue;
				}
				bestTime = due;
				bestRank = rank;
				pulseSlot = slot;
				kind = pulse.Risen == PulseRisen ? TransitionFall : TransitionRise;
			}
			if (slingshot.CommandCount > 0) {
				ref var command = ref RubberSlingshotState.Command(ref slingshot, slingshot.CommandHead);
				if (command.StampUsec <= nowUsec && IsBetter(command.StampUsec, 2, bestTime, bestRank)) {
					pulseSlot = -1;
					kind = TransitionCommand;
				}
			}
			return kind;
		}

		private static bool IsBetter(ulong time, int rank, ulong bestTime, int bestRank)
			=> time < bestTime || (time == bestTime && rank < bestRank);

		private static void ApplyTransition(int itemId, ref RubberSlingshotState slingshot,
			ref PhysicsState state, ulong nowUsec, float timeMs, int kind, int pulseSlot)
		{
			var causalBallId = 0;
			var closureUsec = 0ul;
			switch (kind) {
				case TransitionRise: {
					ref var pulse = ref RubberSlingshotState.Pulse(ref slingshot, pulseSlot);
					pulse.Risen = PulseRisen;
					causalBallId = pulse.CausalBallId;
					closureUsec = pulse.ClosureUsec;
					slingshot.SelfWireLevel = 1;
					break;
				}
				case TransitionFall: {
					ref var pulse = ref RubberSlingshotState.Pulse(ref slingshot, pulseSlot);
					pulse.Risen = PulseConsumed;
					slingshot.SelfWireLevel = 0;
					CompactPulses(ref slingshot);
					break;
				}
				case TransitionCommand: {
					ref var command = ref RubberSlingshotState.Command(ref slingshot, slingshot.CommandHead);
					var level = command.Level;
					slingshot.CommandHead = (slingshot.CommandHead + 1) & (RubberSlingshotState.MaxCommands - 1);
					slingshot.CommandCount--;
					if (level == slingshot.CommandedLevel) {
						// a repeated enable is not a transition, so it cannot retrigger
						return;
					}
					slingshot.CommandedLevel = level;
					break;
				}
				default:
					return;
			}

			var coilLevel = (byte)(slingshot.SelfWireLevel != 0 || slingshot.CommandedLevel != 0 ? 1 : 0);
			if (coilLevel == slingshot.CoilLevel) {
				return;
			}
			slingshot.CoilLevel = coilLevel;
			if (coilLevel != 0) {
				slingshot.KickCount++;
				Kick(ref slingshot, ref state, nowUsec, causalBallId, closureUsec);
				state.EventQueue.Enqueue(new EventData(EventId.CoilEventsEnergized, itemId, timeMs));
			} else {
				state.EventQueue.Enqueue(new EventData(EventId.CoilEventsReleased, itemId, timeMs));
			}
		}

		private static void CompactPulses(ref RubberSlingshotState slingshot)
		{
			while (slingshot.PulseCount > 0
				&& RubberSlingshotState.Pulse(ref slingshot, slingshot.PulseHead).Risen == PulseConsumed) {
				slingshot.PulseHead = (slingshot.PulseHead + 1) & (RubberSlingshotState.MaxPulses - 1);
				slingshot.PulseCount--;
			}
		}

		#endregion

		#region Impacts and switch zones

		/// <summary>
		/// Evaluates the switch zones of every slingshot bound to a span that was just
		/// deflected, per the plan's 3.2. Impacts with zero effective depth close
		/// nothing.
		/// </summary>
		internal static void ApplyImpact(int spanId, int ballId, float apexFraction,
			float effectiveDepth, ref PhysicsState state,
			ref NativeQueue<EventData>.ParallelWriter events, bool emitEvents)
		{
			if (spanId < 0 || !(effectiveDepth > 0f) || !state.RubberSlingshotStates.IsCreated) {
				return;
			}
			var nowUsec = state.TickTimeUsec;
			var timeMs = SimulationTimeMs(in state, nowUsec);
			var apex = math.clamp(apexFraction, MinimumApexFraction, MaximumApexFraction);
			using var enumerator = state.RubberSlingshotStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				var itemId = enumerator.Current.Key;
				ref var slingshot = ref enumerator.Current.Value;
				if (slingshot.SpanId != spanId) {
					continue;
				}
				var closedAny = false;
				for (var i = 0; i < slingshot.ZoneCount; i++) {
					ref var zone = ref RubberSlingshotState.Zone(ref slingshot, i);
					if (!(zone.CloseDeflection > 0f)
						|| Deflection(effectiveDepth, apex, zone.Position01) < zone.CloseDeflection) {
						continue;
					}
					zone.IsClosed = 1;
					zone.ClosedUntilUsec = nowUsec + zone.DwellUsec;
					closedAny = true;
				}
				if (!closedAny || slingshot.SwitchLevel != 0) {
					continue;
				}
				slingshot.SwitchLevel = 1;
				slingshot.SwitchCloseCount++;
				if (emitEvents) {
					events.Enqueue(new EventData(EventId.SwitchEventsClosed, itemId, ballId, timeMs));
				}
				ArmSelfWirePulse(ref slingshot, nowUsec, ballId);
			}
		}

		/// <summary>
		/// The V deflection profile of 3.2: <c>w(u) = delta * u / u_h</c> below the apex
		/// and <c>delta * (1 - u) / (1 - u_h)</c> above it.
		/// </summary>
		internal static float Deflection(float effectiveDepth, float clampedApex, float position01)
		{
			if (position01 <= clampedApex) {
				return effectiveDepth * math.max(0f, position01) / clampedApex;
			}
			return effectiveDepth * math.max(0f, 1f - position01) / (1f - clampedApex);
		}

		private static void ArmSelfWirePulse(ref RubberSlingshotState slingshot, ulong closureUsec,
			int ballId)
		{
			if (slingshot.SelfWired == 0 || slingshot.SelfWireLevel != 0) {
				return;
			}
			for (var i = 0; i < slingshot.PulseCount; i++) {
				if (RubberSlingshotState.Pulse(ref slingshot, slingshot.PulseHead + i).Risen != PulseConsumed) {
					// a pulse is already pending; the closure schedules nothing
					return;
				}
			}
			if (slingshot.PulseCount == RubberSlingshotState.MaxPulses) {
				slingshot.PulseHead = (slingshot.PulseHead + 1) & (RubberSlingshotState.MaxPulses - 1);
				slingshot.PulseCount--;
				slingshot.DroppedPulseCount++;
			}
			var slot = slingshot.PulseHead + slingshot.PulseCount;
			ref var pulse = ref RubberSlingshotState.Pulse(ref slingshot, slot);
			pulse.RiseUsec = closureUsec + slingshot.WireDelayUsec;
			pulse.FallUsec = pulse.RiseUsec + slingshot.SelfWirePulseUsec;
			pulse.ClosureUsec = closureUsec;
			pulse.CausalBallId = ballId;
			pulse.Risen = PulseArmed;
			slingshot.PulseCount++;
		}

		#endregion

		#region Commands

		/// <summary>
		/// Appends a commanded coil transition. Called from the physics engine's
		/// input-action delegate, which runs at a tick boundary before the tick loop, so
		/// the stamp is that boundary's physics time.
		/// </summary>
		internal static void EnqueueCommand(ref RubberSlingshotState slingshot, bool level,
			ulong stampUsec)
		{
			if (slingshot.CommandCount == RubberSlingshotState.MaxCommands) {
				slingshot.CommandHead = (slingshot.CommandHead + 1) & (RubberSlingshotState.MaxCommands - 1);
				slingshot.CommandCount--;
				slingshot.DroppedCommandCount++;
			}
			var slot = slingshot.CommandHead + slingshot.CommandCount;
			ref var command = ref RubberSlingshotState.Command(ref slingshot, slot);
			command.Level = (byte)(level ? 1 : 0);
			command.StampUsec = stampUsec;
			slingshot.CommandCount++;
		}

		#endregion

		#region Kick

		/// <summary>
		/// The kick rule of 3.3, applied once per aggregate rising edge.
		/// </summary>
		private static void Kick(ref RubberSlingshotState slingshot, ref PhysicsState state,
			ulong nowUsec, int causalBallId, ulong closureUsec)
		{
			if (slingshot.SpanId < 0 || !state.Balls.IsCreated || !state.RubberSpanRebounds.IsCreated
				|| !state.RubberSpanRebounds.TryGetValue(slingshot.SpanId, out var span)
				|| !(span.Length > 0f)) {
				return;
			}
			var cordRadius = CordRadius(in span);
			var windowOpen = causalBallId != 0 && nowUsec >= closureUsec
				&& nowUsec - closureUsec <= slingshot.KickWindowUsec;

			using var enumerator = state.Balls.GetEnumerator();
			while (enumerator.MoveNext()) {
				var ballId = enumerator.Current.Key;
				ref var ball = ref enumerator.Current.Value;
				if (ball.IsFrozen || ball.AttachedMagnetId != 0 || IsCaptured(ballId, ref state)) {
					continue;
				}
				var along = math.clamp(math.dot(ball.Position - span.AnchorA, span.ChordDirection),
					0f, span.Length);
				var offset = ball.Position - (span.AnchorA + span.ChordDirection * along);
				if (math.dot(offset, span.OutwardNormal) <= 0f) {
					continue; // behind the rubber
				}
				if (math.abs(offset.z) > ball.Radius + cordRadius) {
					continue; // outside the rubber's height range
				}
				var u = along / span.Length;
				var x = slingshot.ArmHalfWidth01 > 0f
					? (u - slingshot.ArmPosition01) / slingshot.ArmHalfWidth01
					: 2f;
				if (math.abs(x) > 1f) {
					continue; // outside the arm's width
				}
				var reach = slingshot.ArmReach + ball.Radius + cordRadius;
				var inReach = math.length(offset) <= reach;
				var inWindow = windowOpen && ballId == causalBallId;
				if (!inReach && !inWindow) {
					continue;
				}
				ball.Velocity += span.OutwardNormal * (slingshot.KickSpeed * (1f - x * x));
			}
		}

		/// <summary>The cord radius the span record's cross-section area stands for.</summary>
		private static float CordRadius(in RubberSpanRebound span)
			=> 0.5f * math.sqrt(math.max(0f, 4f * span.CrossSectionArea / math.PI));

		private static bool IsCaptured(int ballId, ref PhysicsState state)
		{
			if (!state.KickerStates.IsCreated) {
				return false;
			}
			using var enumerator = state.KickerStates.GetEnumerator();
			while (enumerator.MoveNext()) {
				if (enumerator.Current.Value.Collision.BallId == ballId) {
					return true;
				}
			}
			return false;
		}

		#endregion

		#region Time

		/// <summary>Simulation time in milliseconds since the table started.</summary>
		internal static float SimulationTimeMs(in PhysicsState state, ulong nowUsec)
			=> nowUsec > state.Env.StartTimeUsec
				? (nowUsec - state.Env.StartTimeUsec) / 1000f
				: 0f;

		#endregion
	}
}
