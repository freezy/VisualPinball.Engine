// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Score Motor")]
	public class ScoreMotorComponent : MonoBehaviour, ICoilDeviceComponent, ISwitchDeviceComponent, ISerializationCallbackReceiver
	{
		[Unit("ms")]
		[Min(0)]
		[Tooltip("Amount of time, in milliseconds to move from the start to end position.")]
		public int Duration = 760;

		[Unit("\u00B0")]
		[Min(0)]
		[Tooltip("The total number of degrees from the start to the end position.")]
		public int Degrees = 120;

		[Tooltip("Define your switches here.")]
		public ScoreMotorSwitch[] Switches = { };

		public const string StartCoilItem = "start_coil";
		public const string MotorRunningSwitchItem = "motor_running_switch";

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(StartCoilItem) {
				Description = "Start Coil"
			}
		};

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => Switches.Select(m => m.Switch).Append(
			new GamelogicEngineSwitch(MotorRunningSwitchItem)
			{
				Description = "Motor Running Switch"
			}
		);

		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;
		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		public event EventHandler OnUpdate;

		#region Runtime

		private void Awake()
		{
			GetComponentInParent<Player>().RegisterScoreMotorComponent(this);
		}

		#endregion

		private void Start()
		{
		}

		private void Update()
		{
			OnUpdate?.Invoke(this, EventArgs.Empty);
		}

		#region ISerializationCallbackReceiver

		public void OnBeforeSerialize()
		{
#if UNITY_EDITOR
			var switchIds = new HashSet<string>();
			foreach (var @switch in Switches)
			{
				if (!@switch.HasId || switchIds.Contains(@switch.SwitchId))
				{
					@switch.GenerateId();
				}
				switchIds.Add(@switch.SwitchId);
			}
#endif
		}

		public void OnAfterDeserialize()
		{
		}

		#endregion
	}
}