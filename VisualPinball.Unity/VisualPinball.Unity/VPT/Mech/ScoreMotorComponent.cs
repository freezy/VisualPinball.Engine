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
	public delegate void ScoreMotorActionCallback(float points, float score);

	public readonly struct ScoreMotorResetScoreEventArgs
	{
		public readonly DisplayComponent DisplayComponent;
		public readonly ScoreMotorActionCallback Callback;

		public ScoreMotorResetScoreEventArgs(DisplayComponent displayComponent, ScoreMotorActionCallback callback)
		{
			DisplayComponent = displayComponent;
			Callback = callback;
		}
	}

	public readonly struct ScoreMotorAddPointsEventArgs
	{
		public readonly DisplayComponent DisplayComponent;
		public readonly float Points;
		public readonly ScoreMotorActionCallback Callback;

		public ScoreMotorAddPointsEventArgs(DisplayComponent displayComponent, float points, ScoreMotorActionCallback callback)
		{
			DisplayComponent = displayComponent;
			Points = points;
			Callback = callback;
		}
	}

	public readonly struct ScoreMotorRegisterDisplayEventArgs
	{
		public readonly DisplayComponent DisplayComponent;

		public ScoreMotorRegisterDisplayEventArgs(DisplayComponent displayComponent)
		{
			DisplayComponent = displayComponent;
		}
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

	[Serializable]
	public class ScoreMotorActions
	{
		public List<ScoreMotorAction> Actions = new List<ScoreMotorAction>();
	}

	[AddComponentMenu("Visual Pinball/Game Item/Score Motor")]
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

		public List<ScoreMotorActions> ScoreMotorActionsList = new List<ScoreMotorActions>()
		{
			new ScoreMotorActions(),
			new ScoreMotorActions(),
			new ScoreMotorActions(),
			new ScoreMotorActions(),
			new ScoreMotorActions()
		};

		public const string MotorRunningSwitchItem = "motor_running_switch";
		public const string MotorStepSwitchItem = "motor_step_switch";

		public event EventHandler OnUpdate;
		public event EventHandler<ScoreMotorRegisterDisplayEventArgs> OnRegisterDisplay;
		public event EventHandler<ScoreMotorResetScoreEventArgs> OnResetScore;
		public event EventHandler<ScoreMotorAddPointsEventArgs> OnAddPoints;

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

		#region Runtime

		private void Awake()
		{
			GetComponentInParent<Player>().RegisterScoreMotorComponent(this);
		}

		private void Update()
		{
			OnUpdate?.Invoke(this, EventArgs.Empty);
		}

		public void RegisterDisplay(DisplayComponent displayComponent)
		{
			OnRegisterDisplay?.Invoke(this, new ScoreMotorRegisterDisplayEventArgs(displayComponent));
		}

		public void ResetScore(DisplayComponent displayComponent, ScoreMotorActionCallback callback)
		{
			OnResetScore?.Invoke(this, new ScoreMotorResetScoreEventArgs(displayComponent, callback));
		}

		public void AddPoints(DisplayComponent displayComponent, float points, ScoreMotorActionCallback callback)
		{
			OnAddPoints?.Invoke(this, new ScoreMotorAddPointsEventArgs(displayComponent, points, callback));
		}

		#endregion
	}
}