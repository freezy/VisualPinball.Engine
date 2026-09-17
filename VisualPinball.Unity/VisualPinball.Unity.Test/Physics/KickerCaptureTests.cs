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
using Unity.Collections;
using Unity.Mathematics;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// A ball created inside a kicker must end up captured at the kicker's capture
	/// position, so that the next kick launches it from there.
	/// </summary>
	public class KickerCaptureTests
	{
		private const int KickerId = 42;
		private static readonly float3 CreationPosition = new(769f, 1868f, 25f);

		private static KickerStaticState SunkenKicker() => new() {
			Center = new float2(769f, 1868f),
			ZLow = -108f,
			FallIn = true,
			FallThrough = false,
			HitAccuracy = 1f,
			Scatter = 0f,
			LegacyMode = true,
		};

		private static void CreateBall(ref BallState ball, ref KickerCollisionState collState, in KickerStaticState staticState, bool staleMembership = false)
		{
			var events = new NativeQueue<EventData>(Allocator.Temp);
			var insideOfs = new InsideOfs(Allocator.Temp);
			try {
				if (staleMembership) {
					insideOfs.SetInsideOf(KickerId, ball.Id);
				}
				var writer = events.AsParallelWriter();
				ball.CollisionEvent.HitFlag = true; // as KickerApi.CreateBall does
				var collEvent = ball.CollisionEvent;
				KickerCollider.Collide(new float3(staticState.Center, staticState.ZLow), ref ball, ref writer,
					ref insideOfs, ref collState, in staticState, default, in collEvent, KickerId, true);
			} finally {
				insideOfs.Dispose();
				events.Dispose();
			}
		}

		[Test]
		public void CreatedBallIsCapturedAtTheKickerHeight()
		{
			var ball = new BallState { Id = 7, Radius = 25f, Mass = 1f, Position = CreationPosition, Velocity = new float3(0.1f, 0f, 0f) };
			var collState = new KickerCollisionState();

			CreateBall(ref ball, ref collState, SunkenKicker());

			Assert.That(collState.BallId, Is.EqualTo(7));
			Assert.That(ball.IsFrozen, Is.True);
			Assert.That(ball.Position.z, Is.EqualTo(-108f + 25f).Within(0.01f), "held at the kicker, not at the creation height");
			Assert.That(math.length(ball.Velocity), Is.EqualTo(0f));
		}

		[Test]
		public void StaleReferenceToTheSameBallIdDoesNotBlockTheCapture()
		{
			// the kicker still references a ball id that a previous ball had, and the new ball got that id
			var ball = new BallState { Id = 7, Radius = 25f, Mass = 1f, Position = CreationPosition };
			var collState = new KickerCollisionState { BallId = 7 };

			CreateBall(ref ball, ref collState, SunkenKicker());

			Assert.That(collState.BallId, Is.EqualTo(7));
			Assert.That(ball.IsFrozen, Is.True);
			Assert.That(ball.Position.z, Is.EqualTo(-108f + 25f).Within(0.01f));
		}

		[Test]
		public void StaleVolumeMembershipDoesNotBlockTheCapture()
		{
			// the kicker's volume set still lists the new ball's id from a previous ball
			var ball = new BallState { Id = 7, Radius = 25f, Mass = 1f, Position = CreationPosition };
			var collState = new KickerCollisionState();

			CreateBall(ref ball, ref collState, SunkenKicker(), staleMembership: true);

			Assert.That(collState.BallId, Is.EqualTo(7));
			Assert.That(ball.IsFrozen, Is.True);
			Assert.That(ball.Position.z, Is.EqualTo(-108f + 25f).Within(0.01f));
		}

		[Test]
		public void AnotherHeldBallKeepsTheKicker()
		{
			var ball = new BallState { Id = 7, Radius = 25f, Mass = 1f, Position = CreationPosition };
			var collState = new KickerCollisionState { BallId = 3 };

			CreateBall(ref ball, ref collState, SunkenKicker());

			Assert.That(collState.BallId, Is.EqualTo(3), "the held ball is not displaced");
			Assert.That(ball.IsFrozen, Is.False);
			Assert.That(ball.Position.z, Is.EqualTo(25f).Within(0.01f), "the new ball stays where it was created");
		}
	}
}
