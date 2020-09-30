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

using System;

namespace VisualPinball.Engine.Game.Engine
{
	[Serializable]
	public class DefaultGamelogicEngine : IGamelogicEngine, IGamelogicEngineWithSwitches, IGamelogicEngineWithCoils
	{
		public string Name => "Default Game Engine";

		public string[] AvailableSwitches { get; } = {"s_left_flipper", "s_right_flipper", "s_plunger"};

		public string[] AvailableCoils { get; } = {"c_left_flipper", "c_right_flipper", "c_auto_plunger"};

		public event EventHandler<CoilEventArgs> OnCoilChanged;

		public void Switch(string id, bool normallyClosed)
		{
			switch (id) {
				case "s_left_flipper":
					OnCoilChanged?.Invoke(this, new CoilEventArgs("c_left_flipper", normallyClosed));
					break;
				case "s_right_flipper":
					OnCoilChanged?.Invoke(this, new CoilEventArgs("c_right_flipper", normallyClosed));
					break;
				case "s_plunger":
					OnCoilChanged?.Invoke(this, new CoilEventArgs("c_auto_plunger", normallyClosed));
					break;
			}
		}
	}
}
