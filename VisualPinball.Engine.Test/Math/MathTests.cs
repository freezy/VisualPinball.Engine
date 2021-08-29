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
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.Test.Common
{
	public class MathTests
	{
		[Test]
		public void ShouldCorrectlyInitializeRect3D()
		{
			var rect = new Rect3D(1f, 2f, 3f, 4f, 5f, 6f);
			rect.Left.Should().Be(1f);
			rect.Right.Should().Be(2f);
			rect.Top.Should().Be(3f);
			rect.Bottom.Should().Be(4f);
			rect.ZLow.Should().Be(5f);
			rect.ZHigh.Should().Be(6f);
		}

		[Test]
		public void ShouldCorrectlyMeasureRect3D()
		{
			var rect = new Rect3D(1f, 2.5f, 3f, 4.6f, 5f, 6.8f);
			rect.Width.Should().Be(1.5f);
			rect.Height.Should().BeApproximately(1.6f, 0.000001f);
			rect.Depth.Should().BeApproximately(1.8f, 0.000001f);
		}
	}
}
