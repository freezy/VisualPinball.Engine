// Visual Pinball Engine
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

using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity
{
	internal struct QuadTreeBlob
	{
		public QuadTree QuadTree;
		public BlobPtr<Collider> PlayfieldCollider;
		public BlobPtr<Collider> GlassCollider;

		public static BlobAssetReference<QuadTreeBlob> CreateBlobAssetReference(Engine.Physics.QuadTree quadTree, HitPlane playfield, HitPlane glass)
		{
			using (var builder = new BlobBuilder(Allocator.Temp)) {
				ref var rootQuadTree = ref builder.ConstructRoot<QuadTreeBlob>();
				QuadTree.Create(quadTree, ref rootQuadTree.QuadTree, builder);

				if (playfield != null) {
					PlaneCollider.Create(builder, playfield, ref rootQuadTree.PlayfieldCollider);

				} else {
					ref var playfieldCollider = ref builder.Allocate(ref rootQuadTree.PlayfieldCollider);
					playfieldCollider.Header = new ColliderHeader {
						Type = ColliderType.None
					};
				}
				PlaneCollider.Create(builder, glass, ref rootQuadTree.GlassCollider);

				return builder.CreateBlobAssetReference<QuadTreeBlob>(Allocator.Persistent);
			}
		}
	}
}
