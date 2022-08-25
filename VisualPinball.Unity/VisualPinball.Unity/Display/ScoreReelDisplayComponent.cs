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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
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

	[AddComponentMenu("Visual Pinball/Display/Score Reel")]
	public class ScoreReelDisplayComponent : DisplayComponent, ICoilDeviceComponent, ISwitchDeviceComponent
	{
		[SerializeField]
		public string _id = "display0";

		public override string Id { get => _id; set => _id = value; }

		[Unit("positions/s")]
		[Tooltip("Positions per second")]
		public float Speed = 15;

		[Unit("ms")]
		[Tooltip("Wait between positions in milliseconds")]
		public float Wait = 30;

		[Tooltip("The reel components, from left to right.")]
		public ScoreReelComponent[] ReelObjects;

		[Unit("\u00B0")]
		[Tooltip("The total number of degrees in one turn.")]
		public int Degrees = 120;

		[Unit("ms")]
		[Tooltip("Amount of time, in milliseconds to move one turn.")]
		public int Duration = 760;

		[Tooltip("The total number of steps per turn.")]
		public int Steps = 0;

		public List<ScoreMotorActions> ScoreMotorActionsList = new List<ScoreMotorActions>();

		public const string ResetCoilItem = "reset_coil";

		public const string MotorRunningSwitchItem = "motor_running_switch";
		public const string MotorStepSwitchItem = "motor_step_switch";
		public const string MotorTurnSwitchItem = "motor_turn_switch";

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(ResetCoilItem) {
				Description = "Reset Coil"
			}
		};

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(MotorRunningSwitchItem)
			{
				Description = "Motor Running Switch"
			},
			new GamelogicEngineSwitch(MotorStepSwitchItem)
			{
				Description = "Motor Step Switch",
				IsPulseSwitch = true
			},
			new GamelogicEngineSwitch(MotorTurnSwitchItem)
			{
				Description = "Motor Turn Switch",
				IsPulseSwitch = true
			}
		};

		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;
		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		public event EventHandler OnUpdate;
		public event EventHandler<DisplayScoreEventArgs> OnScore;

		#region Runtime

		private void Awake()
		{
			GetComponentInParent<Player>().RegisterScoreDisplayComponent(this);
		}

		#endregion

		private void Start()
		{
			foreach (var reelObject in ReelObjects) {
				reelObject.Speed = Speed;
				reelObject.Wait = Wait;
			}
		}

		private void Update()
		{
			OnUpdate?.Invoke(this, EventArgs.Empty);
		}

		public override void Clear()
		{
			foreach (var reelObject in ReelObjects) {
				reelObject.AnimateTo(0);
			}
		}

		public override void AddPoints(float points)
		{
			OnScore?.Invoke(this, new DisplayScoreEventArgs("", points));
		}

		public void UpdateScore(float score)
		{
			UpdateFrame(DisplayFrameFormat.Numeric, BitConverter.GetBytes(score));

			_displayPlayer.DisplayScoreEvent(this, score);
		}

		public override void UpdateFrame(DisplayFrameFormat format, byte[] data)
		{
			var score = (int)BitConverter.ToSingle(data);
			var digits = DigitArr(score);
			var j = digits.Length - 1;
			for (var i = ReelObjects.Length - 1; i >= 0; i--) {
				if (j < 0) {
					SetReel(ReelObjects[i], 0);
					j--;
					continue;
				}
				SetReel(ReelObjects[i], digits[j]);
				j--;
			}
		}

		private static void SetReel(ScoreReelComponent sr, int num)
		{
			sr.AnimateTo(num);
		}

		private static int NumDigits(int n) {
			if (n < 0) {
				n = n == int.MinValue ? int.MaxValue : -n;
			}
			return n switch {
				< 10 => 1,
				< 100 => 2,
				< 1000 => 3,
				< 10000 => 4,
				< 100000 => 5,
				< 1000000 => 6,
				< 10000000 => 7,
				< 100000000 => 8,
				< 1000000000 => 9,
				_ => 10
			};
		}

		private static int[] DigitArr(int n)
		{
			var result = new int[NumDigits(n)];
			for (var i = result.Length - 1; i >= 0; i--) {
				result[i] = n % 10;
				n /= 10;
			}
			return result;
		}

		#region Unused

		protected override Material CreateMaterial()
		{
			throw new NotImplementedException();
		}
		public override void UpdateDimensions(int width, int height, bool flipX = false)
		{
			Debug.Log($"Reel of {width} requested.");
		}

		public override Color LitColor { get; set; }
		public override Color UnlitColor { get; set; }
		protected override float MeshWidth { get; }
		public override float MeshHeight { get; }
		protected override float MeshDepth { get; }
		public override float AspectRatio { get; set; }

		#endregion
	}
}
