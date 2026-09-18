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

using System;
using UnityEngine;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The runtime of a physical slingshot: one blade switch and one actuator coil, as
	/// seen by the gamelogic engine, the player wiring and the scripting api.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The api never touches balls or physics state directly. A coil command becomes a
	/// commanded transition appended to the slingshot's ring through a physics input
	/// action, and the physics thread decides what it does; everything the api reports
	/// comes back through the event queue.
	/// </para>
	/// <para>
	/// Switch edges reach the gamelogic engine through
	/// <see cref="SlingshotSwitchPulseTimer"/>, which guarantees the minimum pulse at
	/// the delivery call; player wires get the raw edges.
	/// </para>
	/// </remarks>
	[Api]
	public class RubberSlingshotApi : IApi, IApiSwitchDevice, IApiCoilDevice, IApiWireDeviceDest,
		IApiRubberSlingshot
	{
		/// <summary>Event emitted when the table is started.</summary>
		public event EventHandler Init;

		/// <summary>Event emitted on every aggregate coil transition.</summary>
		public event EventHandler<NoIdCoilEventArgs> CoilStatusChanged;

		/// <summary>The OR-ed blade switch of the slingshot's switch zones.</summary>
		public DeviceSwitch BladeSwitch { get; }

		/// <summary>The actuator coil. Enabling it appends a commanded transition.</summary>
		public DeviceCoil ActuatorCoil { get; }

		/// <summary>Whether the coil is currently energized, from the last coil edge.</summary>
		public bool IsCoilEnergized { get; private set; }

		/// <summary>Whether the blade switch is currently closed, from the last edge.</summary>
		public bool IsSwitchClosed { get; private set; }

		/// <summary>Simulation time of the last closing edge, in milliseconds.</summary>
		public float LastSwitchCloseSimulationTimeMs { get; private set; } = float.NaN;

		/// <summary>Simulation time of the last energizing edge, in milliseconds.</summary>
		public float LastCoilEnergizedSimulationTimeMs { get; private set; } = float.NaN;

		/// <summary>The ball that closed the switch last, zero if none did.</summary>
		public int LastCausalBallId { get; private set; }

		/// <summary>How many times the coil energized since the table started.</summary>
		public int CoilEnergizeCount { get; private set; }

		internal int ItemId { get; }

		private readonly Player _player;
		private readonly PhysicsEngine _physicsEngine;
		private SlingshotSwitchPulseTimer _pulseTimer;

		public RubberSlingshotApi(GameObject go, Player player, PhysicsEngine physicsEngine,
			RubberSlingshotComponent component)
		{
			ItemId = UnityObjectId.Get(go);
			_player = player;
			_physicsEngine = physicsEngine;
			_pulseTimer = new SlingshotSwitchPulseTimer(component.MinimumSwitchPulseMs / 1000f);
			BladeSwitch = new DeviceSwitch(RubberSlingshotComponent.SwitchItem, false,
				component.SwitchDefault, player, physicsEngine);
			ActuatorCoil = new DeviceCoil(player, () => Command(true), () => Command(false));
		}

		#region Wiring

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => BladeSwitch;
		IApiCoil IApiCoilDevice.Coil(string deviceItem) => ActuatorCoil;
		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => ActuatorCoil;

		#endregion

		#region Commands

		/// <summary>
		/// Appends a commanded coil transition. Repeated enables append repeated levels,
		/// which the tick hook treats as no transition, so a held coil cannot retrigger.
		/// </summary>
		private void Command(bool level)
		{
			if (!_physicsEngine) {
				return;
			}
			var itemId = ItemId;
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (!state.RubberSlingshotStates.IsCreated
					|| !state.RubberSlingshotStates.ContainsKey(itemId)) {
					return;
				}
				ref var slingshot = ref state.RubberSlingshotStates.GetValueByRef(itemId);
				RubberSlingshotPhysics.EnqueueCommand(ref slingshot, level,
					state.Env.CurPhysicsFrameTime);
			});
		}

		#endregion

		#region Events

		void IApi.OnInit(BallManager ballManager)
		{
			if (_player) {
				_player.OnUpdate += OnPlayerUpdate;
			}
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
			if (_player) {
				_player.OnUpdate -= OnPlayerUpdate;
			}
		}

		private void OnPlayerUpdate(object sender, EventArgs args) => PumpPulseTimer(NowSeconds);

		/// <summary>Main-thread clock of the pulse timer.</summary>
		private static float NowSeconds => Time.realtimeSinceStartup;

		void IApiRubberSlingshot.OnSwitchEdge(bool closed, int ballId, float simulationTimeMs)
		{
			if (closed) {
				LastSwitchCloseSimulationTimeMs = simulationTimeMs;
				LastCausalBallId = ballId;
			}
			OnSwitchEdge(closed, ballId, NowSeconds);
		}

		void IApiRubberSlingshot.OnCoilEdge(bool energized, float simulationTimeMs)
		{
			IsCoilEnergized = energized;
			if (energized) {
				LastCoilEnergizedSimulationTimeMs = simulationTimeMs;
				CoilEnergizeCount++;
			}
			CoilStatusChanged?.Invoke(this, new NoIdCoilEventArgs(energized));
		}

		/// <summary>
		/// Delivers one switch edge: the close at once, the open only once the minimum
		/// pulse elapsed. Wires always get the raw edge.
		/// </summary>
		internal void OnSwitchEdge(bool closed, int ballId, float nowSeconds)
		{
			IsSwitchClosed = closed;
			if (closed) {
				var toEngine = _pulseTimer.OnClose(nowSeconds);
				BladeSwitch.SetSwitch(true, toEngine, true);
				return;
			}
			var deliverNow = _pulseTimer.OnOpen(nowSeconds);
			BladeSwitch.SetSwitch(false, deliverNow, true);
		}

		/// <summary>Releases a deferred opening edge once its window elapsed.</summary>
		internal void PumpPulseTimer(float nowSeconds)
		{
			if (_pulseTimer.Poll(nowSeconds)) {
				BladeSwitch.SetSwitch(false, true, false);
			}
		}

		#endregion
	}
}
