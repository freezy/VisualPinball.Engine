// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

namespace VisualPinball.Unity.Test
{
	public class FrameBoundTests
	{
		private static FrameWindowTimings Window(float frame, float main, float render, float gpu, float wait, int samples = 60)
			=> new FrameWindowTimings { FrameMs = frame, CpuMainMs = main, CpuRenderMs = render, GpuMs = gpu, PresentWaitMs = wait, Samples = samples };

		[Test]
		public void ShouldBeGpuBoundWhenTheGpuTakesLongest()
		{
			FrameBoundClassifier.Classify(Window(12f, 4f, 3f, 11.5f, 0f), 0f).Should().Be(FrameBound.Gpu);
		}

		[Test]
		public void ShouldBeCpuBoundWhenTheMainThreadTakesLongest()
		{
			FrameBoundClassifier.Classify(Window(16f, 15f, 6f, 8f, 0f), 0f).Should().Be(FrameBound.CpuMain);
		}

		[Test]
		public void ShouldBeRenderThreadBoundWhenItTakesLongest()
		{
			FrameBoundClassifier.Classify(Window(16f, 6f, 15f, 8f, 0f), 0f).Should().Be(FrameBound.CpuRender);
		}

		[Test]
		public void ShouldNotCallItGpuBoundWhenTheGpuIsOnlySlightlySlower()
		{
			// within 10 %: the CPU is about as busy, so it's not the GPU alone
			FrameBoundClassifier.Classify(Window(16f, 14f, 6f, 15f, 0f), 0f).Should().Be(FrameBound.CpuMain);
		}

		[Test]
		public void ShouldBeCappedWhenVsyncHoldsItBack()
		{
			// 60 Hz vsync, the main thread waits 9 ms of 16.7 for the present
			FrameBoundClassifier.Classify(Window(16.7f, 5f, 4f, 7f, 9f), 16.67f).Should().Be(FrameBound.Capped);
		}

		[Test]
		public void ShouldBeUnknownWhenCappedButTheGpuTimeIsMissing()
		{
			FrameBoundClassifier.Classify(Window(16.7f, 5f, 4f, 0f, 9f), 16.67f).Should().Be(FrameBound.Unknown);
		}

		[Test]
		public void ShouldBeUnknownWhenCappedButThePresentWaitIsMissing()
		{
			// some D3D12 drivers report no present wait: can't tell capped from bound
			FrameBoundClassifier.Classify(Window(16.7f, 5f, 4f, 7f, 0f), 16.67f).Should().Be(FrameBound.Unknown);
		}

		[Test]
		public void ShouldClassifyNormallyWhenBelowTheCap()
		{
			// 60 Hz vsync but only reaching 40 fps: the GPU is what's slow
			FrameBoundClassifier.Classify(Window(25f, 6f, 5f, 24f, 0.5f), 16.67f).Should().Be(FrameBound.Gpu);
		}

		[Test]
		public void ShouldBeUnknownWithoutGpuTiming()
		{
			FrameBoundClassifier.Classify(Window(16f, 10f, 5f, 0f, 0f), 0f).Should().Be(FrameBound.Unknown);
		}

		[Test]
		public void ShouldBeUnknownWithTooFewSamples()
		{
			FrameBoundClassifier.Classify(Window(12f, 4f, 3f, 11.5f, 0f, samples: 3), 0f).Should().Be(FrameBound.Unknown);
		}

		[TestCase(FrameBound.Gpu, "gpu")]
		[TestCase(FrameBound.CpuMain, "cpu-main")]
		[TestCase(FrameBound.CpuRender, "cpu-render")]
		[TestCase(FrameBound.Capped, "capped")]
		[TestCase(FrameBound.Unknown, "unknown")]
		public void ShouldNameTheBound(FrameBound bound, string name)
		{
			FrameBoundClassifier.Name(bound).Should().Be(name);
		}
	}
}
