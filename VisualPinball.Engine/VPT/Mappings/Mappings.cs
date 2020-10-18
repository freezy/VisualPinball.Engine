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

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VisualPinball.Engine.VPT.Mappings
{
	public class Mappings : Item<MappingsData>
	{
		public override string ItemType => "Mappings";

		public Mappings() : this(new MappingsData("Mappings"))
		{
		}

		public Mappings(MappingsData data) : base(data)
		{
		}

		public Mappings(BinaryReader reader, string itemName) : this(new MappingsData(reader, itemName))
		{
		}

		public void PopulateCoils(string[] engineCoils, ICollection<string> tableCoils)
		{
			foreach (var id in GetIds(engineCoils)) {

				var coilMapping = Data.Coils.FirstOrDefault(mappingsCoilData => mappingsCoilData.Id == id);
				if (coilMapping == null) {
					var itemName = string.Empty;
					var description = string.Empty;
					switch (id) {
						case "c_left_flipper":
							itemName = FindCoil(tableCoils, "LeftFlipper", "FlipperLeft", "FlipperL", "LFlipper");
							description = "Left Flipper";
							break;

						case "c_right_flipper":
							itemName = FindCoil(tableCoils, "RightFlipper", "FlipperRight", "FlipperR", "RFlipper");
							description = "Right Flipper";
							break;

						case "c_auto_plunger":
							itemName = FindCoil(tableCoils, "Plunger");
							description = "Plunger";
							break;
					}

					Data.AddCoil(new MappingsCoilData {
						Id = id,
						Description = description,
						Destination = CoilDestination.Playfield,
						PlayfieldItem = itemName,
						Type = CoilType.SingleWound
					});
				}
			}
		}

		private static string FindCoil(ICollection<string> coils, params string[] names)
		{
			foreach (var itemName in names) {
				if (coils.Contains(itemName.ToLower())) {
					return itemName;
				}
			}
			return string.Empty;
		}

		public IEnumerable<string> GetIds(string[] availableCoils)
		{
			var ids = new List<string>();
			if (availableCoils != null) {
				ids.AddRange(availableCoils);
			}

			foreach (var mappingsCoilData in Data.Coils) {
				if (ids.IndexOf(mappingsCoilData.Id) == -1) {
					ids.Add(mappingsCoilData.Id);
				}
				if (ids.IndexOf(mappingsCoilData.HoldCoilId) == -1) {
					ids.Add(mappingsCoilData.HoldCoilId);
				}
			}

			ids.Sort();
			return ids;
		}
	}
}
