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

using VisualPinball.Engine.VPT.Sound;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Scriptable object wrapper for plain VPX sound data. This will allow us to operate on sound data one a time
	/// for things like undo tracking, rather than needing to serialize the whole table (sidecar) and everything on it
	/// </summary>
	public class TableSerializedSound : TableSerializedData<SoundData>
	{
		public static TableSerializedSound Create(SoundData data) => GenericCreate<TableSerializedSound>(data);
	}
}
