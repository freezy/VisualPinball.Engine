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

using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Mappings;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.Mappings
{
	public class CoilPopulationTests
	{
		[Test]
		public void ShouldMapACoilWithTheSameName()
		{
			var table = new TableBuilder()
				.AddFlipper("left_flipper")
				.Build();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("left_flipper") { Description = "Left Flipper"}
			};

			table.Mappings.PopulateCoils(gameEngineCoils, table.Coilables, table.CoilableDevices);

			table.Mappings.Data.Coils.Should().HaveCount(1);
			table.Mappings.Data.Coils[0].Destination.Should().Be(CoilDestination.Playfield);
			table.Mappings.Data.Coils[0].Id.Should().Be("left_flipper");
			table.Mappings.Data.Coils[0].Description.Should().Be("Left Flipper");
			table.Mappings.Data.Coils[0].PlayfieldItem.Should().Be("left_flipper");
		}

		[Test]
		public void ShouldNotMapACoilWithADifferentName()
		{
			var table = new TableBuilder()
				.AddFlipper("left_flipper")
				.Build();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("left_flipper_")
			};

			table.Mappings.PopulateCoils(gameEngineCoils, table.Coilables, table.CoilableDevices);
			table.Mappings.Data.Coils.Should().HaveCount(1);
			table.Mappings.Data.Coils[0].Id.Should().Be("left_flipper_");
			table.Mappings.Data.Coils[0].PlayfieldItem.Should().BeEmpty();
		}

		[Test]
		public void ShouldMapACoilByHint()
		{
			var table = new TableBuilder()
				.AddFlipper("left_flipper")
				.Build();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("foobar") { PlayfieldItemHint = "left_flipper"}
			};

			table.Mappings.PopulateCoils(gameEngineCoils, table.Coilables, table.CoilableDevices);

			table.Mappings.Data.Coils.Should().HaveCount(1);
			table.Mappings.Data.Coils[0].Destination.Should().Be(CoilDestination.Playfield);
			table.Mappings.Data.Coils[0].Id.Should().Be("foobar");
			table.Mappings.Data.Coils[0].PlayfieldItem.Should().Be("left_flipper");
		}

		[Test]
		public void ShouldMapAHoldCoilByHint()
		{
			var table = new TableBuilder()
				.AddFlipper("left_flipper")
				.Build();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("left_flipper_power") { PlayfieldItemHint = "left_flipper"},
				new GamelogicEngineCoil("left_flipper_hold") {  MainCoilIdOfHoldCoil = "left_flipper_power"},
			};

			table.Mappings.PopulateCoils(gameEngineCoils, table.Coilables, table.CoilableDevices);

			table.Mappings.Data.Coils.Should().HaveCount(1);
			table.Mappings.Data.Coils[0].Destination.Should().Be(CoilDestination.Playfield);
			table.Mappings.Data.Coils[0].Id.Should().Be("left_flipper_power");
			table.Mappings.Data.Coils[0].PlayfieldItem.Should().Be("left_flipper");
			table.Mappings.Data.Coils[0].HoldCoilId.Should().Be("left_flipper_hold");
		}

		[Test]
		public void ShouldCreateMainCoilIfNotFoundByHoldCoil()
		{
			var table = new TableBuilder()
				.AddFlipper("left_flipper")
				.Build();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("left_flipper_power") { PlayfieldItemHint = "left_flipper"},
				new GamelogicEngineCoil("left_flipper_hold") { MainCoilIdOfHoldCoil = "foobar"},
			};

			table.Mappings.PopulateCoils(gameEngineCoils, table.Coilables, table.CoilableDevices);

			table.Mappings.Data.Coils.Should().HaveCount(2);
			table.Mappings.Data.Coils[0].Destination.Should().Be(CoilDestination.Playfield);
			table.Mappings.Data.Coils[0].Id.Should().Be("left_flipper_power");
			table.Mappings.Data.Coils[0].PlayfieldItem.Should().Be("left_flipper");
			table.Mappings.Data.Coils[0].HoldCoilId.Should().BeEmpty();

			table.Mappings.Data.Coils[1].Id.Should().Be("foobar");
			table.Mappings.Data.Coils[1].PlayfieldItem.Should().BeEmpty();
			table.Mappings.Data.Coils[1].HoldCoilId.Should().Be("left_flipper_hold");
		}

		[Test]
		public void ShouldMapADeviceCoilByHint()
		{
			var table = new TableBuilder()
				.AddTrough("my_trough")
				.Build();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("eject_trough") { DeviceHint = "my_trough", DeviceItemHint = "eject"}
			};

			table.Mappings.PopulateCoils(gameEngineCoils, table.Coilables, table.CoilableDevices);

			table.Mappings.Data.Coils.Should().HaveCount(1);
			table.Mappings.Data.Coils[0].Destination.Should().Be(CoilDestination.Device);
			table.Mappings.Data.Coils[0].Id.Should().Be("eject_trough");
			table.Mappings.Data.Coils[0].Device.Should().Be("my_trough");
			table.Mappings.Data.Coils[0].DeviceItem.Should().Be("eject_coil");
		}

		[Test]
		public void ShouldReturnCustomCoilsSorted()
		{
			var table = new TableBuilder().Build();
			table.Mappings.Data.AddCoil(new MappingsCoilData {Id = "zzz"});
			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("yyy")
			};
			var coils = table.Mappings.GetCoils(gameEngineCoils).ToArray();
			coils[0].Id.Should().Be("yyy");
			coils[1].Id.Should().Be("zzz");
		}
	}
}
