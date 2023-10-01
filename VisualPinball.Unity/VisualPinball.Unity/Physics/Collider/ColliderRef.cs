using Unity.Collections;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public readonly ref struct ColliderRef
	{
		internal readonly int Id;
		private readonly BlobAssetReference<ColliderBlob> _colliders;

		internal ColliderRef(int id, ref BlobAssetReference<ColliderBlob> colliders)
		{
			_colliders = colliders;
			Id = id;
		}

		internal float HitTest(ref BallData ball, ref CollisionEventData collEvent, ref InsideOfs insideOfs,
			ref NativeList<ContactBufferElement> contacts)
		{
			var hitTime = -1f;
			switch (_colliders.GetType(Id)) {
				case ColliderType.Plane:
					hitTime = _colliders.GetPlaneCollider(Id).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);
					break;
				case ColliderType.Line:
					hitTime = _colliders.GetLineCollider(Id).HitTest(ref collEvent, ref insideOfs, ref ball, ball.CollisionEvent.HitTime);
					break;
				case ColliderType.Triangle:
					hitTime = _colliders.GetTriangleCollider(Id).HitTest(ref collEvent, in insideOfs, in ball, ball.CollisionEvent.HitTime);
					break;
				case ColliderType.Line3D:
					hitTime = _colliders.GetLine3DCollider(Id).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);
					break;
				case ColliderType.Point:
					hitTime = _colliders.GetPointCollider(Id).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);
					break;
			}
			return hitTime;
		}
	}
}
