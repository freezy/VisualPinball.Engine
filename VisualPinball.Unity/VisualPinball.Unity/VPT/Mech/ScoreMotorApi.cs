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
using Logger = NLog.Logger;
using NLog;
using UnityEngine;
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	public class ScoreMotorApi : IApi, IApiSwitchDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly ScoreMotorComponent _scoreMotorComponent;
		private readonly Player _player;

		public event EventHandler Init;

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => Switch(deviceItem);

		public DeviceCoil ResetCoil;
		public DeviceSwitch MotorRunningSwitch;
		public DeviceSwitch MotorStepSwitch;

		private int _degreesPerStep;
		private float _degreesPerSecond;

		private bool _running;

		private float _time;
		private int _pos;

		private float _score;
		private float _points;
		private int _increase;
		private DisplayComponent _displayComponent;

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
		}

		void IApi.OnInit(BallManager ballManager)
		{
			MotorRunningSwitch = new DeviceSwitch(ScoreMotorComponent.MotorRunningSwitchItem, false, SwitchDefault.NormallyOpen, _player);
			MotorStepSwitch = new DeviceSwitch(ScoreMotorComponent.MotorStepSwitchItem, true, SwitchDefault.NormallyOpen, _player);

			Init?.Invoke(this, EventArgs.Empty);

			_scoreMotorComponent.OnAddPoints += HandleAddPoints;

			_degreesPerSecond = _scoreMotorComponent.Degrees / (_scoreMotorComponent.Duration / 1000f);
			_degreesPerStep = _scoreMotorComponent.Degrees / _scoreMotorComponent.Steps;

			_score = 0;
			_points = 0;
			_increase = 0;
		}

		private void HandleAddPoints(object sender, ScoreMotorAddPointsEventArgs e)
		{
			var increase = (int)
				((e.Points % 1000000 == 0) ? e.Points / 1000000 :
				(e.Points % 100000 == 0) ? e.Points / 100000 :
				(e.Points % 10000 == 0) ? e.Points / 10000 :
				(e.Points % 1000 == 0) ? e.Points / 1000 :
				(e.Points % 100 == 0) ? e.Points / 100 :
				(e.Points % 10 == 0) ? e.Points / 10 :
				e.Points);

			if (increase > ScoreMotorComponent.MaxIncrease) {
				Logger.Info($"too many increases (ignoring points), name={_scoreMotorComponent.name}, points={e.Points}, increase={increase}");
				return;
			}

			if (_running) {
				if (increase > 1 || (increase == 1 && _scoreMotorComponent.BlockScoring))
				{
					Logger.Info($"already running (ignoring points), name={_scoreMotorComponent.name}, points={e.Points}");
					return;
				}
			}

			if (increase == 1) {
				Logger.Info($"single points, name={_scoreMotorComponent.name}, points={e.Points}");

				e.DisplayComponent.IncrementScore(e.Points);

				return;
			}

			_increase = increase;
			_points = e.Points / increase;
			_displayComponent = e.DisplayComponent;

			Logger.Info($"multi points, name={_scoreMotorComponent.name}, increase={_increase}, points={e.Points}");

			_time = 0;
			_pos = 0;

			_running = true;

			MotorRunningSwitch.SetSwitch(true);

			Advance();

			_scoreMotorComponent.OnUpdate += HandleUpdate;
		}

		private void HandleUpdate(object sender, EventArgs eventArgs)
		{
			_time += Time.deltaTime;

			int newPos = (int)(_degreesPerSecond * _time);

			while (_pos <= newPos && _pos < _scoreMotorComponent.Degrees) {
				Advance();
			}

			if (_pos >= _scoreMotorComponent.Degrees) {
				_scoreMotorComponent.OnUpdate -= HandleUpdate;

				MotorRunningSwitch.SetSwitch(false);

				_running = false;
			}
		}

		void IApi.OnDestroy()
		{
			_scoreMotorComponent.OnUpdate -= HandleUpdate;
			_scoreMotorComponent.OnAddPoints -= HandleAddPoints;

			Logger.Info($"Destroying {_scoreMotorComponent.name}");
		}

		private void Advance()
		{
			if (_pos % _degreesPerStep == 0) {
				MotorStepSwitch.SetSwitch(true);

				var step = _pos / _degreesPerStep;
				var action = _scoreMotorComponent.ScoreMotorActionsList[_increase - 1].Actions[step];

				Logger.Info($"advance, name={_scoreMotorComponent.name}, pos={_pos}, time={_time}, increase={_increase}, step={step}, points={_points}, action={action}");

				if (action == ScoreMotorAction.Increase) {
					_displayComponent.IncrementScore(_points);
				}
			}

			_pos++;
		}
	}
}

