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

using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class DisplayPlayer
	{
		private readonly Dictionary<string, DmdAuthoring> _displays = new Dictionary<string, DmdAuthoring>();

		private Table _table;
		private IGamelogicEngine _gamelogicEngine;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public void Awake(Table table, IGamelogicEngine gamelogicEngine)
		{
			_table = table;
			_gamelogicEngine = gamelogicEngine;
		}

		public void OnStart()
		{
			if (_gamelogicEngine?.AvailableDisplays.Length > 0) {
				var dmds = Object.FindObjectsOfType<DmdAuthoring>();
				foreach (var display in _gamelogicEngine.AvailableDisplays) {
					_displays[display.Id] = dmds.FirstOrDefault(d => d.Id == display.Id);
					if (_displays[display.Id] != null) {
						_displays[display.Id].UpdateDimensions(display.Width, display.Height);
						_displays[display.Id].DisplayType = display.Type;

					} else {
						Logger.Error($"Cannot find DMD game object for display ${display.Id}");
					}
				}
				_gamelogicEngine.OnDisplayFrame += HandleFrameEvent;
			}
		}
		private void HandleFrameEvent(object sender, DisplayFrameData e)
		{
			if (_displays.ContainsKey(e.Id)) {
				_displays[e.Id].UpdateFrame(e.Data);
			}
		}

		public void OnDestroy()
		{
			if (_gamelogicEngine?.AvailableDisplays.Length > 0) {
				_gamelogicEngine.OnDisplayFrame -= HandleFrameEvent;
			}
		}
	}
}
