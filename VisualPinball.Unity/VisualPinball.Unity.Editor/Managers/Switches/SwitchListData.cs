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

using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.MappingConfig;

namespace VisualPinball.Unity.Editor
{
	public enum SwitchEvent
	{
		None = 0,
		KeyDown = 1,
		KeyUp = 2,
		Hit = 3,
		UnHit = 4
	}

	public class SwitchListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, HeaderName = "ID", Width = 120)]
		public string Name => ID;

		[ManagerListColumn(Order = 1, HeaderName = "Description", Width = 120)]
		public string Description;

		[ManagerListColumn(Order = 2, HeaderName = "Source", Width = 120)]
		public int Source;

		[ManagerListColumn(Order = 3, HeaderName = "Element", Width = 150)]
		public string Element;

		[ManagerListColumn(Order = 4, HeaderName = "Type", Width = 100)]
		public int Type = SwitchType.OnOff;

		[ManagerListColumn(Order = 5, HeaderName = "Trigger", Width = 100)]
		public SwitchEvent Trigger;

		[ManagerListColumn(Order = 6, HeaderName = "Off", Width = 100)]
		public string Off = "";

		public string ID;
		public int Constant = SwitchConstant.NormallyClosed;
		public int Pulse = 10;

		public MappingEntryData MappingEntryData;

		public SwitchListData(MappingEntryData mappingEntryData) {
			ID = mappingEntryData.ID;
			Description = mappingEntryData.Description;
			Element = mappingEntryData.Element;
			Source = mappingEntryData.Source;
			Type = mappingEntryData.Type;

			MappingEntryData = mappingEntryData;

		}

		public void Update()
		{
			MappingEntryData.ID = ID;
			MappingEntryData.Description = Description;
			MappingEntryData.Element = Element;
			MappingEntryData.Source = Source;
			MappingEntryData.Type = Type;
		}
	}
}
