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
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class CoilPopulationTests
	{
		[Test]
		public void ShouldMapACoilWithTheSameName()
		{
			var table = new TableBuilder()
				.AddFlipper("left_flipper")
				.Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("left_flipper") { Description = "Left Flipper"}
			};

			tableComponent.MappingConfig.Clear();
			tableComponent.MappingConfig.PopulateCoils(gameEngineCoils, tableComponent);

			tableComponent.MappingConfig.Coils.Should().HaveCount(1);
			tableComponent.MappingConfig.Coils[0].Destination.Should().Be(CoilDestination.Playfield);
			tableComponent.MappingConfig.Coils[0].Id.Should().Be("left_flipper");
			tableComponent.MappingConfig.Coils[0].Description.Should().Be("Left Flipper");
			tableComponent.MappingConfig.Coils[0].Device.name.Should().Be("left_flipper");
			tableComponent.MappingConfig.Coils[0].DeviceItem.Should().Be(FlipperAuthoring.MainCoilItem);
		}

		[Test]
		public void ShouldCorrectlyPrintCoilInfo()
		{
			var coil = new CoilMapping {
				Id = "c_left_flipper",
				InternalId = 12,
				Description = "Left Flipper"
			};
			coil.ToString().Should().Be("coil c_left_flipper (12) Left Flipper");
		}

		[Test]
		public void ShouldNotMapACoilWithADifferentName()
		{
			var table = new TableBuilder()
				.AddFlipper("left_flipper")
				.Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("left_flipper_")
			};

			tableComponent.MappingConfig.Clear();
			tableComponent.MappingConfig.PopulateCoils(gameEngineCoils, tableComponent);
			tableComponent.MappingConfig.Coils.Should().HaveCount(1);
			tableComponent.MappingConfig.Coils[0].Id.Should().Be("left_flipper_");
			tableComponent.MappingConfig.Coils[0].Device.Should().BeNull();
		}

		[Test]
		public void ShouldMapACoilByHint()
		{
			var table = new TableBuilder()
				.AddFlipper("left_flipper")
				.Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("foobar") { DeviceHint = "left_flipper"}
			};

			tableComponent.MappingConfig.Clear();
			tableComponent.MappingConfig.PopulateCoils(gameEngineCoils, tableComponent);

			tableComponent.MappingConfig.Coils.Should().HaveCount(1);
			tableComponent.MappingConfig.Coils[0].Destination.Should().Be(CoilDestination.Playfield);
			tableComponent.MappingConfig.Coils[0].Id.Should().Be("foobar");
			tableComponent.MappingConfig.Coils[0].Device.name.Should().Be("left_flipper");
			tableComponent.MappingConfig.Coils[0].DeviceItem.Should().Be(FlipperAuthoring.MainCoilItem);
		}

		[Test]
		public void ShouldMapAHoldCoilByHint()
		{
			var table = new TableBuilder()
				.AddFlipper("left_flipper", true)
				.Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("left_flipper_power") { DeviceHint = "left_flipper", DeviceItemHint = FlipperAuthoring.MainCoilItem },
				new GamelogicEngineCoil("left_flipper_hold") { DeviceHint = "left_flipper", DeviceItemHint = FlipperAuthoring.HoldCoilItem },
			};

			tableComponent.MappingConfig.Clear();
			tableComponent.MappingConfig.PopulateCoils(gameEngineCoils, tableComponent);

			tableComponent.MappingConfig.Coils.Should().HaveCount(2);
			tableComponent.MappingConfig.Coils[1].Destination.Should().Be(CoilDestination.Playfield);
			tableComponent.MappingConfig.Coils[1].Id.Should().Be("left_flipper_power");
			tableComponent.MappingConfig.Coils[1].Device.name.Should().Be("left_flipper");
			tableComponent.MappingConfig.Coils[1].DeviceItem.Should().Be(FlipperAuthoring.MainCoilItem);
			tableComponent.MappingConfig.Coils[0].Id.Should().Be("left_flipper_hold");
			tableComponent.MappingConfig.Coils[0].Device.name.Should().Be("left_flipper");
			tableComponent.MappingConfig.Coils[0].DeviceItem.Should().Be(FlipperAuthoring.HoldCoilItem);
		}

		[Test]
		public void ShouldReturnCustomCoilsSorted()
		{
			var table = new TableBuilder().Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			tableComponent.MappingConfig.Clear();
			tableComponent.MappingConfig.AddCoil(new CoilMapping {Id = "zzz"});
			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("yyy")
			};
			var coils = tableComponent.MappingConfig.GetCoils(gameEngineCoils).ToArray();
			coils[0].Id.Should().Be("yyy");
			coils[1].Id.Should().Be("zzz");
		}

		[Test]
		public void ShouldAddLamps()
		{
			var table = new TableBuilder().Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("yyy") { IsLamp = true }
			};
			tableComponent.MappingConfig.Clear();
			tableComponent.MappingConfig.PopulateCoils(gameEngineCoils, tableComponent);

			tableComponent.MappingConfig.Coils.Count.Should().Be(1);
			tableComponent.MappingConfig.Lamps.Count.Should().Be(1);
			tableComponent.MappingConfig.Lamps[0].Id.Should().Be("yyy");
			tableComponent.MappingConfig.Lamps[0].Source.Should().Be(LampSource.Coils);
		}

		[Test]
		public void ShouldDeleteLamp()
		{
			var table = new TableBuilder().Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("yyy") { IsLamp = true }
			};
			tableComponent.MappingConfig.Clear();
			tableComponent.MappingConfig.AddLamp(new LampMapping { Id = "yyy" });
			tableComponent.MappingConfig.PopulateCoils(gameEngineCoils, tableComponent);
			tableComponent.MappingConfig.RemoveCoil(tableComponent.MappingConfig.Coils[0]);
			tableComponent.MappingConfig.Coils.Count.Should().Be(0);
			tableComponent.MappingConfig.Lamps.Count.Should().Be(1);
		}

		[Test]
		public void ShouldDeleteCoilLamps()
		{
			var table = new TableBuilder().Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("yyy") { IsLamp = true },
				new GamelogicEngineCoil("zzz") { IsLamp = true }
			};
			tableComponent.MappingConfig.Clear();
			tableComponent.MappingConfig.AddLamp(new LampMapping { Id = "yyy" });
			tableComponent.MappingConfig.PopulateCoils(gameEngineCoils, tableComponent);
			tableComponent.MappingConfig.RemoveAllCoils();
			tableComponent.MappingConfig.Coils.Count.Should().Be(0);
			tableComponent.MappingConfig.Lamps.Count.Should().Be(1);
		}

		[Test]
		public void ShouldNotDeleteCoilLamps()
		{
			var table = new TableBuilder().Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			var gameEngineCoils = new[] {
				new GamelogicEngineCoil("yyy") { IsLamp = true },
				new GamelogicEngineCoil("zzz") { IsLamp = true }
			};
			tableComponent.MappingConfig.Clear();
			tableComponent.MappingConfig.AddLamp(new LampMapping { Id = "yyy" });
			tableComponent.MappingConfig.PopulateCoils(gameEngineCoils, tableComponent);
			tableComponent.MappingConfig.RemoveAllLamps();
			tableComponent.MappingConfig.Lamps.Count.Should().Be(2);
		}
	}
}
