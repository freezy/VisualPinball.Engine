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

using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.MappingConfig;

namespace VisualPinball.Engine.Test.VPT.MappingConfig
{
	public class MappingConfigDataTests
	{
		[Test]
		public void ShouldReadMappingConfigData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.MappingConfig);
			var data = table.MappingConfigs["switch"].Data;
			ValidateTableData(data);
		}

		[Test]
		public void ShouldWriteMappingConfigData()
		{
			const string tmpFileName = "ShouldWriteMappingConfigData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.MappingConfig);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTableData(writtenTable.MappingConfigs["switch"].Data);
		}

		private static void ValidateTableData(MappingConfigData data)
		{
			data.Name.Should().Be("Switch");
			data.MappingEntries.Length.Should().Be(4);

			data.MappingEntries[0].Id.Should().Be("1");
			data.MappingEntries[0].Description.Should().Be("Flipper");
			data.MappingEntries[0].Source.Should().Be(SwitchSource.Playfield);
			data.MappingEntries[0].PlayfieldItem.Should().Be("Flipper001");
			data.MappingEntries[0].Type.Should().Be(SwitchType.OnOff);

			data.MappingEntries[1].Id.Should().Be("2");
			data.MappingEntries[1].Description.Should().Be("Flipper");
			data.MappingEntries[1].Source.Should().Be(SwitchSource.Playfield);
			data.MappingEntries[1].PlayfieldItem.Should().Be("Flipper002");
			data.MappingEntries[1].Type.Should().Be(SwitchType.OnOff);

			data.MappingEntries[2].Id.Should().Be("1");
			data.MappingEntries[2].Description.Should().Be("Input");
			data.MappingEntries[2].Source.Should().Be(SwitchSource.InputSystem);
			data.MappingEntries[2].InputActionMap.Should().Be("Cabinet Switches");
			data.MappingEntries[2].InputAction.Should().Be("Upper Left Flipper");
			data.MappingEntries[2].Type.Should().Be(SwitchType.Pulse);
			data.MappingEntries[2].Pulse.Should().Be(20);

			data.MappingEntries[3].Id.Should().Be("3");
			data.MappingEntries[3].Description.Should().Be("Open Switch");
			data.MappingEntries[3].Source.Should().Be(SwitchSource.Constant);
			data.MappingEntries[3].Constant.Should().Be(SwitchConstant.NormallyOpen);
		}
	}
}
