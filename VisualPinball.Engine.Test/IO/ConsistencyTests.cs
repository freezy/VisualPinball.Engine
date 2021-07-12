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

using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.IO
{
	public class ConsistencyTests
	{
		//todo renable[Test]
		public void ShouldClearWrongMaterialReference()
		{
			const string tmpFileName = "ShouldClearWrongMaterialReference.vpx";

			var th = new TableBuilder()
				.AddBumper("Bumper1")
				.AddMaterial(new Material("DoesExist"))
				.Build();

			th.Bumper("Bumper1").Data.BaseMaterial = "DoesExist";
			th.Bumper("Bumper1").Data.CapMaterial = "DoesNotExist";

			th.Save(tmpFileName);

			th.Bumper("Bumper1").Data.BaseMaterial.Should().Be("DoesExist");
			th.Bumper("Bumper1").Data.CapMaterial.Should().BeEmpty();
		}

		//todo renable[Test]
		public void ShouldClearWrongTextureReference()
		{
			const string tmpFileName = "ShouldClearWrongTextureReference.vpx";

			var table = new TableBuilder()
				.AddFlipper("Flipper")
				.AddTexture("DoesExist")
				.Build();

			table.Flipper("Flipper").Data.Image = "DoesExist";
			table.Save(tmpFileName);
			table.Flipper("Flipper").Data.Image.Should().Be("DoesExist");

			table.Flipper("Flipper").Data.Image = "DoesNotExist";
			table.Save(tmpFileName);
			table.Flipper("Flipper").Data.Image.Should().BeEmpty();
		}
	}
}
