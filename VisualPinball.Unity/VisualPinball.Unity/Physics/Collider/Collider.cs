using NLog;
using Unity.Entities;
using Unity.Mathematics;
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
	public struct Collider : ICollider, ICollidable, IComponentData
	{
		public ColliderHeader Header;

		public ColliderType Type => Header.Type;
		public Aabb Aabb => Header.Aabb;
		public PhysicsMaterialData Material => Header.Material;

		public static Collider None => new Collider {
			Header = {
				Type = ColliderType.None,
				Aabb = default,
				EntityIndex = -1
			}
		};

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

		public unsafe float HitTest(in BallData ball, float dTime, CollisionEventData coll)
		{
			fixed (Collider* collider = &this) {
				switch (collider->Type) {
					case ColliderType.Circle:        return ((CircleCollider*)collider)->HitTest(in ball, dTime, coll);
					case ColliderType.Flipper:       return ((FlipperCollider*)collider)->HitTest(in ball, dTime, coll);
					case ColliderType.Line:          return ((LineCollider*)collider)->HitTest(in ball, dTime, coll);
					case ColliderType.LineSlingShot: return ((LineSlingshotCollider*)collider)->HitTest(in ball, dTime, coll);
					case ColliderType.LineZ:         return ((LineZCollider*)collider)->HitTest(in ball, dTime, coll);
					case ColliderType.Line3D:        return ((Line3DCollider*)collider)->HitTest(in ball, dTime, coll);
					case ColliderType.Point:         return ((PointCollider*)collider)->HitTest(in ball, dTime, coll);
					case ColliderType.Plane:         return ((PlaneCollider*)collider)->HitTest(in ball, dTime, coll);
					case ColliderType.Poly3D:        return ((Poly3DCollider*)collider)->HitTest(in ball, dTime, coll);
					default: return -1;
				}
			}
		}

		public unsafe void Collide(ref BallData ballData, CollisionEventData coll)
		{
			fixed (Collider* collider = &this) {
				switch (collider->Type) {
					case ColliderType.Plane: ((PlaneCollider*)collider)->Collide(ref ballData, coll); break;
				}
			}
		}

		public void Contact(ref BallData ball, in CollisionEventData coll, double hitTime, in float3 gravity)
		{
			BallCollider.HandleStaticContact(ref ball, coll, Header.Material.Friction, (float)hitTime, gravity);
		}
	}
}
