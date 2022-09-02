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
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public delegate void ScoreMotorResetCallback(float score);
	public delegate void ScoreMotorAddPointsCallback(float points);

	[AddComponentMenu("Visual Pinball/Game Item/Score Motor")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/score-motor.html")]
	public class ScoreMotorComponent : MonoBehaviour, ISwitchDeviceComponent
	{
		public const int MaxIncrease = 5;

		[Unit("\u00B0")]
		[Tooltip("The total number of degrees in one turn.")]
		public int Degrees = 120;

		[Unit("ms")]
		[Tooltip("Amount of time, in milliseconds to move one turn.")]
		public int Duration = 769;

		[Tooltip("The total number of steps per turn.")]
		[Min(MaxIncrease)]
		public int Steps = 6;

		[Tooltip("Disable to allow single point scores while score motor running.")]
		public bool BlockScoring = true;

		public List<ScoreMotorTiming> ScoreMotorTimingList = new List<ScoreMotorTiming>() {
			new ScoreMotorTiming(),
			new ScoreMotorTiming(),
			new ScoreMotorTiming(),
			new ScoreMotorTiming(),
			new ScoreMotorTiming()
		};

		public const string MotorRunningSwitchItem = "motor_running_switch";
		public const string MotorStepSwitchItem = "motor_step_switch";

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(MotorRunningSwitchItem)
			{
				Description = "Motor Running Switch"
			},
			new GamelogicEngineSwitch(MotorStepSwitchItem)
			{
				Description = "Motor Step Switch",
				IsPulseSwitch = true
			}
		};

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;
		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		public event EventHandler<SwitchEventArgs2> OnSwitchChanged;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private int DegreesPerStep => Degrees / Steps;
		private float DegreesPerSecond => Degrees / (Duration / 1000f);

		private bool _isRunning;

		private float _time;
		private int _pos;

		private ScoreMotorMode _mode;
		private string _id;
		private int _increase;

		private float _score;
		private ScoreMotorResetCallback _resetCallback;

		private float _points;
		private ScoreMotorAddPointsCallback _addPointsCallback;

		#region Runtime

		private void Awake()
		{
			GetComponentInParent<Player>().RegisterScoreMotorComponent(this);
		}

		private void Switch(string id, bool isClosed)
		{
			OnSwitchChanged?.Invoke(this, new SwitchEventArgs2(id, isClosed));
		}

		private void Update()
		{
			if (!_isRunning) {
				return;
			}

			_time += Time.deltaTime;

			var currentPos = (int)(DegreesPerSecond * _time);

			while (_pos <= currentPos && _pos < Degrees) {
				AdvanceMotor();
			}

			if (_pos >= Degrees) {
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

		public void ResetScore(string id, float score, ScoreMotorResetCallback callback)
		{
			if (_isRunning) {
				Logger.Debug($"already running (ignoring reset), id={id}");
				return;
			}

			if (score == 0) {
				Logger.Debug($"score already 0 (ignoring reset), id={id}");
				callback(0);
				return;
			}

			Logger.Debug($"reset, id={id}, score={score}");

			_mode = ScoreMotorMode.Reset;
			_id = id;
			_score = score;
			_increase = MaxIncrease;
			_resetCallback = callback;

			StartMotor();
		}

		public void AddPoints(string id, float points, ScoreMotorAddPointsCallback callback)
		{
			var increase = (int)System.Math.Floor(points / System.Math.Pow(10, System.Math.Floor(System.Math.Log10(points))));

			if (increase > MaxIncrease) {
				Logger.Error($"too many increases (ignoring points), id={id}, points={points}, increase={increase}");
				return;
			}

			if (_isRunning) {
				if (increase > 1 || (increase == 1 && BlockScoring)) {
					Logger.Debug($"already running (ignoring points), id={id}, points={points}");
					return;
				}
			}

			if (increase == 1) {
				Logger.Debug($"single points, id={id}, points={points}");
				callback(points);
				return;
			}

			Logger.Debug($"multi points, id={id}, increase={increase}, points={points}");

			_mode = ScoreMotorMode.AddPoints;
			_id = id;
			_increase = increase;
			_points = points / increase;
			_addPointsCallback = callback;

			StartMotor();
		}

		private void StartMotor()
		{
			Logger.Debug($"start motor");

			_time = 0;
			_pos = 0;

			_isRunning = true;

			Switch(MotorRunningSwitchItem, true);

			AdvanceMotor();
		}

		private void AdvanceMotor()
		{
			if (_pos % DegreesPerStep == 0) {
				Switch(MotorStepSwitchItem, true);

				var step = _pos / DegreesPerStep;
				var action = ScoreMotorTimingList[_increase - 1].Actions[step];

				Logger.Debug($"advance motor, pos={_pos}, time={_time}, increase={_increase}, step={step}, action={action}");

				if (action == ScoreMotorAction.Increase) {
					Increase();
				}
			}

			_pos++;
		}

		private void StopMotor()
		{
			Logger.Debug($"stop motor");

			_isRunning = false;

			Switch(MotorRunningSwitchItem, false);
		}

		private void Increase()
		{
			switch (_mode) {
				case ScoreMotorMode.Reset:
					_score = ResetScore(_score);
					Logger.Debug($"increase, mode={_mode}, id={_id}, score={_score}");
					_resetCallback(_score);
					break;

				case ScoreMotorMode.AddPoints:
					Logger.Debug($"increase, mode={_mode}, id={_id}, points={_points}");
					_addPointsCallback(_points);
					break;
			}
		}

		private float ResetScore(float score)
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

		#endregion
	}

	[Serializable]
	public class ScoreMotorTiming
	{
		public List<ScoreMotorAction> Actions = new List<ScoreMotorAction>();
	}

	public enum ScoreMotorMode
	{
		Reset = 0,
		AddPoints = 1
	}

	public enum ScoreMotorAction
	{
		Wait = 0,
		Increase = 1
	}
}
