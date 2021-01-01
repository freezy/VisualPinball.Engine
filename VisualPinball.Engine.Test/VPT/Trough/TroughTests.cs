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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Engine.Test.VPT.Trough
{
	public class TroughTests
	{
		[Test]
		public void ShouldReturnCorrectSwitchesForModernOpto()
		{
			var data = new TroughData("Trough") {
				TransitionTime = 100,
				RollTime = 1000,
				Type = TroughType.ModernOpto,
				SwitchCount = 3
			};
			var trough = new Engine.VPT.Trough.Trough(data);
			var switches = trough.AvailableSwitches.ToArray();

			trough.RollTimeDisabled.Should().Be(900);
			trough.RollTimeEnabled.Should().Be(100);

			switches.Should().HaveCount(3);
			switches[0].Id.Should().Be("1");
			switches[1].Id.Should().Be("2");
			switches[2].Id.Should().Be("3");
		}

		[Test]
		public void ShouldReturnCorrectCoilsForModernOpto()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.ModernOpto,
			};
			var trough = new Engine.VPT.Trough.Trough(data);
			var coils = trough.AvailableCoils.ToArray();

			coils.Should().HaveCount(1);
			coils[0].Id.Should().Be(Engine.VPT.Trough.Trough.EjectCoilId);
		}

		[Test]
		public void ShouldReturnCorrectSwitchesForModernMechanical()
		{
			var data = new TroughData("Trough") {
				TransitionTime = 100,
				RollTime = 1000,
				Type = TroughType.ModernMech,
				SwitchCount = 3
			};
			var trough = new Engine.VPT.Trough.Trough(data);
			var switches = trough.AvailableSwitches.ToArray();

			trough.RollTimeDisabled.Should().Be(500);
			trough.RollTimeEnabled.Should().Be(500);

			switches.Should().HaveCount(3);
			switches[0].Id.Should().Be("1");
			switches[1].Id.Should().Be("2");
			switches[2].Id.Should().Be("3");
		}

		[Test]
		public void ShouldReturnCorrectCoilsForModernMechanical()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.ModernMech,
			};
			var trough = new Engine.VPT.Trough.Trough(data);
			var coils = trough.AvailableCoils.ToArray();

			coils.Should().HaveCount(1);
			coils[0].Id.Should().Be(Engine.VPT.Trough.Trough.EjectCoilId);
		}

		[Test]
		public void ShouldReturnCorrectSwitchesForTwoCoilsNSwitches()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.TwoCoilsNSwitches,
				SwitchCount = 3
			};
			var trough = new Engine.VPT.Trough.Trough(data);
			var switches = trough.AvailableSwitches.ToArray();

			switches.Should().HaveCount(4);
			switches[0].Id.Should().Be(Engine.VPT.Trough.Trough.EntrySwitchId);
			switches[1].Id.Should().Be("1");
			switches[2].Id.Should().Be("2");
			switches[3].Id.Should().Be("3");
		}

		[Test]
		public void ShouldReturnCorrectCoilsForTwoCoilsNSwitches()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.TwoCoilsNSwitches,
			};
			var trough = new Engine.VPT.Trough.Trough(data);
			var coils = trough.AvailableCoils.ToArray();

			coils.Should().HaveCount(2);
			coils[0].Id.Should().Be(Engine.VPT.Trough.Trough.EntryCoilId);
			coils[1].Id.Should().Be(Engine.VPT.Trough.Trough.EjectCoilId);
		}

		[Test]
		public void ShouldReturnCorrectSwitchesForTwoCoilsOneSwitch()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.TwoCoilsOneSwitch,
			};
			var trough = new Engine.VPT.Trough.Trough(data);
			var switches = trough.AvailableSwitches.ToArray();

			switches.Should().HaveCount(2);
			switches[0].Id.Should().Be(Engine.VPT.Trough.Trough.EntrySwitchId);
			switches[1].Id.Should().Be(Engine.VPT.Trough.Trough.TroughSwitchId);
		}

		[Test]
		public void ShouldReturnCorrectCoilsForTwoCoilsOneSwitch()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.TwoCoilsOneSwitch,
			};
			var trough = new Engine.VPT.Trough.Trough(data);
			var coils = trough.AvailableCoils.ToArray();

			coils.Should().HaveCount(2);
			coils[0].Id.Should().Be(Engine.VPT.Trough.Trough.EntryCoilId);
			coils[1].Id.Should().Be(Engine.VPT.Trough.Trough.EjectCoilId);
		}


		[Test]
		public void ShouldReturnCorrectSwitchesForClassicSingleBall()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.ClassicSingleBall,
			};
			var trough = new Engine.VPT.Trough.Trough(data);
			var switches = trough.AvailableSwitches.ToArray();

			switches.Should().HaveCount(1);
			switches[0].Id.Should().Be(Engine.VPT.Trough.Trough.EntrySwitchId);
		}

		[Test]
		public void ShouldReturnCorrectCoilsForClassicSingleBall()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.ClassicSingleBall,
			};
			var trough = new Engine.VPT.Trough.Trough(data);
			var coils = trough.AvailableCoils.ToArray();

			coils.Should().HaveCount(1);
			coils[0].Id.Should().Be(Engine.VPT.Trough.Trough.EjectCoilId);
		}
	}
}
