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

namespace VisualPinball.Unity
{
	/// <summary>
	/// One blade switch zone of a physical slingshot, in span (path) coordinates.
	/// </summary>
	internal struct RubberSlingshotZone
	{
		/// <summary>Position along the span, already mapped out of a reversed binding.</summary>
		public float Position01;

		/// <summary>Deflection at which the zone closes, in VPX units.</summary>
		public float CloseDeflection;

		/// <summary>How long the zone stays closed once it closed, in microseconds.</summary>
		public ulong DwellUsec;

		/// <summary>Simulation time at which the dwell ends.</summary>
		public ulong ClosedUntilUsec;

		/// <summary>Non-zero while the zone is closed.</summary>
		public byte IsClosed;
	}

	/// <summary>
	/// One scheduled self-wire pulse: the switch closure that caused it and the two
	/// times at which <see cref="RubberSlingshotState.SelfWireLevel"/> changes.
	/// </summary>
	internal struct RubberSlingshotPulse
	{
		public ulong RiseUsec;
		public ulong FallUsec;

		/// <summary>Simulation time of the switch closure this pulse was armed by.</summary>
		public ulong ClosureUsec;

		/// <summary>The ball whose impact closed the switch, or zero.</summary>
		public int CausalBallId;

		/// <summary>Non-zero once the rise has been applied, so only then is the fall due.</summary>
		public byte Risen;
	}

	/// <summary>
	/// One commanded coil transition staged by the main thread, with the physics time
	/// of the tick boundary at which the input-action delegate ran.
	/// </summary>
	internal struct RubberSlingshotCommand
	{
		public ulong StampUsec;
		public byte Level;
	}

	/// <summary>
	/// Physics state of one physical slingshot, per the plan's section 3.1.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Keyed by the slingshot component's item id, not the rubber's. Everything is
	/// blittable: the rings are plain fields with static ref accessors, because a
	/// struct instance method may not return a reference to one of its own fields and
	/// fixed-size buffers would need pinning at every use site inside the tick hook.
	/// </para>
	/// <para>
	/// Times are absolute physics times in microseconds, the same clock as
	/// <see cref="PhysicsEnv.CurPhysicsFrameTime"/>, so nothing accumulates drift.
	/// </para>
	/// </remarks>
	internal struct RubberSlingshotState
	{
		internal const int MaxZones = 4;
		internal const int MaxPulses = 4;
		internal const int MaxCommands = 8;

		#region Binding

		/// <summary>
		/// Dense id of the bound compliant span, or <c>-1</c> while unresolved. Assigned
		/// in <see cref="PhysicsEngine.Start"/> after the spans were registered.
		/// </summary>
		public int SpanId;

		/// <summary>Item id of the rubber the span belongs to.</summary>
		public int RubberItemId;

		/// <summary>Index of the bound free span in the rubber's baked path.</summary>
		public int PathElementIndex;

		/// <summary>Non-zero when the authored binding runs against the path direction.</summary>
		public byte IsReversed;

		#endregion

		#region Configuration

		public int ZoneCount;
		public RubberSlingshotZone Zone0;
		public RubberSlingshotZone Zone1;
		public RubberSlingshotZone Zone2;
		public RubberSlingshotZone Zone3;

		/// <summary>Non-zero when the switch closure arms a self-wire pulse.</summary>
		public byte SelfWired;

		public ulong WireDelayUsec;
		public ulong SelfWirePulseUsec;
		public ulong KickWindowUsec;

		/// <summary>Kick magnitude at the arm position, in VPX units per step.</summary>
		public float KickSpeed;

		/// <summary>Arm contact position in span coordinates.</summary>
		public float ArmPosition01;

		/// <summary>Half width of the arm's kick window in span coordinates.</summary>
		public float ArmHalfWidth01;

		/// <summary>Fired arm displacement, <c>ArmTipTravel - ArmRestClearance</c>.</summary>
		public float ArmReach;

		#endregion

		#region Levels

		/// <summary>Non-zero while any zone is closed.</summary>
		public byte SwitchLevel;

		public byte SelfWireLevel;
		public byte CommandedLevel;

		/// <summary>The derived <c>SelfWireLevel || CommandedLevel</c>.</summary>
		public byte CoilLevel;

		#endregion

		#region Rings

		public int PulseHead;
		public int PulseCount;
		public RubberSlingshotPulse Pulse0;
		public RubberSlingshotPulse Pulse1;
		public RubberSlingshotPulse Pulse2;
		public RubberSlingshotPulse Pulse3;

		public int CommandHead;
		public int CommandCount;
		public RubberSlingshotCommand Command0;
		public RubberSlingshotCommand Command1;
		public RubberSlingshotCommand Command2;
		public RubberSlingshotCommand Command3;
		public RubberSlingshotCommand Command4;
		public RubberSlingshotCommand Command5;
		public RubberSlingshotCommand Command6;
		public RubberSlingshotCommand Command7;

		#endregion

		#region Counters

		public int KickCount;
		public int DroppedPulseCount;
		public int DroppedCommandCount;
		public int SwitchCloseCount;

		#endregion

		#region Accessors

		internal static ref RubberSlingshotZone Zone(ref RubberSlingshotState state, int index)
		{
			switch (index) {
				case 0: return ref state.Zone0;
				case 1: return ref state.Zone1;
				case 2: return ref state.Zone2;
				default: return ref state.Zone3;
			}
		}

		internal static ref RubberSlingshotPulse Pulse(ref RubberSlingshotState state, int index)
		{
			switch (index & (MaxPulses - 1)) {
				case 0: return ref state.Pulse0;
				case 1: return ref state.Pulse1;
				case 2: return ref state.Pulse2;
				default: return ref state.Pulse3;
			}
		}

		internal static ref RubberSlingshotCommand Command(ref RubberSlingshotState state, int index)
		{
			switch (index & (MaxCommands - 1)) {
				case 0: return ref state.Command0;
				case 1: return ref state.Command1;
				case 2: return ref state.Command2;
				case 3: return ref state.Command3;
				case 4: return ref state.Command4;
				case 5: return ref state.Command5;
				case 6: return ref state.Command6;
				default: return ref state.Command7;
			}
		}

		#endregion
	}
}
