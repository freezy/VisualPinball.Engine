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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Engine.VPT.Trough
{
	public class Trough : Item<TroughData>, ISwitchableDevice, ICoilableDevice
	{
		public override string ItemName { get; } = "Trough";
		public override string ItemGroupName { get; } = null;

		public const string JamSwitchId = "jam";
		public const string EjectCoilId = "eject";
		public const string EntryCoilId = "entry";

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => Enumerable.Repeat(0, Data.SwitchCount)
			.Select((_, i) => new GamelogicEngineSwitch {Description = SwitchDescription(i), Id = $"{i + 1}"})
			.Concat( new[]{ new GamelogicEngineSwitch{Description = "Jam Switch", Id = JamSwitchId} });

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil {Description = "Entry", Id = EntryCoilId},
			new GamelogicEngineCoil {Description = "Eject", Id = EjectCoilId}
		};

		public Trough(TroughData data) : base(data)
		{
		}

		public Trough(BinaryReader reader, string itemName) : this(new TroughData(reader, itemName))
		{
		}

		private string SwitchDescription(int i)
		{
			if (i == 0) {
				return "Ball 1 (eject)";
			}

			return i == Data.SwitchCount - 1
				? $"Ball {i + 1} (entry)"
				: $"Ball {i + 1}";
		}

		public static Trough GetDefault(Table.Table table)
		{
			var primitiveData = new TroughData(table.GetNewName<Trough>("Trough"));
			return new Trough(primitiveData);
		}
	}
}
