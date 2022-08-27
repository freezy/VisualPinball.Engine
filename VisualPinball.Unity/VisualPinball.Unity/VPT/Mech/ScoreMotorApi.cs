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

		public DeviceCoil ResetCoil;
		public DeviceSwitch MotorRunningSwitch;
		public DeviceSwitch MotorStepSwitch;

		private int _degreesPerStep;
		private float _degreesPerSecond;

		private bool _running;

		private float _time;
		private int _pos;

		private ScoreMotorMode _mode;
		private string _id;
		private int _increase;
		private float _points;
		private ScoreMotorActionCallback _callback;

		private Dictionary<string, DisplayComponent> _displays = new();
		private Dictionary<string, float> _displayScores = new();

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

			_displays.Clear();
			_displayScores.Clear();

			_degreesPerSecond = _scoreMotorComponent.Degrees / (_scoreMotorComponent.Duration / 1000f);
			_degreesPerStep = _scoreMotorComponent.Degrees / _scoreMotorComponent.Steps;

			_scoreMotorComponent.OnAttachDisplayComponent += HandleAttachDisplayComponent;
			_scoreMotorComponent.OnResetScore += HandleResetScore;
			_scoreMotorComponent.OnAddPoints += HandleAddPoints;

			_running = false;
		}

		void IApi.OnInit(BallManager ballManager)
		{
			MotorRunningSwitch = new DeviceSwitch(ScoreMotorComponent.MotorRunningSwitchItem, false, SwitchDefault.NormallyOpen, _player);
			MotorStepSwitch = new DeviceSwitch(ScoreMotorComponent.MotorStepSwitchItem, true, SwitchDefault.NormallyOpen, _player);

			Init?.Invoke(this, EventArgs.Empty);
		}

		private void HandleAttachDisplayComponent(object sender, ScoreMotorAttachDisplayComponentEventArgs e)
		{
			var id = e.DisplayComponent.Id;

			_displays[id] = e.DisplayComponent;
			_displayScores[id] = 0f;
		}

		private void HandleResetScore(object sender, ScoreMotorResetScoreEventArgs e)
		{
			var id = e.DisplayComponent.Id;

			if (!_displays.ContainsKey(id)) {
				Logger.Error($"invalid id, id={id}");
				return;
			}

			if (_running) {
				Logger.Info($"already running (ignoring reset), id={id}");
				return;
			}

			if (_displayScores[id] == 0) {
				Logger.Info($"score already 0 (ignoring reset), id={id}");
				e.Callback(0, 0);

				return;
			}

			_mode = ScoreMotorMode.Reset;
			_id = id;
			_increase = ScoreMotorComponent.MaxIncrease;
			_callback = e.Callback;

			Logger.Info($"reset, id={id}");

			StartMotor();
		}

		private void HandleAddPoints(object sender, ScoreMotorAddPointsEventArgs e)
		{
			var id = e.DisplayComponent.Id;

			if (!_displays.ContainsKey(id)) {
				Logger.Error($"invalid id, id={id}");
				return;
			}

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
				Logger.Error($"too many increases (ignoring points), id={id}, points={e.Points}, increase={increase}");
				return;
			}

			if (_running) {
				if (increase > 1 || (increase == 1 && _scoreMotorComponent.BlockScoring)) {
					Logger.Info($"already running (ignoring points), id={id}, points={e.Points}");
					return;
				}
			}

			if (increase == 1) {
				_displayScores[id] += e.Points;

				Logger.Info($"single points, id={id}, points={e.Points}");
				e.Callback(e.Points, _displayScores[id]);

				return;
			}

			_mode = ScoreMotorMode.AddPoints;
			_id = id;
			_increase = increase;
			_points = e.Points / increase;
			_callback = e.Callback;

			Logger.Info($"multi points, id={id}, increase={_increase}, points={e.Points}");

			StartMotor();
		}

		private void StartMotor()
		{ 
			_time = 0;
			_pos = 0;

			_running = true;

			MotorRunningSwitch.SetSwitch(true);

			Advance();

			_scoreMotorComponent.OnUpdate += HandleUpdate;
		}

		private void StopMotor()
		{
			_scoreMotorComponent.OnUpdate -= HandleUpdate;

			MotorRunningSwitch.SetSwitch(false);

			_running = false;
		}

		private void HandleUpdate(object sender, EventArgs eventArgs)
		{
			_time += Time.deltaTime;

			int currentPos = (int)(_degreesPerSecond * _time);

			while (_pos <= currentPos && _pos < _scoreMotorComponent.Degrees) {
				Advance();
			}

			if (_pos >= _scoreMotorComponent.Degrees) {
				if (_mode == ScoreMotorMode.Reset) {
					if (_displayScores[_id] > 0) {
						_time = 0;
						_pos = 0;

						Advance();

						return;
					}
				}

				StopMotor();
			}
		}

		void IApi.OnDestroy()
		{
			_scoreMotorComponent.OnUpdate -= HandleUpdate;

			_scoreMotorComponent.OnAttachDisplayComponent -= HandleAttachDisplayComponent;
			_scoreMotorComponent.OnResetScore -= HandleResetScore;
			_scoreMotorComponent.OnAddPoints -= HandleAddPoints;

			Logger.Info($"Destroying {_scoreMotorComponent.name}");
		}

		private void Increase()
		{
			switch(_mode) {
				case ScoreMotorMode.Reset:
					_displayScores[_id] = ResetScore(_id);

					Logger.Info($"increase, mode={_mode}, id={_id}, score={_displayScores[_id]}");
					_callback(0, _displayScores[_id]);

					break;

				case ScoreMotorMode.AddPoints:
					_displayScores[_id] += _points;

					Logger.Info($"increase, mode={_mode}, id={_id}, points={_points}, score={_displayScores[_id]}");
					_callback(_points, _displayScores[_id]);

					break;
			}
		}

		private void Advance()
		{
			if (_pos % _degreesPerStep == 0) {
				MotorStepSwitch.SetSwitch(true);

				var step = _pos / _degreesPerStep;
				var action = _scoreMotorComponent.ScoreMotorActionsList[_increase - 1].Actions[step];

				Logger.Info($"advance, pos={_pos}, time={_time}, increase={_increase}, step={step}, action={action}");

				if (action == ScoreMotorAction.Increase) {
					Increase();
				}
			}

			_pos++;
		}

		private float ResetScore(string id)
		{
			DisplayComponent displayComponent = _displays[id];

			if (displayComponent is ScoreReelDisplayComponent) {
				var score = _displayScores[id];

				// Truncate score to the amount of reels

				score = (float)(score % System.Math.Pow(10,
						((ScoreReelDisplayComponent)displayComponent).ReelObjects.Length));

				float newScore = 0;

				var pos = 0;
				while (score > 0) {
					var i = (int)(score % 10);
					if (i > 0 && i < 9)
					{
						newScore += (float)(System.Math.Pow(10, pos) * (i + 1));
					}
					score = (int)(score / 10);
					pos++;
				}

				return newScore;
			}

			return 0;
		}
	}
}

