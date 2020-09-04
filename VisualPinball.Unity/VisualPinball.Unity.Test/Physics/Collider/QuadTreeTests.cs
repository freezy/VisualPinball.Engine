﻿// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using System.Collections.Generic;
using NUnit.Framework;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Test
{
	[TestFixture]
	[Category("Visual Pinball")]
	public class QuadTreeTests
	{
		[Test]
		public unsafe void ShouldSerializeCorrectly()
		{
			// var bounds = new Rect3D(true);
			// var hitQuad = new HitQuadTree(new List<HitObject> {
			// 	new LineSeg(new Vertex2D(1f, 2f), new Vertex2D(3f, 4f), 5f, 6f, ItemType.Table),
			// 	new HitCircle(new Vertex2D(7f, 8f), 9f, 10f, 11f, ItemType.Table),
			// 	new LineSeg(new Vertex2D(12f, 13f), new Vertex2D(14f, 15f), 16f, 17f, ItemType.Table),
			// }, bounds);
			//
			// var quadTreeBlobAssetRef = QuadTreeBlob.CreateBlobAssetReference(
			// 	hitQuad,
			// 	new HitPlane(new Vertex3D(0, 0, 1), 10f),
			// 	new HitPlane(new Vertex3D(0, 0, -1), 20f)
			// 	);
			// ref var collider1 = ref quadTreeBlobAssetRef.Value.QuadTree.Bounds[0].Value;
			// ref var collider2 = ref quadTreeBlobAssetRef.Value.QuadTree.Bounds[1].Value;
			// ref var collider3 = ref quadTreeBlobAssetRef.Value.QuadTree.Bounds[2].Value;
			// ref var collider4 = ref quadTreeBlobAssetRef.Value.PlayfieldCollider.Value;

			// Assert.AreEqual(ColliderType.Line, collider1.Type);
			// Assert.AreEqual(ColliderType.Circle, collider2.Type);
			// Assert.AreEqual(ColliderType.Line, collider3.Type);
			// Assert.AreEqual(ColliderType.Plane, collider4.Type);
			// fixed (Unity.Physics.Collider.Collider* collider = &collider4) {
			// 	Assert.AreEqual(new float3(0f, 0f, 1f), ((PlaneCollider*)collider)->Normal);
			// }
		}
	}
}
