using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity
{
	public struct ColliderBlob : IComponentData
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
