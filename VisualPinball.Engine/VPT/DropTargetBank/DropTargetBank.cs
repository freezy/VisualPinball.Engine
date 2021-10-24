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

using System.IO;

namespace VisualPinball.Engine.VPT.DropTargetBank
{
	public class DropTargetBank : Item<DropTargetBankData>
	{
		public override string ItemGroupName => null;

		public DropTargetBank(DropTargetBankData data) : base(data)
		{
		}

		public DropTargetBank(BinaryReader reader, string itemName) : this(new DropTargetBankData(reader, itemName))
		{
		}

		public static DropTargetBank GetDefault(Table.Table table)
		{
			var data = new DropTargetBankData(table.GetNewName<DropTargetBank>("DropTargetBank"));
			return new DropTargetBank(data);
		}
	}
}