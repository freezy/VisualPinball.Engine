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
	public class SwitchPopulationTests
	{
		[Test]
		public void ShouldMapASwitchWithTheSameName()
		{
			var table = new TableBuilder()
				.AddBumper("bumper_1")
				.Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			var gameEngineSwitches = new[] {
				new GamelogicEngineSwitch("bumper_1")
			};

			tableComponent.MappingConfig.PopulateSwitches(gameEngineSwitches, tableComponent);

			tableComponent.MappingConfig.Switches.Should().HaveCount(1);
			tableComponent.MappingConfig.Switches[0].Source.Should().Be(SwitchSource.Playfield);
			tableComponent.MappingConfig.Switches[0].Id.Should().Be("bumper_1");
			tableComponent.MappingConfig.Switches[0].Device.name.Should().Be("bumper_1");
			tableComponent.MappingConfig.Switches[0].DeviceItem.Should().Be(BumperAuthoring.SocketSwitchItem);
		}

		[Test]
		public void ShouldMapASwitchWithASwPrefix()
		{
			var table = new TableBuilder()
				.AddBumper("sw23")
				.Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			var gameEngineSwitches = new[] {
				new GamelogicEngineSwitch("23")
			};

			tableComponent.MappingConfig.PopulateSwitches(gameEngineSwitches, tableComponent);

			tableComponent.MappingConfig.Switches.Should().HaveCount(1);
			tableComponent.MappingConfig.Switches[0].Source.Should().Be(SwitchSource.Playfield);
			tableComponent.MappingConfig.Switches[0].Id.Should().Be("23");
			tableComponent.MappingConfig.Switches[0].Device.name.Should().Be("sw23");
			tableComponent.MappingConfig.Switches[0].DeviceItem.Should().Be(BumperAuthoring.SocketSwitchItem);
		}

		[Test]
		public void ShouldNotMapASwitchWithADifferentSameName()
		{
			var table = new TableBuilder()
				.AddBumper("bumper_1")
				.Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			var gameEngineSwitches = new[] {
				new GamelogicEngineSwitch("bumper_")
			};

			tableComponent.MappingConfig.PopulateSwitches(gameEngineSwitches, tableComponent);

			tableComponent.MappingConfig.Switches.Should().HaveCount(1);
			tableComponent.MappingConfig.Switches[0].Id.Should().Be("bumper_");
			tableComponent.MappingConfig.Switches[0].Device.Should().BeNull();
			tableComponent.MappingConfig.Switches[0].DeviceItem.Should().BeEmpty();
		}

		[Test]
		public void ShouldMapADeviceSwitchByHint()
		{
			var table = new TableBuilder()
				.AddTrough("some_trough")
				.Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			var gameEngineSwitches = new[] {
				new GamelogicEngineSwitch("88") { DeviceHint = "some_trough", DeviceItemHint = "1"}
			};

			tableComponent.MappingConfig.PopulateSwitches(gameEngineSwitches, tableComponent);

			tableComponent.MappingConfig.Switches.Should().HaveCount(1);
			tableComponent.MappingConfig.Switches[0].Id.Should().Be("88");
			tableComponent.MappingConfig.Switches[0].Device.name.Should().Be("some_trough");
			tableComponent.MappingConfig.Switches[0].DeviceItem.Should().Be("ball_switch_1");
		}

		[Test]
		public void ShouldReturnCustomSwitchesSorted()
		{
			var table = new TableBuilder().Build();

			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();

			tableComponent.MappingConfig.AddSwitch(new SwitchMapping {Id = "bbb"});

			var gameEngineSwitches = new[] {
				new GamelogicEngineSwitch("aaa") {DeviceHint = "some_trough", DeviceItemHint = "1"}
			};
			var switches = tableComponent.MappingConfig.GetSwitchIds(gameEngineSwitches).ToArray();

			switches[0].Id.Should().Be("aaa");
			switches[1].Id.Should().Be("bbb");
		}
	}
}
