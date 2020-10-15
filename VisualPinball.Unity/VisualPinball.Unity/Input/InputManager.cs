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

using NLog;
using Logger = NLog.Logger;
using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using System;
using System.Collections.Generic;
using Unity.Entities.UniversalDelegates;

namespace VisualPinball.Unity
{
	public class InputManager
	{
		public const string MapCabinetSwitches = "Cabinet Switches";
		public const string MapDebug = "Visual Pinball Engine";

		public const string ActionCreateBall = "Create Ball";
		public const string ActionKicker = "Kicker";

		public const string ActionUpperLeftFlipper = "Upper Left Flipper";
		public const string ActionUpperRightFlipper = "Upper Right Flipper";
		public const string ActionLeftFlipper = "Left Flipper";
		public const string ActionRightFlipper = "Right Flipper";
		public const string ActionRightMagnasave = "Right Magnasave";
		public const string ActionLeftMagnasave = "Left Magnasave";
		public const string ActionFire1 = "Fire 1";
		public const string ActionFire2 = "Fire 2";
		public const string ActionFrontBuyIn = "Front (buy-in)";
		public const string ActionStartGame = "Start Game";
		public const string ActionPlunger = "Plunger";
		public const string ActionInsertCoin1 = "Insert Coin Slot 1";
		public const string ActionInsertCoin2 = "Insert Coin Slot 2";
		public const string ActionInsertCoin3 = "Insert Coin Slot 3";
		public const string ActionInsertCoin4 = "Insert Coin Slot 4";
		public const string ActionCoinDoorOpenClose = "Coin Door Open/Close";
		public const string ActionCoinDoorCancel = "Coin Door Cancel (WPC)";
		public const string ActionCoinDoorDown = "Coin Door Down (WPC)";
		public const string ActionCoinDoorUp = "Coin Door Up (WPC)";
		public const string ActionCoinDoorAdvance = "Coin Door Advance";
		public const string ActionCoinDoorUpDown = "Coin Door Up/Down";
		public const string ActionSlamTilt = "Slam Tilt";

		private static readonly string RESOURCE_NAME = "VPE";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private InputActionAsset _asset;

		public InputManager()
		{
			_asset = UnityEngine.Resources.Load<InputActionAsset>(RESOURCE_NAME);

			if (_asset == null)
			{
				_asset = GetDefaultInputActionAsset();
			}
		}

		public InputManager(string directory)
		{
			try
			{
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				var path = directory + "/" + RESOURCE_NAME + ".inputactions";

				if (File.Exists(path))
				{
					_asset = InputActionAsset.FromJson(File.ReadAllText(path));
				}
				else
				{
					_asset = GetDefaultInputActionAsset();

					File.WriteAllText(path, _asset.ToJson());
				}
			}

			catch(Exception e)
			{
				Logger.Error(e);
			}

			if (_asset == null)
			{
				_asset = GetDefaultInputActionAsset();
			}
		}

		public void Enable(Action<object, InputActionChange> action)
		{
			_asset.Enable();
			InputSystem.onActionChange += action;
		}

		public void Disable(Action<object, InputActionChange> action)
		{
			InputSystem.onActionChange -= action;
			_asset.Disable();
		}

		public List<string> GetActionMapNames()
		{
			List<string> list = new List<string>();

			foreach (var map in _asset.actionMaps)
			{
				list.Add(map.name);
			}

			return list;
		}

		public List<string> GetActionNames(string mapName)
		{
			List<string> list = new List<string>();

			var map = _asset.FindActionMap(mapName);

			if (map != null)
			{
				foreach (var action in map)
				{
					list.Add(action.name);
				}
			}

			return list;
		}

		public static InputActionAsset GetDefaultInputActionAsset()
		{
			var asset = ScriptableObject.CreateInstance<InputActionAsset>();

			var map = new InputActionMap(MapCabinetSwitches);
			map.AddAction(ActionUpperLeftFlipper, InputActionType.Button, "<Keyboard>/a");
			map.AddAction(ActionUpperRightFlipper, InputActionType.Button, "<Keyboard>/quote");
			map.AddAction(ActionLeftFlipper, InputActionType.Button, "<Keyboard>/leftShift");
			map.AddAction(ActionRightFlipper, InputActionType.Button, "<Keyboard>/rightShift");
			map.AddAction(ActionRightMagnasave, InputActionType.Button, "<Keyboard>/rightCtrl");
			map.AddAction(ActionLeftMagnasave, InputActionType.Button, "<Keyboard>/leftCtrl");
			map.AddAction(ActionFire1, InputActionType.Button, "<Keyboard>/leftCtrl");
			map.AddAction(ActionFire2, InputActionType.Button, "<Keyboard>/rightAlt");
			map.AddAction(ActionFrontBuyIn, InputActionType.Button, "<Keyboard>/2");
			map.AddAction(ActionStartGame, InputActionType.Button, "<Keyboard>/1");
			map.AddAction(ActionPlunger, InputActionType.Button, "<Keyboard>/enter");
			map.AddAction(ActionInsertCoin1, InputActionType.Button, "<Keyboard>/3");
			map.AddAction(ActionInsertCoin2, InputActionType.Button, "<Keyboard>/4");
			map.AddAction(ActionInsertCoin3, InputActionType.Button, "<Keyboard>/5");
			map.AddAction(ActionInsertCoin4, InputActionType.Button, "<Keyboard>/6");
			map.AddAction(ActionCoinDoorOpenClose, InputActionType.Button, "<Keyboard>/end");
			map.AddAction(ActionCoinDoorCancel, InputActionType.Button, "<Keyboard>/7");
			map.AddAction(ActionCoinDoorDown, InputActionType.Button, "<Keyboard>/8");
			map.AddAction(ActionCoinDoorUp, InputActionType.Button, "<Keyboard>/9");
			map.AddAction(ActionCoinDoorAdvance, InputActionType.Button, "<Keyboard>/8");
			map.AddAction(ActionCoinDoorUpDown, InputActionType.Button, "<Keyboard>/7");
			map.AddAction(ActionSlamTilt, InputActionType.Button, "<Keyboard>/home");

			asset.AddActionMap(map);

			map = new InputActionMap(MapDebug);
			map.AddAction(ActionCreateBall, InputActionType.Button, "<Keyboard>/b");
			map.AddAction(ActionKicker, InputActionType.Button, "<Keyboard>/n");

			asset.AddActionMap(map);

			return asset;
		}
	}
}
