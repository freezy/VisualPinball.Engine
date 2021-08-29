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
	public class SwitchPopulationTests
	{
		[Test]
		public void ShouldMapASwitchWithTheSameName()
		{
			var table = new TableBuilder()
				.AddBumper("bumper_1")
				.Build();

			var gameEngineSwitches = new[] {
				new GamelogicEngineSwitch("bumper_1")
			};

			table.Mappings.PopulateSwitches(gameEngineSwitches, table.Switchables, table.SwitchableDevices);

			table.Mappings.Data.Switches.Should().HaveCount(1);
			table.Mappings.Data.Switches[0].Source.Should().Be(SwitchSource.Playfield);
			table.Mappings.Data.Switches[0].Id.Should().Be("bumper_1");
			table.Mappings.Data.Switches[0].PlayfieldItem.Should().Be("bumper_1");
		}

		[Test]
		public void ShouldMapASwitchWithASwPrefix()
		{
			var table = new TableBuilder()
				.AddBumper("sw23")
				.Build();

			var gameEngineSwitches = new[] {
				new GamelogicEngineSwitch("23")
			};

			table.Mappings.PopulateSwitches(gameEngineSwitches, table.Switchables, table.SwitchableDevices);

			table.Mappings.Data.Switches.Should().HaveCount(1);
			table.Mappings.Data.Switches[0].Source.Should().Be(SwitchSource.Playfield);
			table.Mappings.Data.Switches[0].Id.Should().Be("23");
			table.Mappings.Data.Switches[0].PlayfieldItem.Should().Be("sw23");
		}

		[Test]
		public void ShouldNotMapASwitchWithADifferentSameName()
		{
			var table = new TableBuilder()
				.AddBumper("bumper_1")
				.Build();

			var gameEngineSwitches = new[] {
				new GamelogicEngineSwitch("bumper_")
			};

			table.Mappings.PopulateSwitches(gameEngineSwitches, table.Switchables, table.SwitchableDevices);

			table.Mappings.Data.Switches.Should().HaveCount(1);
			table.Mappings.Data.Switches[0].Id.Should().Be("bumper_");
			table.Mappings.Data.Switches[0].PlayfieldItem.Should().BeEmpty();
		}

		[Test]
		public void ShouldMapADeviceSwitchByHint()
		{
			var table = new TableBuilder()
				.AddTrough("some_trough")
				.Build();

			var gameEngineSwitches = new[] {
				new GamelogicEngineSwitch("88") { DeviceHint = "some_trough", DeviceItemHint = "1"}
			};

			table.Mappings.PopulateSwitches(gameEngineSwitches, table.Switchables, table.SwitchableDevices);

			table.Mappings.Data.Switches.Should().HaveCount(1);
			table.Mappings.Data.Switches[0].Source.Should().Be(SwitchSource.Device);
			table.Mappings.Data.Switches[0].Id.Should().Be("88");
			table.Mappings.Data.Switches[0].Device.Should().Be("some_trough");
			table.Mappings.Data.Switches[0].DeviceItem.Should().Be("1");
		}

		[Test]
		public void ShouldReturnCustomSwitchesSorted()
		{
			var table = new TableBuilder().Build();
			table.Mappings.Data.AddSwitch(new MappingsSwitchData {Id = "bbb"});

			var gameEngineSwitches = new[] {
				new GamelogicEngineSwitch("aaa") {DeviceHint = "some_trough", DeviceItemHint = "1"}
			};
			var switches = table.Mappings.GetSwitchIds(gameEngineSwitches).ToArray();

			switches[0].Id.Should().Be("aaa");
			switches[1].Id.Should().Be("bbb");
		}
	}
}
