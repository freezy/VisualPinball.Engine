using NLog;
using Unity.Entities;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	/// <summary>
	/// Base struct common to all colliders.
	/// Dispatches the interface methods to appropriate implementations for the collider type.
	/// </summary>
	public struct Collider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		public ColliderType Type => _header.Type;
		public Aabb Aabb => _header.Aabb;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static void Create(HitObject src, ref BlobPtr<Collider> dest, BlobBuilder builder)
		{
			switch (src) {
				case HitCircle hitCircle:
					CircleCollider.Create(builder, hitCircle, ref dest);
					break;
				case LineSegSlingshot lineSegSlingshot:
					LineSlingshotCollider.Create(builder, lineSegSlingshot, ref dest);
					break;
				case FlipperHit flipperHit:
					FlipperCollider.Create(builder, flipperHit, ref dest);
					break;
				case LineSeg lineSeg:
					LineCollider.Create(builder, lineSeg, ref dest);
					break;
				case HitLine3D hitLine3D:
					Line3DCollider.Create(builder, hitLine3D, ref dest);
					break;
				case HitLineZ hitLineZ:
					LineZCollider.Create(builder, hitLineZ, ref dest);
					break;
				case HitPoint hitPoint:
					PointCollider.Create(builder, hitPoint, ref dest);
					break;
				case Hit3DPoly hit3DPoly:
					Poly3DCollider.Create(builder, hit3DPoly, ref dest);
					break;
				case HitPlane hitPlane:
					PlaneCollider.Create(builder, hitPlane, ref dest);
					break;
				default:
					Logger.Warn("Unknown collider {0}, skipping.", src.GetType().Name);
					break;
			}
		}

		public unsafe float HitTest(BallData ball, float dTime, CollisionEventData coll)
		{
			fixed (Collider* collider = &this) {
				switch (collider->Type) {
					case ColliderType.Circle:        return ((CircleCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.Flipper:       return ((FlipperCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.Line:          return ((LineCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.LineSlingShot: return ((LineSlingshotCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.LineZ:         return ((LineZCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.Line3D:        return ((Line3DCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.Point:         return ((PointCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.Plane:         return ((PlaneCollider*)collider)->HitTest(ball, dTime, coll);
					case ColliderType.Poly3D:        return ((Poly3DCollider*)collider)->HitTest(ball, dTime, coll);
					default: return -1;
				}
			}
		}
	}
}
