using Unity.Entities;
using VisualPinball.Engine.Physics;

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

		public static void CreatePtr(HitObject hitObject, ref BlobPtr<Collider> ptr, BlobBuilder builder)
		{
			if (hitObject is LineSeg lineSeg) {
				LineCollider.CreatePtr(lineSeg, ref ptr, builder);
			}
		}

		public unsafe int MemorySize
		{
			get {
				fixed (Collider* collider = &this) {
					switch (collider->Type) {
						case ColliderType.Line:
							return ((LineCollider*)collider)->MemorySize;
						default:
							return 0;
					}
				}
			}
		}

		public unsafe float HitTest(float dTime)
		{
			fixed (Collider* collider = &this) {
				switch (collider->Type) {
					case ColliderType.Line:
						return ((LineCollider*)collider)->HitTest(dTime);
				}
			}

			return -1;
		}
	}
}
