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

using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Patcher
{
	/// <summary>
	/// Matches by Table Info (in Visual Pinball under the "Table" menu). <p/>
	///
	/// All of the provided fields must match. If none provided, it always matches.
	/// </summary>
	public class MetaMatchAttribute : TableMatchAttribute
	{
		public string TableName;
		public string AuthorName;

		public override bool Matches(FileTableContainer th, string fileName)
		{
			if (TableName != null && th.InfoName != TableName) {
				return false;
			}
			if (AuthorName != null && th.InfoAuthorName != AuthorName) {
				return false;
			}

			return true;
		}
	}
}
