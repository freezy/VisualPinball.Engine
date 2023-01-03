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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Mechs/Step Rotator Mech")]
	public class StepRotatorMechComponent : MonoBehaviour, ISwitchDeviceComponent, ICoilDeviceComponent, IMechHandler, ISerializationCallbackReceiver
	{
		#region Data

		[Min(0)]
		public int NumSteps;

		[Tooltip("On each mark, the switch changes are transmitted to the gamelogic engine.")]
		public MechMark[] Marks = {};

		#endregion

		#region Constants

		public const string MotorCoilItem = "motor_coil";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#endregion

		#region Wiring

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(MotorCoilItem) {
				Description = "Motor"
			}
		};

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => Marks.Select(m => m.Switch);

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;

		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		#endregion

		#region Runtime

		public event EventHandler<MechEventArgs> OnMechUpdate;

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null) {
				Logger.Error($"Cannot find player for step rotator {name}.");
				return;
			}

			player.RegisterStepRotator(this);
		}

		public void UpdateRotation(float speed, float position)
		{
			OnMechUpdate?.Invoke(this, new MechEventArgs(speed, position));
		}

		#endregion

		#region ISerializationCallbackReceiver

		public void OnBeforeSerialize()
		{
			#if UNITY_EDITOR

			var switchIds = new HashSet<string>();
			foreach (var mark in Marks) {
				if (!mark.HasId || switchIds.Contains(mark.SwitchId)) {
					mark.GenerateId();
				}
				switchIds.Add(mark.SwitchId);
			}
			#endif
		}

		public void OnAfterDeserialize()
		{
		}

		#endregion

	}

}
