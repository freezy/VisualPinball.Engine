// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using System.Collections.Generic;
using NLog;
using Logger = NLog.Logger;
using VisualPinball.Engine.VPT.Light;
using static Unity.Entities.SystemBaseDelegates;
using UnityEngine.SocialPlatforms.Impl;

namespace VisualPinball.Unity
{
	public class ScoreMotorApi : IApi, IApiSwitchDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly ScoreMotorComponent _scoreMotorComponent;
		private readonly Player _player;

		public event EventHandler Init;

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => Switch(deviceItem);

		private DeviceSwitch MotorRunningSwitch;
		private DeviceSwitch MotorStepSwitch;

		public IApiSwitch Switch(string deviceItem)
		{
			return deviceItem switch
			{
				ScoreMotorComponent.MotorRunningSwitchItem => MotorRunningSwitch,
				ScoreMotorComponent.MotorStepSwitchItem => MotorStepSwitch,
				_ => throw new ArgumentException($"Unknown switch \"{deviceItem}\". "
					+ "Valid names are \"{ScoreReelDisplayComponent.MotorRunningSwitchItem}\", and "
					+ "\"{ScoreReelDisplayComponent.MotorStepSwitchItem}\".")
			};
		}

		internal ScoreMotorApi(GameObject go, Player player)
		{
			_scoreMotorComponent = go.GetComponentInChildren<ScoreMotorComponent>();
			_player = player;

			_scoreMotorComponent.OnSwitchChanged += HandleSwitchChanged;
		}

		void IApi.OnInit(BallManager ballManager)
		{
			MotorRunningSwitch = new DeviceSwitch(ScoreMotorComponent.MotorRunningSwitchItem, false, SwitchDefault.NormallyOpen, _player);
			MotorStepSwitch = new DeviceSwitch(ScoreMotorComponent.MotorStepSwitchItem, true, SwitchDefault.NormallyOpen, _player);

			Init?.Invoke(this, EventArgs.Empty);
		}

		private void HandleSwitchChanged(object sender, SwitchEventArgs2 e)
		{
			((DeviceSwitch)Switch(e.Id)).SetSwitch(e.IsEnabled);
		}

		void IApi.OnDestroy()
		{
			_scoreMotorComponent.OnSwitchChanged -= HandleSwitchChanged;

			Logger.Info($"Destroying {_scoreMotorComponent.name}");
		}
	}
}

