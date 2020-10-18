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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;

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

		#region Switch Population

		public void PopulateSwitches(string[] engineSwitches, IEnumerable<ISwitchable> tableSwitches)
		{
			var switches = tableSwitches
				.GroupBy(x => x.Name.ToLower())
				.ToDictionary(x => x.Key, x => x.First());

			foreach (var id in GetSwitchIds(engineSwitches))
			{
				var switchMapping = Data.Switches.FirstOrDefault(mappingsSwitchData => mappingsSwitchData.Id == id);

				if (switchMapping == null) {
					var matchKey = int.TryParse(id, out var numericSwitchId)
						? $"sw{numericSwitchId}"
						: id;

					var matchedItem = switches.ContainsKey(matchKey)
						? switches[matchKey]
						: null;

					var description = GuessDescription(id);
					Data.AddSwitch(new MappingsSwitchData {
						Id = id,
						Description = description,
						Source = description == string.Empty ? SwitchSource.Playfield : SwitchSource.InputSystem,
						PlayfieldItem = matchedItem == null ? string.Empty : matchedItem.Name,
						Type = matchedItem is Kicker.Kicker || matchedItem is Trigger.Trigger || description == string.Empty
							? SwitchType.OnOff
							: SwitchType.Pulse,
						InputActionMap = GuessInputMap(id),
						InputAction = description != string.Empty ? GuessInputAction(id) : null,
					});
				}
			}
		}


		/// <summary>
		/// Returns a sorted list of switch names from the gamelogic engine,
		/// appended with the additional names in the switch mapping. In short,
		/// the list of switch names to choose from.
		/// </summary>
		/// <param name="engineSwitches">Switch names provided by the gamelogic engine</param>
		/// <returns>All switch names</returns>
		public IEnumerable<string> GetSwitchIds(string[] engineSwitches)
		{
			var ids = new List<string>();
			if (engineSwitches != null) {
				ids.AddRange(engineSwitches);
			}

			foreach (var mappingsSwitchData in Data.Switches) {
				if (ids.IndexOf(mappingsSwitchData.Id) == -1) {
					ids.Add(mappingsSwitchData.Id);
				}
			}
			ids.Sort();

			return ids;
		}

		private string GuessDescription(string switchId)
		{
			if (switchId.Contains("left_flipper")) {
				return "Left Flipper";
			}
			if (switchId.Contains("right_flipper")) {
				return "Right Flipper";
			}
			if (switchId.Contains("create_ball")) {
				return "Create Ball";
			}
			if (switchId.Contains("plunger")) {
				return "Plunger";
			}

			return string.Empty;
		}

		private string GuessInputMap(string switchId)
		{
			if (switchId.Contains("create_ball")) {
				return InputConstants.MapDebug;
			}
			return InputConstants.MapCabinetSwitches;
		}

		private string GuessInputAction(string switchId)
		{
			if (switchId.Contains("left_flipper")) {
				return InputConstants.ActionLeftFlipper;
			}
			if (switchId.Contains("right_flipper")) {
				return InputConstants.ActionRightFlipper;
			}
			if (switchId.Contains("create_ball")) {
				return InputConstants.ActionCreateBall;
			}
			if (switchId.Contains("plunger")) {
				return InputConstants.ActionPlunger;
			}

			return string.Empty;
		}


		#endregion

		#region Coil Population

		/// <summary>
		/// Auto-matches the coils provided by the gamelogic engine with the
		/// coils on the playfield.
		/// </summary>
		/// <param name="engineCoils">List of coils provided by the gamelogic engine</param>
		/// <param name="tableCoils">List of coils on the playfield</param>
		public void PopulateCoils(string[] engineCoils, ICollection<string> tableCoils)
		{
			foreach (var id in GetCoilIds(engineCoils)) {

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

		/// <summary>
		/// Returns a sorted list of coil names from the gamelogic engine,
		/// appended with the additional names in the coil mapping. In short,
		/// the list of coil names to choose from.
		/// </summary>
		/// <param name="engineCoils">Coil names provided by the gamelogic engine</param>
		/// <returns>All coil names</returns>
		public IEnumerable<string> GetCoilIds(string[] engineCoils)
		{
			var ids = new List<string>();
			if (engineCoils != null) {
				ids.AddRange(engineCoils);
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

		private static string FindCoil(ICollection<string> coils, params string[] names)
		{
			foreach (var itemName in names) {
				if (coils.Contains(itemName.ToLower())) {
					return itemName;
				}
			}
			return string.Empty;
		}

		#endregion
	}
}
