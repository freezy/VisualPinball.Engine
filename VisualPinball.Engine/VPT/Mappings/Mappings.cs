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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Engine.VPT.Mappings
{
	public class Mappings : Item<MappingsData>
	{
		public override string ItemName => "Mapping";
		public override string ItemGroupName => "Mappings";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public Mappings() : this(new MappingsData("Mappings"))
		{
		}

		public Mappings(MappingsData data) : base(data)
		{
		}

		public Mappings(BinaryReader reader, string itemName) : this(new MappingsData(reader, itemName))
		{
		}

		public bool IsEmpty()
		{
			return (Data.Coils == null || Data.Coils.Length == 0)
				&& (Data.Switches == null || Data.Switches.Length == 0);
		}

		#region Lamp Population

		/// <summary>
		/// Auto-matches the lamps provided by the gamelogic engine with the
		/// lamps on the playfield.
		/// </summary>
		/// <param name="engineLamps">List of lamps provided by the gamelogic engine</param>
		/// <param name="tableLamps">List of lamps on the playfield</param>
		public void PopulateLamps(GamelogicEngineLamp[] engineLamps, IEnumerable<ILightable> tableLamps)
		{
			var lamps = tableLamps
				.GroupBy(x => x.Name.ToLower())
				.ToDictionary(x => x.Key, x => x.First());

			var gbLamps = new List<GamelogicEngineLamp>();
			foreach (var engineLamp in GetLamps(engineLamps)) {

				var lampMapping = Data.Lamps.FirstOrDefault(mappingsLampData => mappingsLampData.Id == engineLamp.Id && mappingsLampData.Source != LampSource.Coils);
				if (lampMapping == null) {

					// we'll handle those in a second loop when all the R lamps are added
					if (!string.IsNullOrEmpty(engineLamp.MainLampIdOfGreen) || !string.IsNullOrEmpty(engineLamp.MainLampIdOfBlue)) {
						gbLamps.Add(engineLamp);
						continue;
					}

					var description = string.IsNullOrEmpty(engineLamp.Description) ? string.Empty : engineLamp.Description;
					var playfieldItem = GuessPlayfieldLamp(lamps, engineLamp);

					Data.AddLamp(new MappingsLampData {
						Id = engineLamp.Id,
						Description = description,
						Destination = LampDestination.Playfield,
						PlayfieldItem = playfieldItem != null ? playfieldItem.Name : string.Empty,
					});
				}
			}

			foreach (var gbLamp in gbLamps) {
				var rLampId = !string.IsNullOrEmpty(gbLamp.MainLampIdOfGreen) ? gbLamp.MainLampIdOfGreen : gbLamp.MainLampIdOfBlue;
				var rLamp = Data.Lamps.FirstOrDefault(c => c.Id == rLampId);
				if (rLamp == null) {
					var playfieldItem = GuessPlayfieldLamp(lamps, gbLamp);
					rLamp = new MappingsLampData {
						Id = rLampId,
						Destination = LampDestination.Playfield,
						PlayfieldItem = playfieldItem != null ? playfieldItem.Name : string.Empty,
					};
					Data.AddLamp(rLamp);
				}

				rLamp.Type = LampType.RgbMulti;
				if (!string.IsNullOrEmpty(gbLamp.MainLampIdOfGreen)) {
					rLamp.Green = gbLamp.Id;

				} else {
					rLamp.Blue = gbLamp.Id;
				}
			}
		}

		/// <summary>
		/// Returns a sorted list of lamp names from the gamelogic engine,
		/// appended with the additional names in the lamp mapping. In short,
		/// the list of lamp names to choose from.
		/// </summary>
		/// <param name="engineLamps">Lamp names provided by the gamelogic engine</param>
		/// <returns>All lamp names</returns>
		public IEnumerable<GamelogicEngineLamp> GetLamps(GamelogicEngineLamp[] engineLamps)
		{
			var lamps = new List<GamelogicEngineLamp>();

			// first, add lamps from the gamelogic engine
			if (engineLamps != null) {
				lamps.AddRange(engineLamps);
			}

			// then add lamp ids that were added manually
			foreach (var mappingsLampData in Data.Lamps) {
				if (!lamps.Exists(entry => entry.Id == mappingsLampData.Id)) {
					lamps.Add(new GamelogicEngineLamp(mappingsLampData.Id));
				}
			}

			lamps.Sort((s1, s2) => s1.Id.CompareTo(s2.Id));
			return lamps;
		}

		private static ILightable GuessPlayfieldLamp(Dictionary<string, ILightable> lamps, GamelogicEngineLamp engineLamp)
		{
			// first, match by regex if hint provided
			if (!string.IsNullOrEmpty(engineLamp.PlayfieldItemHint)) {
				foreach (var lampName in lamps.Keys) {
					var regex = new Regex(engineLamp.PlayfieldItemHint.ToLower());
					if (regex.Match(lampName).Success) {
						return lamps[lampName];
					}
				}
			}

			// second, match by "lXX" or name
			var matchKey = int.TryParse(engineLamp.Id, out var numericLampId)
				? $"l{numericLampId}"
				: engineLamp.Id;

			return lamps.ContainsKey(matchKey) ? lamps[matchKey] : null;
		}

		#endregion
	}
}
