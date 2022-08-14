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

// ReSharper disable InconsistentNaming

using System;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	[Serializable]
	public class ScoreMotorSwitch
	{
		public ScoreMotorSwitchType Type;
		public string Name;
		public string SwitchId;

		[Unit("\u00B0")]
		public int StartPos;

		[Unit("\u00B0")]
		public int EndPos;

		[Unit("\u00B0")]
		public int Freq = 1;

		[Unit("\u00B0")]
		public int Duration = 1;

		public GamelogicEngineSwitch Switch => new(SwitchId) { Description = Name };

		public ScoreMotorSwitch(ScoreMotorSwitchType type, string name, string switchId, int startPos, int endPos)
		{
			Type = type;
			Name = name;
			SwitchId = switchId;
			StartPos = startPos;
			EndPos = endPos;
		}

		public bool HasId => !string.IsNullOrEmpty(SwitchId);
		public void GenerateId() => SwitchId = $"switch_{Guid.NewGuid().ToString()[..8]}";
	}

	public enum ScoreMotorSwitchType
	{
		EnableBetween = 0, EnableEvery = 1
	}
}
