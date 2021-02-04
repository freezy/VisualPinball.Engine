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

using System;
using FluentAssertions;
using NUnit.Framework;
using VisualPinball.Engine.Common;

namespace VisualPinball.Engine.Test.Common
{
	public class EngineTests
	{
		[Test]
		public void ShouldFailIfNoneSet()
		{
			Action act = () => EngineProvider<ITestEngine>.Get();
			act.Should().Throw<InvalidOperationException >()
				.WithMessage("Must select VisualPinball.Engine.Test.Common.ITestEngine engine before retrieving!");
		}

		[Test]
		public void ShouldProvideIfSet()
		{
			EngineProvider<ITestEngine>.Set("VisualPinball.Engine.Test.Common.TestEngine1");
			var engine = EngineProvider<ITestEngine>.Get();

			engine.Should().NotBeNull();
			engine.Name.Should().Be("Engine 1");

			EngineProvider<ITestEngine>.Set("VisualPinball.Engine.Test.Common.TestEngine2");
			engine = EngineProvider<ITestEngine>.Get();

			engine.Should().NotBeNull();
			engine.Name.Should().Be("Engine 2");
		}
	}

	internal interface ITestEngine : IEngine
	{

	}

	internal class TestEngine1 : ITestEngine
	{

		public string Name { get; } = "Engine 1";
	}

	internal class TestEngine2 : ITestEngine
	{

		public string Name { get; } = "Engine 2";
	}
}
