using Unity.Entities;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	/// <summary>
	/// Base struct common to all colliders.
	/// Dispatches the interface methods to appropriate implementations for the collider type.
	/// </summary>
	public struct Collider : ICollider, ICollidable, IComponentData
	{
		private ColliderHeader _header;
		public ColliderType Type => _header.Type;
		public Aabb Aabb => _header.HitBBox;

		public static void Create(HitObject src, ref BlobPtr<Collider> dest, BlobBuilder builder)
		{
			switch (src) {
				case HitCircle hitCircle:
					CircleCollider.Create(hitCircle, ref dest, builder);
					break;
				case LineSegSlingshot lineSegSlingshot:
					LineSlingshotCollider.Create(lineSegSlingshot, ref dest, builder);
					break;
				case LineSeg lineSeg:
					LineCollider.Create(lineSeg, ref dest, builder);
					break;
				case HitLine3D hitLine3D:
					Line3DCollider.Create(hitLine3D, ref dest, builder);
					break;
				case HitLineZ hitLineZ:
					LineZCollider.Create(hitLineZ, ref dest, builder);
					break;
				case HitPoint hitPoint:
					PointCollider.Create(hitPoint, ref dest, builder);
					break;
				case HitPlane hitPlane:
					PlaneCollider.Create(hitPlane, ref dest, builder);
					break;
			}
		}

		public unsafe float HitTest(BallData ball, float dTime, CollisionEvent coll)
		{
			fixed (Collider* collider = &this) {
				switch (collider->Type) {
					case ColliderType.Circle:        return ((CircleCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.Line:          return ((LineCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.LineSlingShot: return ((LineSlingshotCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.LineZ:         return ((LineZCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.Line3D:        return ((Line3DCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.Point:         return ((PointCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.Plane:         return ((PlaneCollider*)collider)->HitTest(ball, dTime, coll);
					default: return -1;
				}
			}
		}
	}
}
