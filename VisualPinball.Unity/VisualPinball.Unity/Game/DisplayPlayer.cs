// Visual Pinball Engine
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

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public void Awake(IGamelogicEngine gamelogicEngine)
		{
			_gamelogicEngine = gamelogicEngine;

			_gamelogicEngine.OnDisplaysRequested += HandleDisplaysRequested;
			_gamelogicEngine.OnDisplayUpdateFrame += HandleDisplayUpdateFrame;
			_gamelogicEngine.OnDisplayAddPoints += HandleDisplayAddPoints;

			var displays = UnityEngine.Object.FindObjectsOfType<DisplayComponent>();
			foreach (var display in displays) {
				Logger.Info($"[Player] display \"{display.Id}\" connected.");

				display._displayPlayer = this;
				_displayGameObjects[display.Id] = display;
			}
		}

		private void HandleDisplaysRequested(object sender, RequestedDisplays requestedDisplays)
		{
			foreach (var display in requestedDisplays.Displays) {
				if (_displayGameObjects.ContainsKey(display.Id)) {
					Logger.Info($"Updating display \"{display.Id}\" to {display.Width}x{display.Height}");
					_displayGameObjects[display.Id].UpdateDimensions(display.Width, display.Height, display.FlipX);
					_displayGameObjects[display.Id].Clear();
				} else {
					Logger.Warn($"Cannot find game object for display \"{display.Id}\"");
				}
			}
		}

		private void HandleDisplayUpdateFrame(object sender, DisplayFrameData e)
		{
			if (_displayGameObjects.ContainsKey(e.Id)) {
				_displayGameObjects[e.Id].UpdateFrame(e.Format, e.Data);
			}
		}

		private void HandleDisplayAddPoints(object sender, DisplayAddPointsData e)
		{
			if (_displayGameObjects.ContainsKey(e.Id)) {
				_displayGameObjects[e.Id].AddPoints(e.Points);
			}
		}

		public void DisplayScoreEvent(DisplayComponent display, float score)
		{
			var id = _displayGameObjects.FirstOrDefault(x => x.Value == display).Key;

			if (id != null) {
				_gamelogicEngine.DisplayScoreEvent(id, score);
			}
		}

		public void OnDestroy()
		{
			_gamelogicEngine.OnDisplaysRequested -= HandleDisplaysRequested;
			_gamelogicEngine.OnDisplayUpdateFrame -= HandleDisplayUpdateFrame;
			_gamelogicEngine.OnDisplayAddPoints -= HandleDisplayAddPoints;
		}
	}
}
