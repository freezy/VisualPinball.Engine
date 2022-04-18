﻿// Visual Pinball Engine
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
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity
{
	[Serializable]
	public class LampMapping
	{
		public string Id = string.Empty;

		public LampSource Source = LampSource.Lamp;

		public int FadingSteps;

		public bool IsCoil;

		public string Description = string.Empty;

		[SerializeReference]
		public MonoBehaviour _device;
		public ILampDeviceComponent Device { get => _device as ILampDeviceComponent; set => _device = value as MonoBehaviour; }

		public string DeviceItem = string.Empty;

		public LampType Type = LampType.SingleOnOff;

		public ColorChannel Channel = ColorChannel.Alpha;
	}
}
