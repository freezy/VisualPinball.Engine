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

namespace VisualPinball.Unity.Test
{
	public class Line3DColliderTests
	{
		[Test]
		public void HitNormalIsTransformedBackToWorldSpace()
		{
			var collider = new Line3DCollider(
				new float3(0f, 0f, 0f),
				new float3(10f, 0f, 0f),
				new ColliderInfo { ItemId = 1 }
			);
			var ball = new BallState {
				Position = new float3(5f, 2f, 2f),
				Velocity = new float3(0f, -1f, -1f),
				Radius = 1f
			};
			var collEvent = new CollisionEventData();

			var hitTime = collider.HitTest(ref collEvent, in ball, 2f);

			Assert.That(hitTime, Is.GreaterThanOrEqualTo(0f));
			Assert.That(collEvent.HitNormal.x, Is.EqualTo(0f).Within(1e-5f));
			Assert.That(collEvent.HitNormal.y, Is.EqualTo(math.sqrt(0.5f)).Within(1e-5f));
			Assert.That(collEvent.HitNormal.z, Is.EqualTo(math.sqrt(0.5f)).Within(1e-5f));
		}
	}
}
