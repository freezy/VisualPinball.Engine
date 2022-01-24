﻿// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.VPT.HitTarget
{
	public class HitTargetDataTests
	{
		[Test]
		public void ShouldReadHitTargetData()
		{
			var table = FileTableContainer.Load(VpxPath.HitTarget);
			ValidateHitTargetData(table.HitTarget("Data").Data);
		}

		[Test]
		public void ShouldWriteHitTargetData()
		{
			const string tmpFileName = "ShouldWriteHitTargetData.vpx";
			var table = FileTableContainer.Load(VpxPath.HitTarget);
			table.Save(tmpFileName);
			var writtenTable = FileTableContainer.Load(tmpFileName);
			ValidateHitTargetData(writtenTable.HitTarget("Data").Data);
			File.Delete(tmpFileName);
		}

		public static void ValidateHitTargetData(HitTargetData data)
		{
			data.DepthBias.Should().Be(0.651f);
			data.DisableLightingBelow.Should().Be(0.1932f);
			data.DisableLightingTop.Should().Be(0.2f);
			data.DropSpeed.Should().Be(0.5982f);
			data.Elasticity.Should().Be(0.9287f);
			data.ElasticityFalloff.Should().Be(0.1897f);
			data.Friction.Should().Be(1f);
			data.Image.Should().Be("");
			data.IsCollidable.Should().Be(true);
			data.IsDropped.Should().Be(false);
			data.IsDropTarget.Should().Be(false);
			data.IsLegacy.Should().Be(false);
			data.IsReflectionEnabled.Should().Be(true);
			data.IsVisible.Should().Be(true);
			data.Material.Should().Be("Playfield");
			data.OverwritePhysics.Should().Be(true);
			data.PhysicsMaterial.Should().Be("");
			data.Position.X.Should().Be(427.12f);
			data.Position.Y.Should().Be(1079.21f);
			data.Position.Z.Should().Be(12.3221f);
			data.RaiseDelay.Should().Be(216);
			data.RotZ.Should().Be(2.124f);
			data.Scatter.Should().Be(5.12354f);
			data.Size.X.Should().Be(32.32f);
			data.Size.Y.Should().Be(32.44f);
			data.Size.Z.Should().Be(32.5055f);
			data.TargetType.Should().Be(TargetType.HitFatTargetRectangle);
			data.Threshold.Should().Be(3.2124f);
			data.UseHitEvent.Should().Be(true);
		}
	}
}
