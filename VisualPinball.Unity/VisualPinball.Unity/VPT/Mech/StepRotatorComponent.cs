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
using NLog;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Step Rotator")]
	public class StepRotatorComponent : MonoBehaviour, ISwitchDeviceComponent, ICoilDeviceComponent
	{
		#region Data

		public IRotatableComponent Target { get => _target as IRotatableComponent; set => _target = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(IRotatableComponent), PickerLabel = "Rotatable Objects")]
		[Tooltip("The target that will rotate.")]
		public MonoBehaviour _target;

		[Range(0f, 360f)]
		[Tooltip("Angle in degrees the object rotates until it changes rotation and goes back. It's the angle that corresponds to the number of steps below.")]
		public float TotalRotationDegrees = 65;

		[Min(0)]
		public int NumSteps;

		[Tooltip("On each mark, the switch changes are transmitted to the gamelogic engine.")]
		public StepRotatorMark[] Marks;

		[SerializeField]
		[TypeRestriction(typeof(IRotatableComponent), PickerLabel = "Rotatable Objects")]
		[Tooltip("Other objects at will rotate around the target.")]
		public MonoBehaviour[] _rotateWith;
		public IRotatableComponent[] RotateWith => _rotateWith.OfType<IRotatableComponent>().ToArray();

		#endregion

		#region Access

		internal IEnumerable<KickerComponent> Kickers => _rotateWith.OfType<KickerComponent>();

		#endregion

		#region Constants

		public const string MotorCoilItem = "motor_coil";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#endregion

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

		private Dictionary<IRotatableComponent, (float, float)> _rotatingObjectDistances = new();

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null)
			{
				Logger.Error($"Cannot find player for cannon {name}.");
				return;
			}

			foreach (var kicker in GetComponentsInChildren<KickerColliderComponent>()) {
				kicker.FallIn = false;
			}

			var pos = Target.RotatedPosition;
			_rotatingObjectDistances = RotateWith.ToDictionary(
				r => r,
				r => (
					math.distance(pos, r.RotatedPosition),
					math.sign(pos.x - r.RotatedPosition.x) * Vector2.Angle(r.RotatedPosition - pos, new float2(0f, -1f))
				)
			);

			player.RegisterStepRotator(this);
		}

		public void UpdateRotation(float value)
		{
			var angleDeg =  value * TotalRotationDegrees;

			Target.RotateZ = -angleDeg;

			var pos = Target.RotatedPosition;
			foreach (var obj in _rotatingObjectDistances.Keys) {
				var (distance, angle) = _rotatingObjectDistances[obj];
				obj.RotateZ = -angleDeg;
				obj.RotatedPosition = new float2(
					pos.x -distance * math.sin(math.radians(angleDeg + angle)),
					pos.y -distance * math.cos(math.radians(angleDeg + angle))
				);
			}
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
