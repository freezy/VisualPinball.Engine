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

// ReSharper disable InconsistentNaming

using System;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	[Serializable]
	public class MechMark
	{
		public MechMarkSwitchType Type;
		public string Name;
		public string SwitchId;
		public int StepBeginning;
		public int StepEnd;
		[Unit("step(s)")]
		public int PulseFreq = 1;
		[Unit("ms")]
		public int PulseDuration = 50;

		public GamelogicEngineSwitch Switch => new(SwitchId) { Description = Name };

		public MechMark(MechMarkSwitchType type, string name, string switchId, int stepBeginning, int stepEnd)
		{
			Type = type;
			Name = name;
			SwitchId = switchId;
			StepBeginning = stepBeginning;
			StepEnd = stepEnd;
		}

		public bool HasId => !string.IsNullOrEmpty(SwitchId);
		public void GenerateId() => SwitchId = $"switch_{Guid.NewGuid().ToString()[..8]}";
	}

	public enum MechMarkSwitchType
	{
		EnableBetween = 0, AlwaysPulse = 1, PulseBetween = 2
	}
}
