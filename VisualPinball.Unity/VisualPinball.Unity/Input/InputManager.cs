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

using NLog;
using Logger = NLog.Logger;
using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using System;
using System.Collections.Generic;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	public class InputManager
	{
		private static readonly string RESOURCE_NAME = "InputBindings";

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
			var map = new InputActionMap(InputConstants.MapCabinetSwitches);
			map.AddAction(InputConstants.ActionUpperLeftFlipper, InputActionType.Button, "<Keyboard>/a");
			map.AddAction(InputConstants.ActionUpperRightFlipper, InputActionType.Button, "<Keyboard>/quote");
			map.AddAction(InputConstants.ActionLeftFlipper, InputActionType.Button, "<Keyboard>/leftShift").AddBinding("<Gamepad>/leftShoulder");
			map.AddAction(InputConstants.ActionRightFlipper, InputActionType.Button, "<Keyboard>/rightShift").AddBinding("<Gamepad>/rightShoulder");
			map.AddAction(InputConstants.ActionRightMagnasave, InputActionType.Button, "<Keyboard>/rightCtrl");
			map.AddAction(InputConstants.ActionLeftMagnasave, InputActionType.Button, "<Keyboard>/leftCtrl");
			map.AddAction(InputConstants.ActionFire1, InputActionType.Button, "<Keyboard>/leftCtrl");
			map.AddAction(InputConstants.ActionFire2, InputActionType.Button, "<Keyboard>/rightAlt");
			map.AddAction(InputConstants.ActionFrontBuyIn, InputActionType.Button, "<Keyboard>/2");
			map.AddAction(InputConstants.ActionStartGame, InputActionType.Button, "<Keyboard>/1");
			map.AddAction(InputConstants.ActionPlunger, InputActionType.Button, "<Keyboard>/enter");
			map.AddAction(InputConstants.ActionPlungerAnalog, InputActionType.Button, "<Gamepad>/rightStick/down");
			map.AddAction(InputConstants.ActionInsertCoin1, InputActionType.Button, "<Keyboard>/3");
			map.AddAction(InputConstants.ActionInsertCoin2, InputActionType.Button, "<Keyboard>/4");
			map.AddAction(InputConstants.ActionInsertCoin3, InputActionType.Button, "<Keyboard>/5");
			map.AddAction(InputConstants.ActionInsertCoin4, InputActionType.Button, "<Keyboard>/6");
			map.AddAction(InputConstants.ActionCoinDoorOpenClose, InputActionType.Button, "<Keyboard>/end");
			map.AddAction(InputConstants.ActionCoinDoorCancel, InputActionType.Button, "<Keyboard>/7");
			map.AddAction(InputConstants.ActionCoinDoorDown, InputActionType.Button, "<Keyboard>/8");
			map.AddAction(InputConstants.ActionCoinDoorUp, InputActionType.Button, "<Keyboard>/9");
			map.AddAction(InputConstants.ActionCoinDoorEnter, InputActionType.Button, "<Keyboard>/0");
			map.AddAction(InputConstants.ActionCoinDoorAdvance, InputActionType.Button, "<Keyboard>/8");
			map.AddAction(InputConstants.ActionCoinDoorUpDown, InputActionType.Button, "<Keyboard>/7");
			map.AddAction(InputConstants.ActionSlamTilt, InputActionType.Button, "<Keyboard>/home");
			map.AddAction(InputConstants.ActionSelfTest, InputActionType.Button, "<Keyboard>/8");

			asset.AddActionMap(map);

			map = new InputActionMap(InputConstants.MapDebug);
			map.AddAction(InputConstants.ActionCreateBall, InputActionType.Button, "<Keyboard>/b");
			map.AddAction(InputConstants.ActionKicker, InputActionType.Button, "<Keyboard>/n");
			map.AddAction(InputConstants.ActionSlowMotion, InputActionType.Button, "<Keyboard>/s").AddBinding("<Gamepad>/leftStick/down");
			map.AddAction(InputConstants.ActionTimeLapse, InputActionType.Button, "<Gamepad>/leftStick/up");

			asset.AddActionMap(map);

			return asset;
		}
	}
}
