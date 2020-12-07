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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Game.Engine;

namespace VisualPinball.Engine.VPT.Trough
{
	public class Trough : Item<TroughData>, ISwitchableDevice, ICoilableDevice
	{
		public override string ItemName { get; } = "Trough";
		public override string ItemGroupName { get; } = null;

		public const string EntrySwitchId = "drain_switch";
		public const string TroughSwitchId = "trough_switch";
		public const string JamSwitchId = "jam_switch";
		public const string EjectCoilId = "eject_coil";
		public const string EntryCoilId = "entry_coil";

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches {
			get {

				switch (Data.Type) {
					case TroughType.ModernOpto:
					case TroughType.ModernMech:
						return Enumerable.Repeat(0, Data.SwitchCount)
							.Select((_, i) => new GamelogicEngineSwitch
								{ Description = SwitchDescription(i), Id = $"{i + 1}" })
							.Concat(Data.JamSwitch
								? new [] { new GamelogicEngineSwitch {Description = "Jam Switch", Id = JamSwitchId }}
								: new GamelogicEngineSwitch[0]
							);

					case TroughType.TwoCoilsNSwitches:
						return new[] {
							new GamelogicEngineSwitch {Description = "Entry Switch", Id = EntrySwitchId}
						}.Concat(Enumerable.Repeat(0, Data.SwitchCount)
							.Select((_, i) => new GamelogicEngineSwitch
								{ Description = SwitchDescription(i), Id = $"{i + 1}"} )
						).Concat(Data.JamSwitch
							? new [] { new GamelogicEngineSwitch {Description = "Jam Switch", Id = JamSwitchId }}
							: new GamelogicEngineSwitch[0]
						);

					case TroughType.TwoCoilsOneSwitch:
						return new[] {
							new GamelogicEngineSwitch {Description = "Entry Switch", Id = EntrySwitchId},
							new GamelogicEngineSwitch {Description = "Trough Switch", Id = TroughSwitchId},
						}.Concat(Data.JamSwitch
							? new [] { new GamelogicEngineSwitch {Description = "Jam Switch", Id = JamSwitchId }}
							: new GamelogicEngineSwitch[0]
						);

					case TroughType.ClassicSingleBall:
						return new[] {
							new GamelogicEngineSwitch {Description = "Drain Switch", Id = EntrySwitchId},
						};

					default:
						throw new ArgumentException("Invalid trough type " + Data.Type);
				}
			}
		}

		public IEnumerable<GamelogicEngineCoil> AvailableCoils {
			get {
				switch (Data.Type) {
					case TroughType.ModernOpto:
					case TroughType.ModernMech:
						return new[] {
							new GamelogicEngineCoil {Description = "Eject", Id = EjectCoilId}
						};
					case TroughType.TwoCoilsNSwitches:
					case TroughType.TwoCoilsOneSwitch:
						return new[] {
							new GamelogicEngineCoil {Description = "Entry", Id = EntryCoilId},
							new GamelogicEngineCoil {Description = "Eject", Id = EjectCoilId}
						};
					case TroughType.ClassicSingleBall:
						return new[] {
							new GamelogicEngineCoil {Description = "Eject", Id = EjectCoilId}
						};
					default:
						throw new ArgumentException("Invalid trough type " + Data.Type);
				}
			}
		}

		/// <summary>
		/// Time in milliseconds it takes the switch to enable when the ball enters.
		/// </summary>
		/// <exception cref="ArgumentException"></exception>
		public int RollTimeEnabled {
			get {
				switch (Data.Type) {
					case TroughType.ModernOpto:
						return Data.TransitionTime;

					case TroughType.ModernMech:
					case TroughType.TwoCoilsNSwitches:
					case TroughType.TwoCoilsOneSwitch:
					case TroughType.ClassicSingleBall:
						return Data.RollTime / 2;

					default:
						throw new ArgumentException("Invalid trough type " + Data.Type);
				}
			}
		}

		/// <summary>
		/// Time in milliseconds it takes the switch to disable after ball starts rolling.
		/// </summary>
		/// <exception cref="ArgumentException"></exception>
		public int RollTimeDisabled {
			get {
				switch (Data.Type) {
					case TroughType.ModernOpto:
						return Data.RollTime - Data.TransitionTime;

					case TroughType.ModernMech:
					case TroughType.TwoCoilsNSwitches:
					case TroughType.TwoCoilsOneSwitch:
					case TroughType.ClassicSingleBall:
						return Data.RollTime / 2;

					default:
						throw new ArgumentException("Invalid trough type " + Data.Type);
				}
			}
		}

		public Trough(TroughData data) : base(data)
		{
		}

		public Trough(BinaryReader reader, string itemName) : this(new TroughData(reader, itemName))
		{
		}

		private string SwitchDescription(int i)
		{
			if (i == 0) {
				return "Ball 1 (eject)";
			}

			return i == Data.SwitchCount - 1
				? $"Ball {i + 1} (entry)"
				: $"Ball {i + 1}";
		}

		public static Trough GetDefault(Table.Table table)
		{
			var primitiveData = new TroughData(table.GetNewName<Trough>("Trough"));
			return new Trough(primitiveData);
		}
	}
}
