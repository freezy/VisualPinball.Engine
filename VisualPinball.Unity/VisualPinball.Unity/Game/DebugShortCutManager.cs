// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VisualPinball.Unity
{
	public class DebugShortcutManager : MonoBehaviour
	{
		[Serializable]
		public class DebugShortcut
		{
			public string description;
			public string itemId;
			public Key key;
			public DebugAction action;
			public float value;
			public PressMode mode;

			private float _previousValue = float.NaN;

			public float AlternatingValue { get {
					{
						if (mode == PressMode.Const) {
							return value;
						}
						// start with value
						if (float.IsNaN(_previousValue)) {
							_previousValue = value;
							return value;
						}
						// return inverted value
						// ReSharper disable once CompareOfFloatsByEqualityOperator
						if (_previousValue == InvValue) {
							_previousValue = value;
							return value;
						}
						_previousValue = InvValue;
						return InvValue;
					}
				}
			}

			public float InvValue => value == 0 ? 1 : 0;
		}

		public enum PressMode
		{
			/// <summary>
			/// Key press always sends the same signal.
			/// </summary>
			Const,

			/// <summary>
			/// Key press sens inverted signal every second time
			/// </summary>
			Toggle,

			/// <summary>
			/// Key down sends signal, key up inverted signal
			/// </summary>
			Momentary
		}

		public enum DebugAction
		{
			None,
			Coil,
			Switch,
			Lamp,
		}

		[Header("Debug Shortcuts")]
		public List<DebugShortcut> shortcuts = new();

		private IGamelogicEngine _gle;
		private IGamelogicBridge _gleBridge;
		private Keyboard _keyboard;

		private void Start()
		{
			_gle = GetComponentInParent<IGamelogicEngine>();
			_gleBridge = GetComponentInParent<IGamelogicBridge>();
			_keyboard = Keyboard.current;
		}

		private void Update()
		{
			if (_gle == null || _gleBridge == null) {
				return;
			}
			foreach (var shortcut in shortcuts) {
				if (_keyboard[shortcut.key].wasPressedThisFrame) {
					switch (shortcut.mode) {
						case PressMode.Const:
						case PressMode.Momentary:
							ExecuteAction(shortcut, shortcut.value);
							break;
						case PressMode.Toggle:
							ExecuteAction(shortcut, shortcut.AlternatingValue);
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
				}
				if (_keyboard[shortcut.key].wasReleasedThisFrame) {
					switch (shortcut.mode) {
						case PressMode.Const:
						case PressMode.Toggle:
							// do nothing
							break;
						case PressMode.Momentary:
							ExecuteAction(shortcut, shortcut.AlternatingValue);
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
				}
			}
		}

		private void ExecuteAction(DebugShortcut shortcut, float value)
		{
			switch (shortcut.action) {
				case DebugAction.Coil:
					_gleBridge.SetCoil(shortcut.itemId, value != 0);
					break;

				case DebugAction.Switch:
					_gle.Switch(shortcut.itemId, value != 0);
					break;

				case DebugAction.Lamp:
					_gleBridge.SetLamp(shortcut.itemId, value);
					break;

				case DebugAction.None:
				default:
					break;
			}
		}
	}
}
