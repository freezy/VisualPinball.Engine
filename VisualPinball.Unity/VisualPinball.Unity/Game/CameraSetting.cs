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

using UnityEngine;

namespace VisualPinball.Unity
{
	[CreateAssetMenu(fileName = "CameraPreset", menuName = "Visual Pinball/Camera Preset", order = 1)]
	public class CameraSetting : ScriptableObject
	{
		public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

		public string displayName;
		public Vector3 offset = Vector3.zero;
		public float fov = 25f;
		public float distance = 1.7f;
		public float angle = 10f;
		public float orbit;

		public CameraSetting Clone() => CreateInstance<CameraSetting>().ApplyFrom(this);

		public CameraSetting ApplyFrom(CameraSetting setting)
		{
			if (!setting) {
				return this;
			}
			displayName = setting.DisplayName;
			offset = setting.offset;
			fov = setting.fov;
			distance = setting.distance;
			angle = setting.angle;
			orbit = setting.orbit;
			return this;
		}
	}
}
