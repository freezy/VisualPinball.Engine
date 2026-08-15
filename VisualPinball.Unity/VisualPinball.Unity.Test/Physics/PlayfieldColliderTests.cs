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

using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class PlayfieldColliderTests
	{
		[TestCase(true, 4)]
		[TestCase(false, 3)]
		public void BacksideBoundsColliderCanBeDisabled(bool collideWithBackside, int expectedLineColliders)
		{
			var gameObject = new GameObject("Playfield");
			var nonTransformableColliderTransforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var colliders = new ColliderReference(ref nonTransformableColliderTransforms, Allocator.Temp);

			try {
				var playfield = gameObject.AddComponent<PlayfieldComponent>();
				playfield.Left = 0f;
				playfield.Right = 100f;
				playfield.Top = 0f;
				playfield.Bottom = 200f;
				playfield.GlassHeight = 50f;

				var colliderComponent = gameObject.AddComponent<PlayfieldColliderComponent>();
				colliderComponent.CollideWithBounds = true;
				colliderComponent.CollideWithBackside = collideWithBackside;

				var api = new PlayfieldApi(gameObject, null, null);
				((IApiColliderGenerator)api).CreateColliders(ref colliders, float4x4.identity, 0f);

				Assert.That(colliders.PlaneColliders.Length, Is.EqualTo(2));
				Assert.That(colliders.LineColliders.Length, Is.EqualTo(expectedLineColliders));
				Assert.That(CountBacksideColliders(colliders, playfield.Top), Is.EqualTo(collideWithBackside ? 1 : 0));
			} finally {
				colliders.Dispose();
				nonTransformableColliderTransforms.Dispose();
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void BacksideColliderSettingIsPacked()
		{
			var gameObject = new GameObject("Playfield");

			try {
				var colliderComponent = gameObject.AddComponent<PlayfieldColliderComponent>();
				colliderComponent.CollideWithBackside = false;

				var bytes = colliderComponent.Pack();
				colliderComponent.CollideWithBackside = true;
				colliderComponent.Unpack(bytes);

				Assert.That(colliderComponent.CollideWithBackside, Is.False);
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void PackedColliderWithoutBacksideSettingKeepsCurrentBehavior()
		{
			var gameObject = new GameObject("Playfield");

			try {
				var colliderComponent = gameObject.AddComponent<PlayfieldColliderComponent>();
				var bytes = Encoding.UTF8.GetBytes("{\"Gravity\":0.97,\"DefaultScatter\":0.0,\"CollideWithBounds\":true}");

				colliderComponent.Unpack(bytes);

				Assert.That(colliderComponent.CollideWithBackside, Is.True);
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		private static int CountBacksideColliders(ColliderReference colliders, float top)
		{
			var count = 0;
			for (var i = 0; i < colliders.LineColliders.Length; i++) {
				var line = colliders.LineColliders[i];
				if (math.abs(line.V1.y - top) < math.EPSILON && math.abs(line.V2.y - top) < math.EPSILON) {
					count++;
				}
			}
			return count;
		}
	}
}
