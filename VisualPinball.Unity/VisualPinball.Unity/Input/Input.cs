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

using UnityEngine.InputSystem;
using VisualPinball.Engine.VPT.Table;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class Input : MonoBehaviour
	{
		public Table Table { get; private set; }

		public Input()
		{
			var textFile = UnityEngine.Resources.Load<InputActionAsset>("VPE");

			Debug.Log(textFile.ToJson());

		}



		public static InputActionAsset GetDefaultInputActions()
		{
			var asset = ScriptableObject.CreateInstance<InputActionAsset>();

			var map = new InputActionMap("Cabinet Switches");
			map.AddAction("Upper Left Flipper", InputActionType.Button, "<Keyboard>/a");
			map.AddAction("Upper Right Flipper", InputActionType.Button, "<Keyboard>/quote");
			map.AddAction("Left Flipper", InputActionType.Button, "<Keyboard>/leftShift");
			map.AddAction("Right Flipper", InputActionType.Button, "<Keyboard>/rightShift");
			map.AddAction("Right Magnasave", InputActionType.Button, "<Keyboard>/rightCtrl");
			map.AddAction("Left Magnasave", InputActionType.Button, "<Keyboard>/leftCtrl");
			map.AddAction("Fire 1", InputActionType.Button, "<Keyboard>/leftCtrl");
			map.AddAction("Fire 2", InputActionType.Button, "<Keyboard>/rightAlt");
			map.AddAction("Front (buy-in)", InputActionType.Button, "<Keyboard>/2");
			map.AddAction("Start Game", InputActionType.Button, "<Keyboard>/1");
			map.AddAction("Plunger", InputActionType.Button, "<Keyboard>/enter");
			map.AddAction("Insert Coin Slot 1", InputActionType.Button, "<Keyboard>/3");
			map.AddAction("Insert Coin Slot 2", InputActionType.Button, "<Keyboard>/4");
			map.AddAction("Insert Coin Slot 3", InputActionType.Button, "<Keyboard>/5");
			map.AddAction("Insert Coin Slot 4", InputActionType.Button, "<Keyboard>/6");
			map.AddAction("Coin Door Open/Close", InputActionType.Button, "<Keyboard>/end");
			map.AddAction("Coin Door Cancel (WPC)", InputActionType.Button, "<Keyboard>/7");
			map.AddAction("Coin Door Down (WPC)", InputActionType.Button, "<Keyboard>/8");
			map.AddAction("Coin Door Up (WPC)", InputActionType.Button, "<Keyboard>/9");
			map.AddAction("Coin Door Advance", InputActionType.Button, "<Keyboard>/8");
			map.AddAction("Coin Door Up/Down", InputActionType.Button, "<Keyboard>/7");
			map.AddAction("Slam Tilt", InputActionType.Button, "<Keyboard>/home");

			asset.AddActionMap(map);

			return asset;
		}

	}
}
