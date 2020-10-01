﻿// Visual Pinball Engine
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

namespace VisualPinball.Unity
{
	public class InputManager
	{
		public static readonly string VPE_ACTION_CREATE_BALL = "Create Ball";
		public static readonly string VPE_ACTION_KICKER = "Kicker";

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

			map = new InputActionMap("Visual Pinball Engine");
			map.AddAction(VPE_ACTION_CREATE_BALL, InputActionType.Button, "<Keyboard>/b");
			map.AddAction(VPE_ACTION_KICKER, InputActionType.Button, "<Keyboard>/n");

			asset.AddActionMap(map);

			return asset;
		}
	}
}
