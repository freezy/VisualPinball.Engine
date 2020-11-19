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
using VisualPinball.Engine.VPT.Mappings;

namespace VisualPinball.Engine.Test.VPT.Mappings
{
	public class MappingsDataTests
	{
		[Test]
		public void ShouldReadMappingsData()
		{
			var table = Engine.VPT.Table.Table.Load(VpxPath.Mappings);
			var data = table.Mappings.Data;
			ValidateTableData(data);
		}

		[Test]
		public void ShouldWriteMappingsData()
		{
			const string tmpFileName = "ShouldWriteMappingsData.vpx";
			var table = Engine.VPT.Table.Table.Load(VpxPath.Mappings);
			table.Save(tmpFileName);
			var writtenTable = Engine.VPT.Table.Table.Load(tmpFileName);
			ValidateTableData(writtenTable.Mappings.Data);
		}

		private static void ValidateTableData(MappingsData data)
		{
			data.Switches.Length.Should().Be(13);

			data.Switches[0].Id.Should().Be("s_create_ball");
			data.Switches[0].Description.Should().Be("Create Ball");
			data.Switches[0].Source.Should().Be(SwitchSource.InputSystem);
			data.Switches[0].InputActionMap.Should().Be("Visual Pinball Engine");
			data.Switches[0].InputAction.Should().Be("Create Ball");
			data.Switches[0].Type.Should().Be(SwitchType.OnOff);

			data.Switches[1].Id.Should().Be("s_left_flipper");
			data.Switches[1].Description.Should().Be("Left Flipper");
			data.Switches[1].Source.Should().Be(SwitchSource.InputSystem);
			data.Switches[1].InputActionMap.Should().Be("Cabinet Switches");
			data.Switches[1].InputAction.Should().Be("Left Flipper");
			data.Switches[1].Type.Should().Be(SwitchType.OnOff);

			data.Switches[2].Id.Should().Be("s_plunger");
			data.Switches[2].Description.Should().Be("Plunger");
			data.Switches[2].Source.Should().Be(SwitchSource.Playfield);
			data.Switches[2].PlayfieldItem.Should().Be("Bumper1");
			data.Switches[2].Type.Should().Be(SwitchType.Pulse);
			data.Switches[2].PulseDelay.Should().Be(20);

			data.Switches[3].Id.Should().Be("s_right_flipper");
			data.Switches[3].Description.Should().Be("Right Flipper");
			data.Switches[3].Source.Should().Be(SwitchSource.Constant);
			data.Switches[3].Constant.Should().Be(SwitchConstant.NormallyClosed);

			data.Switches[3].Id.Should().Be("s_right_flipper");
			data.Switches[3].Description.Should().Be("Right Flipper");
			data.Switches[3].Source.Should().Be(SwitchSource.Constant);
			data.Switches[3].Constant.Should().Be(SwitchConstant.NormallyClosed);

			data.Switches[7].Id.Should().Be("s_trough1");
			data.Switches[7].Description.Should().Be("Trough 1 (eject)");
			data.Switches[7].Source.Should().Be(SwitchSource.Device);
			data.Switches[7].Device.Should().Be("Trough");
			data.Switches[7].DeviceItem.Should().Be("1");
			data.Switches[7].Type.Should().Be(SwitchType.OnOff);
			data.Switches[7].PulseDelay.Should().Be(250);

			data.Coils.Length.Should().Be(6);

			data.Coils[0].Id.Should().Be("c_auto_plunger");
			data.Coils[0].Description.Should().Be("Auto Plunger");
			data.Coils[0].Destination.Should().Be(CoilDestination.Playfield);
			data.Coils[0].PlayfieldItem.Should().Be("Plunger1");
			data.Coils[0].Type.Should().Be(CoilType.SingleWound);

			data.Coils[1].Id.Should().Be("c_left_flipper");
			data.Coils[1].Description.Should().Be("Left Flipper");
			data.Coils[1].Destination.Should().Be(CoilDestination.Playfield);
			data.Coils[1].PlayfieldItem.Should().Be("Flipper1");
			data.Coils[1].Type.Should().Be(CoilType.DualWound);
			data.Coils[1].HoldCoilId.Should().Be("c_left_flipper_hold");

			data.Coils[5].Id.Should().Be("c_trough_eject");
			data.Coils[5].Description.Should().Be("Trough Eject");
			data.Coils[5].Destination.Should().Be(CoilDestination.Device);
			data.Coils[5].Device.Should().Be("Trough");
			data.Coils[5].DeviceItem.Should().Be("eject");
			data.Coils[5].Type.Should().Be(CoilType.SingleWound);
			data.Coils[5].HoldCoilId.Should().Be("");

			data.Wires.Length.Should().Be(2);

			data.Wires[0].Description.Should().Be("Left Flipper Input Activate Bumper");
			data.Wires[0].Source.Should().Be(SwitchSource.InputSystem);
			data.Wires[0].SourceInputActionMap.Should().Be("Cabinet Switches");
			data.Wires[0].SourceInputAction.Should().Be("Left Flipper");
			data.Wires[0].Destination.Should().Be(WireDestination.Playfield);
			data.Wires[0].DestinationPlayfieldItem.Should().Be("Bumper1");

			data.Wires[1].Description.Should().Be("Bumper1 Activate Bumper2");
			data.Wires[1].Source.Should().Be(SwitchSource.Playfield);
			data.Wires[1].SourcePlayfieldItem.Should().Be("Bumper1");
			data.Wires[1].Destination.Should().Be(WireDestination.Playfield);
			data.Wires[1].DestinationPlayfieldItem.Should().Be("Bumper2");
			data.Wires[1].PulseDelay.Should().Be(200);
		}
	}
}
