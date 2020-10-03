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

namespace VisualPinball.Unity.Editor
{
	public class CollectionListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, HeaderName = "Index", Width = 50)]
		public string Index => $"{CollectionData?.StorageIndex ?? -1:D3}";

		[ManagerListColumn(Order = 0, Width = 200)]
		public string Name => CollectionData?.Name ?? "";

		[ManagerListColumn(Order = 1, HeaderName = "Nb Items", Width = 100)]
		public int NbItems => CollectionData?.ItemNames?.Length ?? 0;

		[ManagerListColumn(Order = 2, HeaderName = "Fire Events", Width = 100)]
		public bool FireEvents => CollectionData?.FireEvents ?? false;

		[ManagerListColumn(Order = 3, HeaderName = "Group Elements", Width = 100)]
		public bool GroupElements => CollectionData?.GroupElements ?? false;

		[ManagerListColumn(Order = 4, HeaderName = "Stop Single Events", Width = 100)]
		public bool StopSingleEvents => CollectionData?.StopSingleEvents ?? false;

		public Engine.VPT.Collection.CollectionData CollectionData;
	}
}
