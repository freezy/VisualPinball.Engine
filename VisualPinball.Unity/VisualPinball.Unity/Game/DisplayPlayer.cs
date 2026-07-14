// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using System.Linq;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class DisplayPlayer
	{
		private IGamelogicEngine _gamelogicEngine;
		private readonly Dictionary<string, DisplayComponent> _displayGameObjects = new Dictionary<string, DisplayComponent>();
		private readonly Dictionary<string, DisplayConfig> _displayConfigs = new Dictionary<string, DisplayConfig>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public void Awake(IGamelogicEngine gamelogicEngine)
		{
			_gamelogicEngine = gamelogicEngine;

			_gamelogicEngine.OnDisplaysRequested += HandleDisplaysRequested;
			_gamelogicEngine.OnDisplayClear += HandleDisplayClear;
			_gamelogicEngine.OnDisplayUpdateFrame += HandleDisplayUpdateFrame;

			var displays = UnityEngine.Object.FindObjectsOfType<DisplayComponent>();
			foreach (var display in displays) {
				Logger.Info($"[Player] display \"{display.Id}\" connected.");

				_displayGameObjects[display.Id] = display;
				_displayGameObjects[display.Id].OnDisplayChanged += HandleDisplayChanged;
			}
		}

		private void HandleDisplaysRequested(object sender, RequestedDisplays requestedDisplays)
		{
			foreach (var display in requestedDisplays.Displays) {
				if (_displayGameObjects.TryGetValue(display.Id, out var displayGameObject)) {
					// When another subscriber (e.g. the DMD bridge's in-scene destination) owns this
					// display, it reconfigures and feeds the component itself. Touching it here would
					// resize the texture and blank it via Clear(), causing a flicker — so skip it.
					if (!displayGameObject.ReceiveGamelogicFrames) {
						continue;
					}
					if (_displayConfigs.TryGetValue(display.Id, out var previous) && SameConfig(previous, display)) {
						Logger.Debug($"Ignoring unchanged configuration for display \"{display.Id}\".");
						continue;
					}
					Logger.Info($"Updating display \"{display.Id}\" to {display.Width}x{display.Height}");
					displayGameObject.UpdateDimensions(display.Width, display.Height, display.FlipX);
					if (display.LitColor.HasValue) {
						displayGameObject.UpdateColor(display.LitColor.Value);
					}
					if (display.UnlitColor.HasValue) {
						displayGameObject.UnlitColor = display.UnlitColor.Value;
					}
					displayGameObject.Clear();
					_displayConfigs[display.Id] = display;
				} else {
					Logger.Warn($"Cannot find game object for display \"{display.Id}\"");
				}
			}
		}

		internal static bool SameConfig(DisplayConfig first, DisplayConfig second)
		{
			return first.Id == second.Id && first.Width == second.Width && first.Height == second.Height &&
			       first.FlipX == second.FlipX && first.LitColor.Equals(second.LitColor) &&
			       first.UnlitColor.Equals(second.UnlitColor);
		}

		private void HandleDisplayClear(object sender, string id)
		{
			if (_displayGameObjects.TryGetValue(id, out var display) && display.ReceiveGamelogicFrames) {
				display.Clear();
			}
		}

		private void HandleDisplayUpdateFrame(object sender, DisplayFrameData e)
		{
			if (_displayGameObjects.TryGetValue(e.Id, out var display) && display.ReceiveGamelogicFrames) {
				display.UpdateFrame(e.Format, e.Data);
			}
		}

		private void HandleDisplayChanged(object sender, DisplayFrameData e)
		{
			_gamelogicEngine.DisplayChanged(e);
		}

		public void OnDestroy()
		{
			_gamelogicEngine.OnDisplaysRequested -= HandleDisplaysRequested;
			_gamelogicEngine.OnDisplayClear -= HandleDisplayClear;
			_gamelogicEngine.OnDisplayUpdateFrame -= HandleDisplayUpdateFrame;

			foreach (var id in _displayGameObjects.Keys) {
				_displayGameObjects[id].OnDisplayChanged -= HandleDisplayChanged;
			}
			_displayConfigs.Clear();
		}
	}
}
