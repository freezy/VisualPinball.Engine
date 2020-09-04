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

namespace VisualPinball.Unity.Patcher.Matcher.Table
{
	/// <summary>
	/// Matches by table name (how the table is called in the script). <p/>
	/// </summary>
	public class TableNameMatchAttribute : TableMatchAttribute
	{
		private readonly string _name;

		public TableNameMatchAttribute(string name)
		{
			_name = name;
		}

		public override bool Matches(Engine.VPT.Table.Table table, string fileName)
		{
			return _name == null || table.Data.Name == _name;
		}
	}
}
