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
using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The broad phase queries with the volume a ball can reach within the
	/// searched time, not with VP's full-step box.
	/// </summary>
	public class BallSweptBoundsTests
	{
		[Test]
		public void SweptBoundsCoverTheSearchedTimeOnly()
		{
			var ball = new BallState { Radius = 25f, Position = new float3(100f, 200f, 25f), Velocity = new float3(60f, -30f, 0f) };

			// one sub cycle: the ball moves 6 in x and -3 in y
			var swept = ball.GetSweptAabb(PhysicsConstants.PhysFactor);
			Assert.That(swept.Left, Is.EqualTo(100f - 25.05f).Within(1e-4f));
			Assert.That(swept.Right, Is.EqualTo(106f + 25.05f).Within(1e-4f));
			Assert.That(swept.Top, Is.EqualTo(197f - 25.05f).Within(1e-4f));
			Assert.That(swept.Bottom, Is.EqualTo(200f + 25.05f).Within(1e-4f));
			Assert.That(swept.ZLow, Is.EqualTo(25f - 25.05f).Within(1e-4f));
			Assert.That(swept.ZHigh, Is.EqualTo(25f + 25.05f).Within(1e-4f));

			// the end position of the motion is inside, VP's full-step reach is not
			var end = ball.Position + ball.Velocity * PhysicsConstants.PhysFactor;
			Assert.That(Contains(swept, end), Is.True);
			Assert.That(Contains(swept, ball.Position + ball.Velocity), Is.False);
			Assert.That(Contains(ball.Aabb, ball.Position + ball.Velocity), Is.True, "the conservative box still covers the full step");
		}

		private static bool Contains(in Aabb aabb, float3 p)
			=> p.x >= aabb.Left && p.x <= aabb.Right && p.y >= aabb.Top && p.y <= aabb.Bottom && p.z >= aabb.ZLow && p.z <= aabb.ZHigh;

		[Test]
		public void SweptBoundsOfARestingBallAreTheBallPlusMargin()
		{
			var ball = new BallState { Radius = 25f, Position = new float3(10f, 20f, 25f) };
			var swept = ball.GetSweptAabb(PhysicsConstants.PhysFactor);
			Assert.That(swept.Left, Is.EqualTo(10f - 25.05f).Within(1e-4f));
			Assert.That(swept.Right, Is.EqualTo(10f + 25.05f).Within(1e-4f));
			Assert.That(swept.Width, Is.EqualTo(ball.Aabb.Width).Within(1e-4f));

			// a negative or zero search window degenerates to the same box
			Assert.That(ball.GetSweptAabb(0f).Width, Is.EqualTo(swept.Width).Within(1e-4f));
			Assert.That(ball.GetSweptAabb(-1f).Width, Is.EqualTo(swept.Width).Within(1e-4f));
		}
	}
}
