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
using Logger = NLog.Logger;
using NLog;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Step Rotator")]
	public class StepRotatorComponent : MonoBehaviour, ISwitchDeviceComponent, ICoilDeviceComponent
	{
		#region Data

		public int NumSteps;
		public StepRotatorMark[] Marks;

		#endregion

		#region Constants

		public const string MotorCoilItem = "motor_coil";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#endregion

		internal KickerComponent[] Kickers;

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

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null)
			{
				Logger.Error($"Cannot find player for cannon {name}.");
				return;
			}

			Kickers = GetComponentsInChildren<KickerComponent>();

			player.RegisterStepRotator(this);
		}

		public void UpdateRotation(float y)
		{
			var rotation = transform.rotation;
			rotation.y = -(y * 0.65f);

			transform.rotation = rotation;
		}

		#endregion
	}

	[Serializable]
	public class StepRotatorMark
	{
		public string Description;
		public string SwitchId;
		public int StepBeginning;
		public int StepEnd;

		public GamelogicEngineSwitch Switch => new(SwitchId) { Description = Description };

		public StepRotatorMark(string description, string switchId, int stepBeginning, int stepEnd)
		{
			Description = description;
			SwitchId = switchId;
			StepBeginning = stepBeginning;
			StepEnd = stepEnd;
		}
	}

}
