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

using System;
using UnityEngine;

namespace VisualPinball.Unity
{
	[RequireComponent(typeof(RotatorComponent))]
	public class CannonRotatorComponent : MonoBehaviour
	{
		#region Data

		public IMechHandler Mech { get => _mech as IMechHandler; set => _mech = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(IMechHandler), PickerLabel = "Mech Handlers")]
		[Tooltip("The mech that will provide the values.")]
		public MonoBehaviour _mech;

		public float Factor = 0.3f;

		#endregion

		[NonSerialized] private RotatorComponent _rotator;
		[NonSerialized] private float _lastSpeed;
		[NonSerialized] private float _lastPosition;

		#region Runtime

		private void Start()
		{
			_rotator = GetComponent<RotatorComponent>();
			if (Mech != null) {
				Mech.OnMechUpdate += UpdateCannon;
			}
		}
		private void UpdateCannon(object sender, MechEventArgs e)
		{
			if (_lastSpeed == e.Speed && _lastPosition == e.Position) {
				return;
			}
			if (_lastSpeed == 0f && e.Speed > 0) {
				_rotator.StartRotating();
			}
			_rotator.UpdateRotation(e.Position * Factor);

			_lastPosition = e.Position;
			_lastSpeed = e.Speed;
		}

		private void OnDestroy()
		{
			if (Mech != null) {
				Mech.OnMechUpdate -= UpdateCannon;
			}
		}
		#endregion
	}
}
