// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using System.Threading;
using UnityEngine;

namespace VisualPinball.Unity
{
	public interface ISimulationThreadCoil
	{
		void OnCoilSimulationThread(bool enabled);
	}

	public class DeviceCoil : IApiCoil
		, ISimulationThreadCoil
	{
		private int _isEnabled;
		private int _simulationEnabled;

		/// <summary>
		/// When true, the simulation thread is actively dispatching coil
		/// changes for this coil. The main-thread <see cref="OnCoil"/>
		/// path will skip <see cref="OnEnable"/>/<see cref="OnDisable"/>
		/// callbacks to avoid double-firing (the sim thread already set
		/// the solenoid flag directly). UI refresh and status events
		/// still fire on the main thread.
		/// </summary>
		private volatile bool _simThreadActive;

		public bool IsEnabled => Volatile.Read(ref _isEnabled) != 0;
		public event EventHandler<NoIdCoilEventArgs> CoilStatusChanged;

		protected Action OnEnable;
		protected Action OnDisable;
		protected Action OnEnableSimulationThread;
		protected Action OnDisableSimulationThread;

		private readonly Player _player;

		public DeviceCoil(Player player, Action onEnable = null, Action onDisable = null,
			Action onEnableSimulationThread = null, Action onDisableSimulationThread = null)
		{
			_player = player;
			OnEnable = onEnable;
			OnDisable = onDisable;
			OnEnableSimulationThread = onEnableSimulationThread;
			OnDisableSimulationThread = onDisableSimulationThread;
		}

		public void OnCoil(bool enabled)
		{
			Interlocked.Exchange(ref _isEnabled, enabled ? 1 : 0);

			// When the simulation thread is actively handling this coil,
			// skip the main-thread enable/disable callbacks. The sim thread
			// already set the solenoid flag directly via OnCoilSimulationThread.
			// We still update _isEnabled above and fire CoilStatusChanged/UI
			// below, since those are main-thread-only concerns.
			if (!_simThreadActive) {
				if (enabled) {
					OnEnable?.Invoke();
				} else {
					OnDisable?.Invoke();
				}
			}
			CoilStatusChanged?.Invoke(this, new NoIdCoilEventArgs(enabled));
#if UNITY_EDITOR
			RefreshUI();
#endif
		}

		public void OnCoilSimulationThread(bool enabled)
		{
			// Mark this coil as sim-thread-active on first dispatch.
			// This suppresses the main-thread OnEnable/OnDisable callbacks.
			if (!_simThreadActive) {
				_simThreadActive = true;
			}

			if (Interlocked.Exchange(ref _simulationEnabled, enabled ? 1 : 0) == (enabled ? 1 : 0)) {
				return;
			}

			if (enabled) {
				OnEnableSimulationThread?.Invoke();
			} else {
				OnDisableSimulationThread?.Invoke();
			}
		}

		public void OnChange(bool enabled) => OnCoil(enabled);

		/// <summary>
		/// Reset simulation-thread state. Call when the simulation thread
		/// is torn down or the gamelogic engine stops, so that a subsequent
		/// session starts clean.
		/// </summary>
		internal void ResetSimulationState()
		{
			_simThreadActive = false;
			Interlocked.Exchange(ref _simulationEnabled, 0);
		}

#if UNITY_EDITOR
		private void RefreshUI()
		{
			if (!_player.UpdateDuringGameplay) {
				return;
			}

			foreach (var editor in (UnityEditor.Editor[])Resources.FindObjectsOfTypeAll(Type.GetType("VisualPinball.Unity.Editor.TroughInspector, VisualPinball.Unity.Editor"))) {
				editor.Repaint();
			}
		}
#endif
	}
}
