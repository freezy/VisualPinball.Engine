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

namespace VisualPinball.Unity
{
	public class ScoreReelDisplayApi : IApi, IApiCoilDevice, IApiSwitchDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly ScoreReelDisplayComponent _scoreReelDisplayComponent;
		private readonly Player _player;

		public event EventHandler Init;

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);
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

		private IApiCoil Coil(string deviceItem)
		{
			return deviceItem switch
			{
				ScoreReelDisplayComponent.ResetCoilItem => ResetCoil,
				_ => throw new ArgumentException($"Unknown coil \"{deviceItem}\". Valid name is \"{ScoreReelDisplayComponent.ResetCoilItem}\".")
			};
		}

		public IApiSwitch Switch(string deviceItem)
		{
			return deviceItem switch
			{
				ScoreReelDisplayComponent.MotorRunningSwitchItem => MotorRunningSwitch,
				ScoreReelDisplayComponent.MotorStepSwitchItem => MotorStepSwitch,
				_ => throw new ArgumentException($"Unknown switch \"{deviceItem}\". "
					+ "Valid names are \"{ScoreReelDisplayComponent.MotorRunningSwitchItem}\", and "
					+ "\"{ScoreReelDisplayComponent.MotorStepSwitchItem}\".")
			};
		}

		internal ScoreReelDisplayApi(GameObject go, Player player)
		{
			_scoreReelDisplayComponent = go.GetComponentInChildren<ScoreReelDisplayComponent>();
			_player = player;
		}

		void IApi.OnInit(BallManager ballManager)
		{
			ResetCoil = new DeviceCoil(_player, OnResetCoilEnabled);

			MotorRunningSwitch = new DeviceSwitch(ScoreReelDisplayComponent.MotorRunningSwitchItem, false, SwitchDefault.NormallyOpen, _player);
			MotorStepSwitch = new DeviceSwitch(ScoreReelDisplayComponent.MotorStepSwitchItem, true, SwitchDefault.NormallyOpen, _player);

			Init?.Invoke(this, EventArgs.Empty);

			_scoreReelDisplayComponent.OnScore += HandleScore;

			_degreesPerSecond = _scoreReelDisplayComponent.Degrees / (_scoreReelDisplayComponent.Duration / 1000f);
			_degreesPerStep = (int)(_scoreReelDisplayComponent.Degrees / _scoreReelDisplayComponent.Steps);

			_score = 0;
			_points = 0;
			_increase = 0;
		}

		private void OnResetCoilEnabled()
		{
		}

		private void HandleScore(object sender, DisplayScoreEventArgs e)
		{
			if (_running) {
				Logger.Info($"{_scoreReelDisplayComponent.name} - aleady running");
				return;
			}

			if (e.Score == 10 || e.Score == 100 || e.Score == 1000) {
				Logger.Info($"{_scoreReelDisplayComponent.name} - single point score: {e.Score}");
				_score = _score + e.Score;
				_scoreReelDisplayComponent.UpdateScore(_score);
				return;
			}

			if (e.Score == 2000 || e.Score == 200 || e.Score == 20) {
				_increase = 2;
				_points = e.Score / 2;
			}

			if (e.Score == 3000 || e.Score == 300 || e.Score == 30) {
				_increase = 3;
				_points = e.Score / 3;
			}

			if (e.Score == 4000 || e.Score == 400 || e.Score == 40) {
				_increase = 4;
				_points = e.Score / 4;
			}

			if (e.Score == 5000 || e.Score == 500 || e.Score == 50) {
				_increase = 5;
				_points = e.Score / 5;
			}

			_time = 0;
			_pos = 0;

			_running = true;

			MotorRunningSwitch.SetSwitch(true);

			Advance();

			_scoreReelDisplayComponent.OnUpdate += HandleUpdate;
		}

		private void HandleUpdate(object sender, EventArgs eventArgs)
		{
			_time += Time.deltaTime;

			int newPos = (int)(_degreesPerSecond * _time);

			while (_pos <= newPos && _pos < _scoreReelDisplayComponent.Degrees) {
				Advance();
			}

			if (_pos >= _scoreReelDisplayComponent.Degrees) {
				_scoreReelDisplayComponent.OnUpdate -= HandleUpdate;

				MotorRunningSwitch.SetSwitch(false);

				_running = false;
			}
		}

		void IApi.OnDestroy()
		{
			_scoreReelDisplayComponent.OnUpdate -= HandleUpdate;
			_scoreReelDisplayComponent.OnScore -= HandleScore;

			Logger.Info($"Destroying {_scoreReelDisplayComponent.name}");
		}

		private void Advance()
		{
			if (_pos % _degreesPerStep == 0) {
				MotorStepSwitch.SetSwitch(true);

				var step = (int)(_pos / _degreesPerStep);
				var action = _scoreReelDisplayComponent.ScoreMotorActionsList[_increase - 1].Actions[step];

				Logger.Info($"{_scoreReelDisplayComponent.name} advancing - pos={_pos}, time={_time}, increase={_increase}, step={step}, points={_points}, action={action}");

				if (action == ScoreMotorAction.Increase) {
					_score += _points;
					_scoreReelDisplayComponent.UpdateScore(_score);
				}
			}

			_pos++;
		}
	}
}

