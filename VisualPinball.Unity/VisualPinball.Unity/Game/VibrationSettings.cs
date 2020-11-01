// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using Unity.Entities;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class VibrationSettings : MonoBehaviour
	{
		//[Range(0, 0.5f)]
		public float speed = 0.1f;

		//[Range(0, 100f)]
		public float strength = 10f;

		public float duration = 1f;

		private VibrationSystem _vibrationSystem;

		private void Awake()
		{
			_vibrationSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VibrationSystem>();
		}

		private void Update()
		{
			_vibrationSystem.Speed = speed;
			_vibrationSystem.Strength = strength;
			_vibrationSystem.Duration = duration;
		}
	}
}
