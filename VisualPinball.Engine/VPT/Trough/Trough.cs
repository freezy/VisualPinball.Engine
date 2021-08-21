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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Engine.VPT.Trough
{
	public class Trough : Item<TroughData>
	{
		public override string ItemName => "Trough";
		public override string ItemGroupName => null;

		public Trough(TroughData data) : base(data)
		{
		}

		public Trough(BinaryReader reader, string itemName) : this(new TroughData(reader, itemName))
		{
		}



		public static Trough GetDefault(Table.Table table)
		{
			var primitiveData = new TroughData(table.GetNewName<Trough>("Trough"));
			return new Trough(primitiveData);
		}
	}
}
