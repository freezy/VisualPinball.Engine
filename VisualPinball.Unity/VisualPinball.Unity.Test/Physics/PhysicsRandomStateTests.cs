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

using NUnit.Framework;
using Random = Unity.Mathematics.Random;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The collide pass draws scatter from <c>PhysicsState.Env.Random</c>, and that
	/// environment is a copy taken when the tick's state is created. Unless the tick
	/// writes the advanced generator back, every tick starts from the same state and
	/// scatter repeats instead of being one running sequence.
	/// </summary>
	public class PhysicsRandomStateTests
	{
		private const uint Seed = 20260918;

		[Test]
		public void ScatterDrawsShouldContinueAcrossTicks()
		{
			var reference = new Random(Seed);
			var first = reference.NextFloat(-1f, 1f);
			var second = reference.NextFloat(-1f, 1f);
			var third = reference.NextFloat(-1f, 1f);

			using var harness = new PhysicsStateHarness();
			harness.Env.Random = new Random(Seed);

			var tickOne = harness.CreateState();
			var drawOne = tickOne.Env.Random.NextFloat(-1f, 1f);
			PhysicsEngineContext.CommitRandom(ref harness.Env, in tickOne);

			var tickTwo = harness.CreateState();
			var drawTwo = tickTwo.Env.Random.NextFloat(-1f, 1f);
			PhysicsEngineContext.CommitRandom(ref harness.Env, in tickTwo);

			var tickThree = harness.CreateState();
			var drawThree = tickThree.Env.Random.NextFloat(-1f, 1f);

			Assert.That(drawOne, Is.EqualTo(first), "the first tick draws the first value of the seeded sequence");
			Assert.That(drawTwo, Is.EqualTo(second), "the second tick continues the sequence where the first tick left it");
			Assert.That(drawThree, Is.EqualTo(third), "and so does the third");
			Assert.That(drawTwo, Is.Not.EqualTo(drawOne), "consecutive ticks must not repeat the same draw");
		}

		[Test]
		public void TheSequenceShouldBeDeterministicForASeed()
		{
			var runOne = DrawAcrossTicks(Seed, 5);
			var runTwo = DrawAcrossTicks(Seed, 5);
			Assert.That(runOne, Is.EqualTo(runTwo), "the same seed must reproduce the same scatter across ticks");
			Assert.That(runOne, Is.Unique, "five ticks must not repeat a draw");
		}

		/// <summary>
		/// Documents the mechanism this fixes: a state's environment is a copy, so a draw
		/// that is not committed back leaves the environment untouched and the next tick
		/// repeats it.
		/// </summary>
		[Test]
		public void AnUncommittedDrawShouldLeaveTheEnvironmentUntouched()
		{
			using var harness = new PhysicsStateHarness();
			harness.Env.Random = new Random(Seed);

			var tickOne = harness.CreateState();
			var drawOne = tickOne.Env.Random.NextFloat(-1f, 1f);

			var tickTwo = harness.CreateState();
			var drawTwo = tickTwo.Env.Random.NextFloat(-1f, 1f);

			Assert.That(drawTwo, Is.EqualTo(drawOne), "without the write-back the next tick starts from the same generator state");
		}

		private static float[] DrawAcrossTicks(uint seed, int ticks)
		{
			using var harness = new PhysicsStateHarness();
			harness.Env.Random = new Random(seed);
			var draws = new float[ticks];
			for (var i = 0; i < ticks; i++) {
				var state = harness.CreateState();
				draws[i] = state.Env.Random.NextFloat(-1f, 1f);
				PhysicsEngineContext.CommitRandom(ref harness.Env, in state);
			}
			return draws;
		}
	}
}
