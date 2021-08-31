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
	public class LampPopulationTests
	{
		// todo move to unity and re-enable
		// [Test]
		// public void ShouldMapALampWithTheSameName()
		// {
		// 	var table = new TableBuilder()
		// 		.AddLight("some_light")
		// 		.Build();
		//
		// 	var gameEngineLamps = new[] {
		// 		new GamelogicEngineLamp("some_light") { Description = "Some Light"}
		// 	};
		//
		// 	table.Mappings.PopulateLamps(gameEngineLamps, table.Lightables);
		//
		// 	table.Mappings.Data.Lamps.Should().HaveCount(1);
		// 	table.Mappings.Data.Lamps[0].Destination.Should().Be(CoilDestination.Playfield);
		// 	table.Mappings.Data.Lamps[0].Id.Should().Be("some_light");
		// 	table.Mappings.Data.Lamps[0].Description.Should().Be("Some Light");
		// 	table.Mappings.Data.Lamps[0].PlayfieldItem.Should().Be("some_light");
		// }
		//
		// [Test]
		// public void ShouldMapALampWithTheSameNumericalId()
		// {
		// 	var table = new TableBuilder()
		// 		.AddLight("l42")
		// 		.Build();
		//
		// 	var gameEngineLamps = new[] {
		// 		new GamelogicEngineLamp("42") { Description = "Light 42"}
		// 	};
		//
		// 	table.Mappings.PopulateLamps(gameEngineLamps, table.Lightables);
		//
		// 	table.Mappings.Data.Lamps.Should().HaveCount(1);
		// 	table.Mappings.Data.Lamps[0].Destination.Should().Be(CoilDestination.Playfield);
		// 	table.Mappings.Data.Lamps[0].Id.Should().Be("42");
		// 	table.Mappings.Data.Lamps[0].Description.Should().Be("Light 42");
		// 	table.Mappings.Data.Lamps[0].PlayfieldItem.Should().Be("l42");
		// }
		//
		// [Test]
		// public void ShouldMapALampWithViaRegex()
		// {
		// 	var table = new TableBuilder()
		// 		.AddLight("lamp_foobar_name")
		// 		.Build();
		//
		// 	var gameEngineLamps = new[] {
		// 		new GamelogicEngineLamp("11") { Description = "Foobar", DeviceHint = "_foobar_"}
		// 	};
		//
		// 	table.Mappings.PopulateLamps(gameEngineLamps, table.Lightables);
		//
		// 	table.Mappings.Data.Lamps.Should().HaveCount(1);
		// 	table.Mappings.Data.Lamps[0].Destination.Should().Be(CoilDestination.Playfield);
		// 	table.Mappings.Data.Lamps[0].Id.Should().Be("11");
		// 	table.Mappings.Data.Lamps[0].Description.Should().Be("Foobar");
		// 	table.Mappings.Data.Lamps[0].PlayfieldItem.Should().Be("lamp_foobar_name");
		// }
		//
		// [Test]
		// public void ShouldNotMapALampWithViaRegex()
		// {
		// 	var table = new TableBuilder()
		// 		.AddLight("lamp_foobar_name")
		// 		.Build();
		//
		// 	var gameEngineLamps = new[] {
		// 		new GamelogicEngineLamp("12") { Description = "Foobar", DeviceHint = "^_foobar_$"}
		// 	};
		//
		// 	table.Mappings.PopulateLamps(gameEngineLamps, table.Lightables);
		//
		// 	table.Mappings.Data.Lamps.Should().HaveCount(1);
		// 	table.Mappings.Data.Lamps[0].Destination.Should().Be(CoilDestination.Playfield);
		// 	table.Mappings.Data.Lamps[0].Id.Should().Be("12");
		// 	table.Mappings.Data.Lamps[0].Description.Should().Be("Foobar");
		// 	table.Mappings.Data.Lamps[0].PlayfieldItem.Should().BeEmpty();
		// }
		//
		// [Test]
		// public void ShouldMapAnRgbLamp()
		// {
		// 	var table = new TableBuilder()
		// 		.AddLight("my_rgb_light")
		// 		.Build();
		//
		// 	var gameEngineLamps = new[] {
		// 		new GamelogicEngineLamp("rgb") { Description = "RGB", DeviceHint = "rgb"},
		// 		new GamelogicEngineLamp("g") { MainLampIdOfGreen = "rgb"},
		// 		new GamelogicEngineLamp("b") { MainLampIdOfBlue = "rgb"}
		// 	};
		//
		// 	table.Mappings.PopulateLamps(gameEngineLamps, table.Lightables);
		//
		// 	table.Mappings.Data.Lamps.Should().HaveCount(1);
		// 	table.Mappings.Data.Lamps[0].Destination.Should().Be(CoilDestination.Playfield);
		// 	table.Mappings.Data.Lamps[0].Id.Should().Be("rgb");
		// 	table.Mappings.Data.Lamps[0].Green.Should().Be("g");
		// 	table.Mappings.Data.Lamps[0].Blue.Should().Be("b");
		// 	table.Mappings.Data.Lamps[0].Description.Should().Be("RGB");
		// 	table.Mappings.Data.Lamps[0].PlayfieldItem.Should().Be("my_rgb_light");
		// }
		//
		// [Test]
		// public void ShouldCreateMapAnRgbLampIfRisMissing()
		// {
		// 	var table = new TableBuilder()
		// 		.AddLight("my_rgb_light")
		// 		.Build();
		//
		// 	var gameEngineLamps = new[] {
		// 		new GamelogicEngineLamp("g") { Description = "RGB", MainLampIdOfGreen = "rgb"},
		// 		new GamelogicEngineLamp("b") { MainLampIdOfBlue = "rgb"}
		// 	};
		//
		// 	table.Mappings.PopulateLamps(gameEngineLamps, table.Lightables);
		//
		// 	table.Mappings.Data.Lamps.Should().HaveCount(1);
		// 	table.Mappings.Data.Lamps[0].Destination.Should().Be(CoilDestination.Playfield);
		// 	table.Mappings.Data.Lamps[0].Id.Should().Be("rgb");
		// 	table.Mappings.Data.Lamps[0].Green.Should().Be("g");
		// 	table.Mappings.Data.Lamps[0].Blue.Should().Be("b");
		// 	table.Mappings.Data.Lamps[0].Description.Should().BeEmpty();
		// 	table.Mappings.Data.Lamps[0].PlayfieldItem.Should().BeEmpty();
		// }
		//
		// [Test]
		// public void ShouldReturnAllLampIds()
		// {
		// 	var table = new TableBuilder()
		// 		.AddLight("l11")
		// 		.AddLight("l12")
		// 		.Build();
		//
		// 	var gameEngineLamps = new[] {
		// 		new GamelogicEngineLamp("11")
		// 	};
		//
		// 	table.Mappings.PopulateLamps(gameEngineLamps, table.Lightables);
		// 	table.Mappings.Data.AddLamp(new MappingsLampData {
		// 		Id = "12",
		// 		Destination = LampDestination.Playfield,
		// 		PlayfieldItem = "l12"
		// 	});
		//
		// 	var lampIds = table.Mappings.GetLamps(gameEngineLamps).ToArray();
		//
		// 	lampIds.Length.Should().Be(2);
		// 	lampIds[0].Id.Should().Be("11");
		// 	lampIds[1].Id.Should().Be("12");
		// }
	}
}
