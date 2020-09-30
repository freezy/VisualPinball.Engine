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

using VisualPinball.Engine.VPT.MappingConfig;

namespace VisualPinball.Unity.Editor
{
	public class SwitchListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, HeaderName = "ID", Width = 120)]
		public string Name => ID;

		[ManagerListColumn(Order = 1, HeaderName = "Description", Width = 150)]
		public string Description;

		[ManagerListColumn(Order = 2, HeaderName = "Source", Width = 150)]
		public int Source;

		[ManagerListColumn(Order = 3, HeaderName = "Element", Width = 200)]
		public string Element;

		[ManagerListColumn(Order = 4, HeaderName = "Type", Width = 100)]
		public int Type;

		[ManagerListColumn(Order = 5, HeaderName = "Off", Width = 100)]
		public string Off;

		public string ID;
		public string InputActionMap;
		public string InputAction;
		public string PlayfieldItem;
		public int Constant;
		public int Pulse;

		public MappingEntryData MappingEntryData;

		public SwitchListData(MappingEntryData mappingEntryData) {
			ID = mappingEntryData.Id;
			Description = mappingEntryData.Description;
			Source = mappingEntryData.Source;
			InputActionMap = mappingEntryData.InputActionMap;
			InputAction = mappingEntryData.InputAction;
			PlayfieldItem = mappingEntryData.PlayfieldItem;
			Constant = mappingEntryData.Constant;
			Type = mappingEntryData.Type;
			Pulse = mappingEntryData.Pulse;

			MappingEntryData = mappingEntryData;
		}

		public void Update()
		{
			MappingEntryData.Id = ID;
			MappingEntryData.Description = Description;
			MappingEntryData.Source = Source;
			MappingEntryData.InputActionMap = InputActionMap;
			MappingEntryData.InputAction = InputAction;
			MappingEntryData.PlayfieldItem = PlayfieldItem;
			MappingEntryData.Constant = Constant;
			MappingEntryData.Type = Type;
			MappingEntryData.Pulse = Pulse;
		}
	}
}
