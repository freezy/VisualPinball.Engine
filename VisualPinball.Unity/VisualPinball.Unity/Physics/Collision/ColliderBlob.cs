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

using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity
{
	internal struct ColliderBlob : IComponentData
	{
		public BlobArray<BlobPtr<Collider>> Colliders;
		public int PlayfieldColliderId;
		public int GlassColliderId;

		public static BlobAssetReference<ColliderBlob> CreateBlobAssetReference(List<HitObject> hitObjects, int playfieldColliderId, int glassColliderId)
		{
			using (var builder = new BlobBuilder(Allocator.Temp)) {
				ref var root = ref builder.ConstructRoot<ColliderBlob>();
				var colliders = builder.Allocate(ref root.Colliders, hitObjects.Count);

				foreach (var hitObject in hitObjects) {
					Collider.Create(builder, hitObject, ref colliders[hitObject.Id]);
				}

				root.PlayfieldColliderId = playfieldColliderId;
				root.GlassColliderId = glassColliderId;

				return builder.CreateBlobAssetReference<ColliderBlob>(Allocator.Persistent);
			}
		}
	}
}
