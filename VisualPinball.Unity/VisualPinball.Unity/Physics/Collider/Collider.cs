using NLog;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	/// <summary>
	/// Base struct common to all colliders.
	/// Dispatches the interface methods to appropriate implementations for the collider type.
	/// </summary>
	public struct Collider : ICollider, IComponentData
	{
		public ColliderHeader Header;

		public int Id => Header.Id;
		public Entity Entity => Header.Entity;
		public ColliderType Type => Header.Type;
		public PhysicsMaterialData Material => Header.Material;

		public static Collider None => new Collider {
			Header = {
				Type = ColliderType.None
			}
		};

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static void Create(BlobBuilder builder, HitObject src, ref BlobPtr<Collider> dest)
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

		public static unsafe float HitTest(ref Collider coll, ref CollisionEventData collEvent,
			ref DynamicBuffer<BallInsideOfBufferElement> insideOf, in BallData ball, float dTime)
		{
			fixed (Collider* collider = &coll) {
				switch (collider->Type) {
					case ColliderType.Circle:        return ((CircleCollider*)collider)->HitTest(ref collEvent, ref insideOf, in ball, dTime);
					case ColliderType.Line:          return ((LineCollider*)collider)->HitTest(ref collEvent, in ball, dTime);
					case ColliderType.LineZ:         return ((LineZCollider*)collider)->HitTest(ref collEvent, in ball, dTime);
					case ColliderType.Line3D:        return ((Line3DCollider*)collider)->HitTest(ref collEvent, in ball, dTime);
					case ColliderType.Point:         return ((PointCollider*)collider)->HitTest(ref collEvent, in ball, dTime);
					case ColliderType.Plane:         return ((PlaneCollider*)collider)->HitTest(ref collEvent, in ball, dTime);
					case ColliderType.Poly3D:        return ((Poly3DCollider*)collider)->HitTest(ref collEvent, in ball, dTime);
					default: return -1;
				}
			}
		}

		public static unsafe void Collide(ref Collider coll, ref BallData ballData, in CollisionEventData collEvent)
		{
			fixed (Collider* collider = &coll) {
				switch (collider->Type) {
					case ColliderType.Plane: ((PlaneCollider*)collider)->Collide(ref ballData, in collEvent); break;
					case ColliderType.Line: ((LineCollider*)collider)->Collide(ref ballData, in collEvent); break;
				}
			}
		}

		public void Contact(ref BallData ball, in CollisionEventData coll, double hitTime, in float3 gravity)
		{
			BallCollider.HandleStaticContact(ref ball, coll, Header.Material.Friction, (float)hitTime, gravity);
		}

		public static unsafe string ToString(ref Collider col)
		{
			fixed (Collider* collider = &col) {
				switch (collider->Type) {
					case ColliderType.Circle:        return ((CircleCollider*)collider)->ToString();
					case ColliderType.Flipper:       return ((FlipperCollider*)collider)->ToString();
					case ColliderType.Line:          return ((LineCollider*)collider)->ToString();
					case ColliderType.LineSlingShot: return ((LineSlingshotCollider*)collider)->ToString();
					case ColliderType.LineZ:         return ((LineZCollider*)collider)->ToString();
					case ColliderType.Line3D:        return ((Line3DCollider*)collider)->ToString();
					case ColliderType.Point:         return ((PointCollider*)collider)->ToString();
					case ColliderType.Plane:         return ((PlaneCollider*)collider)->ToString();
					case ColliderType.Poly3D:        return ((Poly3DCollider*)collider)->ToString();
					default: return "Collider";
				}
			}
		}

		public static ItemType GetItemType(string name)
		{
			switch (name) {
				case CollisionType.Null: return ItemType.Null;
				case CollisionType.Point: return ItemType.Point;
				case CollisionType.LineSeg: return ItemType.LineSeg;
				case CollisionType.LineSegSlingshot: return ItemType.LineSegSlingshot;
				case CollisionType.Joint: return ItemType.Joint;
				case CollisionType.Circle: return ItemType.Circle;
				case CollisionType.Flipper: return ItemType.Flipper;
				case CollisionType.Plunger: return ItemType.Plunger;
				case CollisionType.Spinner: return ItemType.Spinner;
				case CollisionType.Ball: return ItemType.Ball;
				case CollisionType.Poly: return ItemType.Poly;
				case CollisionType.Triangle: return ItemType.Triangle;
				case CollisionType.Plane: return ItemType.Plane;
				case CollisionType.Line: return ItemType.Line;
				case CollisionType.Gate: return ItemType.Gate;
				case CollisionType.TextBox: return ItemType.TextBox;
				case CollisionType.DispReel: return ItemType.DispReel;
				case CollisionType.LightSeq: return ItemType.LightSeq;
				case CollisionType.Primitive: return ItemType.Primitive;
				case CollisionType.HitTarget: return ItemType.HitTarget;
				case CollisionType.Trigger: return ItemType.Trigger;
				case CollisionType.Kicker: return ItemType.Kicker;
				default: return ItemType.Null;
			}
		}
	}
}
