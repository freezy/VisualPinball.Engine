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

using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.Test.VPT.Trough;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trough;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
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

			var troughComponent = CreateTrough(data);
			var switches = troughComponent.AvailableSwitches.ToArray();

			troughComponent.RollTimeDisabled.Should().Be(900);
			troughComponent.RollTimeEnabled.Should().Be(100);

			switches.Should().HaveCount(3);
			switches[0].Id.Should().Be("ball_switch_1");
			switches[1].Id.Should().Be("ball_switch_2");
			switches[2].Id.Should().Be("ball_switch_3");
		}

		[Test]
		public void ShouldReturnCorrectCoilsForModernOpto()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.ModernOpto,
			};
			var troughComponent = CreateTrough(data);
			var coils = troughComponent.AvailableCoils.ToArray();

			coils.Should().HaveCount(1);
			coils[0].Id.Should().Be(TroughAuthoring.EjectCoilId);
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
			var troughComponent = CreateTrough(data);
			var switches = troughComponent.AvailableSwitches.ToArray();

			troughComponent.RollTimeDisabled.Should().Be(500);
			troughComponent.RollTimeEnabled.Should().Be(500);

			switches.Should().HaveCount(3);
			switches[0].Id.Should().Be("ball_switch_1");
			switches[1].Id.Should().Be("ball_switch_2");
			switches[2].Id.Should().Be("ball_switch_3");
		}

		[Test]
		public void ShouldReturnCorrectCoilsForModernMechanical()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.ModernMech,
			};
			var troughComponent = CreateTrough(data);
			var coils = troughComponent.AvailableCoils.ToArray();

			coils.Should().HaveCount(1);
			coils[0].Id.Should().Be(TroughAuthoring.EjectCoilId);
		}

		[Test]
		public void ShouldReturnCorrectSwitchesForTwoCoilsNSwitches()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.TwoCoilsNSwitches,
				SwitchCount = 3
			};
			var troughComponent = CreateTrough(data);
			var switches = troughComponent.AvailableSwitches.ToArray();

			switches.Should().HaveCount(4);
			switches[0].Id.Should().Be(TroughAuthoring.EntrySwitchId);
			switches[1].Id.Should().Be("ball_switch_1");
			switches[2].Id.Should().Be("ball_switch_2");
			switches[3].Id.Should().Be("ball_switch_3");
		}

		[Test]
		public void ShouldReturnCorrectCoilsForTwoCoilsNSwitches()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.TwoCoilsNSwitches,
			};
			var troughComponent = CreateTrough(data);
			var coils = troughComponent.AvailableCoils.ToArray();

			coils.Should().HaveCount(2);
			coils[0].Id.Should().Be(TroughAuthoring.EntryCoilId);
			coils[1].Id.Should().Be(TroughAuthoring.EjectCoilId);
		}

		[Test]
		public void ShouldReturnCorrectSwitchesForTwoCoilsOneSwitch()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.TwoCoilsOneSwitch,
			};
			var troughComponent = CreateTrough(data);
			var switches = troughComponent.AvailableSwitches.ToArray();

			switches.Should().HaveCount(2);
			switches[0].Id.Should().Be(TroughAuthoring.EntrySwitchId);
			switches[1].Id.Should().Be(TroughAuthoring.TroughSwitchId);
		}

		[Test]
		public void ShouldReturnCorrectCoilsForTwoCoilsOneSwitch()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.TwoCoilsOneSwitch,
			};
			var troughComponent = CreateTrough(data);
			var coils = troughComponent.AvailableCoils.ToArray();

			coils.Should().HaveCount(2);
			coils[0].Id.Should().Be(TroughAuthoring.EntryCoilId);
			coils[1].Id.Should().Be(TroughAuthoring.EjectCoilId);
		}


		[Test]
		public void ShouldReturnCorrectSwitchesForClassicSingleBall()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.ClassicSingleBall,
			};
			var troughComponent = CreateTrough(data);
			var switches = troughComponent.AvailableSwitches.ToArray();

			switches.Should().HaveCount(1);
			switches[0].Id.Should().Be(TroughAuthoring.EntrySwitchId);
		}

		[Test]
		public void ShouldReturnCorrectCoilsForClassicSingleBall()
		{
			var data = new TroughData("Trough") {
				Type = TroughType.ClassicSingleBall,
			};
			var troughComponent = CreateTrough(data);
			var coils = troughComponent.AvailableCoils.ToArray();

			coils.Should().HaveCount(1);
			coils[0].Id.Should().Be(TroughAuthoring.EjectCoilId);
		}

		private static TroughAuthoring CreateTrough(TroughData data)
		{
			var table = new TableBuilder().AddTrough(data).Build();
			var go = VpxImportEngine.ImportIntoScene(table, options: ConvertOptions.SkipNone);
			var tableComponent = go.GetComponent<TableAuthoring>();
			return tableComponent.transform.Find($"Playfield/{data.Name}").GetComponent<TroughAuthoring>();
		}

		#if !WRITE_VP106 && !WRITE_VP107

		[Test]
		public void ShouldWriteImportedTroughData()
		{
			const string tmpFileName = "ShouldWriteTroughData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Trough, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableAuthoring>();
			ta.TableContainer.Save(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			TroughDataTests.ValidateTroughData(writtenTable.Trough("Trough1").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		#endif
	}
}
