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

		private readonly int _degreesPerStep;
		private readonly float _degreesPerSecond;

		private bool _running;

		private float _time;
		private int _pos;

		private ScoreMotorMode _mode;
		private string _id;
		private int _increase;

		private float _score;
		private ScoreMotorResetCallback _resetCallback;

		private float _points;
		private ScoreMotorAddPointsCallback _addPointsCallback;

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

			_degreesPerSecond = _scoreMotorComponent.Degrees / (_scoreMotorComponent.Duration / 1000f);
			_degreesPerStep = _scoreMotorComponent.Degrees / _scoreMotorComponent.Steps;

			_scoreMotorComponent.OnReset += HandleReset;
			_scoreMotorComponent.OnAddPoints += HandleAddPoints;

			_running = false;
		}

		void IApi.OnInit(BallManager ballManager)
		{
			MotorRunningSwitch = new DeviceSwitch(ScoreMotorComponent.MotorRunningSwitchItem, false, SwitchDefault.NormallyOpen, _player);
			MotorStepSwitch = new DeviceSwitch(ScoreMotorComponent.MotorStepSwitchItem, true, SwitchDefault.NormallyOpen, _player);

			Init?.Invoke(this, EventArgs.Empty);
		}

		private void HandleReset(object sender, ScoreMotorResetEventArgs e)
		{
			if (_running) {
				Logger.Info($"already running (ignoring reset), id={e.Id}");
				return;
			}

			if (e.Score == 0) {
				Logger.Info($"score already 0 (ignoring reset), id={e.Id}");
				e.Callback(0);
				return;
			}

			Logger.Info($"reset, id={e.Id}, score={e.Score}");

			_mode = ScoreMotorMode.Reset;
			_id = e.Id;
			_score = e.Score;
			_increase = ScoreMotorComponent.MaxIncrease;
			_resetCallback = e.Callback;

			StartMotor();
		}

		private void HandleAddPoints(object sender, ScoreMotorAddPointsEventArgs e)
		{
			var increase = (int)
				((e.Points % 1000000000 == 0) ? e.Points / 1000000000 :
				 (e.Points % 100000000 == 0) ? e.Points / 100000000 :
				 (e.Points % 10000000 == 0) ? e.Points / 10000000 :
				 (e.Points % 1000000 == 0) ? e.Points / 1000000 :
				 (e.Points % 100000 == 0) ? e.Points / 100000 :
				 (e.Points % 10000 == 0) ? e.Points / 10000 :
				 (e.Points % 1000 == 0) ? e.Points / 1000 :
				 (e.Points % 100 == 0) ? e.Points / 100 :
				 (e.Points % 10 == 0) ? e.Points / 10 :
				 e.Points);

			if (increase > ScoreMotorComponent.MaxIncrease) {
				Logger.Error($"too many increases (ignoring points), id={e.Id}, points={e.Points}, increase={increase}");
				return;
			}

			if (_running) {
				if (increase > 1 || (increase == 1 && _scoreMotorComponent.BlockScoring)) {
					Logger.Info($"already running (ignoring points), id={e.Id}, points={e.Points}");
					return;
				}
			}

			if (increase == 1) {
				Logger.Info($"single points, id={e.Id}, points={e.Points}");
				e.Callback(e.Points);
				return;
			}

			Logger.Info($"multi points, id={e.Id}, increase={increase}, points={e.Points}");

			_mode = ScoreMotorMode.AddPoints;
			_id = e.Id;
			_increase = increase;
			_points = e.Points / increase;
			_addPointsCallback = e.Callback;

			StartMotor();
		}

		private void StartMotor()
		{
			Logger.Info($"start motor");

			_time = 0;
			_pos = 0;

			_running = true;

			MotorRunningSwitch.SetSwitch(true);

			AdvanceMotor();

			_scoreMotorComponent.OnUpdate += HandleUpdate;
		}

		private void AdvanceMotor()
		{
			if (_pos % _degreesPerStep == 0) {
				MotorStepSwitch.SetSwitch(true);

				var step = _pos / _degreesPerStep;
				var action = _scoreMotorComponent.ScoreMotorTimingList[_increase - 1].Actions[step];

				Logger.Info($"advance motor, pos={_pos}, time={_time}, increase={_increase}, step={step}, action={action}");

				if (action == ScoreMotorAction.Increase) {
					Increase();
				}
			}

			_pos++;
		}

		private void StopMotor()
		{
			Logger.Info($"stop motor");

			_scoreMotorComponent.OnUpdate -= HandleUpdate;

			MotorRunningSwitch.SetSwitch(false);

			_running = false;
		}

		private void HandleUpdate(object sender, EventArgs eventArgs)
		{
			_time += Time.deltaTime;

			int currentPos = (int)(_degreesPerSecond * _time);

			while (_pos <= currentPos && _pos < _scoreMotorComponent.Degrees) {
				AdvanceMotor();
			}

			if (_pos >= _scoreMotorComponent.Degrees) {
				if (_mode == ScoreMotorMode.Reset) {
					if (_score > 0) {
						_time = 0;
						_pos = 0;

						AdvanceMotor();

						return;
					}
				}

				StopMotor();
			}
		}

		void IApi.OnDestroy()
		{
			_scoreMotorComponent.OnUpdate -= HandleUpdate;

			_scoreMotorComponent.OnReset -= HandleReset;
			_scoreMotorComponent.OnAddPoints -= HandleAddPoints;

			Logger.Info($"Destroying {_scoreMotorComponent.name}");
		}

		private void Increase()
		{
			switch(_mode) {
				case ScoreMotorMode.Reset:
					_score = Reset(_score);
					Logger.Info($"increase, mode={_mode}, id={_id}, score={_score}");
					_resetCallback(0);
					break;

				case ScoreMotorMode.AddPoints:
					Logger.Info($"increase, mode={_mode}, id={_id}, points={_points}");
					_addPointsCallback(_points);
					break;
			}
		}

		private float Reset(float score)
		{
			float newScore = 0;

			var pos = 0;
			while (score > 0) {
				var i = (int)(score % 10);
				if (i > 0 && i < 9) {
					newScore += (float)(System.Math.Pow(10, pos) * (i + 1));
				}
				score = (int)(score / 10);
				pos++;
			}

			return newScore;
		}
	}
}

