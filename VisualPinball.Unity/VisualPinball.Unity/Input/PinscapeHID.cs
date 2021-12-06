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

using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace VisualPinball.Unity
{
	[InputControlLayout(stateType = typeof(PinscapeInputReport))]
#if UNITY_EDITOR
	[InitializeOnLoad]
#endif
	public class PinscapeHID : Gamepad
	{
		static PinscapeHID()
		{
			InputSystem.RegisterLayout<PinscapeHID>(
				matches: new InputDeviceMatcher()
					.WithInterface("HID")
					.WithCapability("vendorId", 0x54C)
					.WithCapability("productId", 0x9CC));
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void Init() {}
	}
}
