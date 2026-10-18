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
	/// Physics fixes ported from Visual Pinball, mostly its FIX_PHYSICS path.
	/// </summary>
	public class PhysicsAlignmentTests
	{
		private const float Tolerance = 1e-5f;

		#region Wall touch band

		[Test]
		public void SlowWallTouchIsAContact()
		{
			// wall along y, facing +x
			var wall = new LineCollider(new float2(0f, 100f), new float2(0f, -100f), 0f, 50f, new ColliderInfo { ItemId = 1 });
			var ball = new BallState {
				Mass = 1f,
				Radius = 25f,
				Position = new float3(25.02f, 0f, 25f), // 0.02 off the wall: inside the touch band
				Velocity = new float3(-0.05f, 0f, 0f), // slower than ContactVel
			};
			var insideOfs = default(InsideOfs);
			var collEvent = new CollisionEventData();

			var hitTime = wall.HitTest(ref collEvent, ref insideOfs, in ball, PhysicsConstants.PhysFactor);

			Assert.That(hitTime, Is.Zero, "a slow touch is a contact now, not a collision at a remapped time");
			Assert.That(collEvent.IsContact, Is.True);
			Assert.That(collEvent.HitOrgNormalVelocity, Is.EqualTo(-0.05f).Within(Tolerance));
			Assert.That(collEvent.HitDistance, Is.EqualTo(0.02f).Within(1e-4f));
		}

		#endregion
	}
}
