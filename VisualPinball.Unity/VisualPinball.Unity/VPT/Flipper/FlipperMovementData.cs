using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public struct FlipperMovementData : IComponentData
	{
		public float Angle;
		public float AngleSpeed;
		public float AngularMomentum;
		public sbyte EnableRotateEvent;
		public quaternion BaseRotation;
		public uint LastHitTime;

		public override string ToString()
		{
			return $"FlipperMovementData(Angle: {Angle}, AngleSpeed: {AngleSpeed}, AngularMomentum: {AngularMomentum}, BaseRotation: {BaseRotation})";
		}

		public void ApplyImpulse(in float3 rotI, float inertia)
		{
			AngularMomentum += rotI.z;                    // only rotation about z axis
			AngleSpeed = AngularMomentum / inertia;       // figure TODO out moment of inertia
		}

		public static float3 SurfaceAcceleration(in FlipperMovementData data, in FlipperVelocityData velData, in float3 surfP)
		{
			// tangential acceleration = (0, 0, omega) x surfP
			var tangAcc = Math.CrossZ(velData.AngularAcceleration, surfP);

			// centripetal acceleration = (0,0,omega) x ( (0,0,omega) x surfP )
			var av2 = data.AngleSpeed * data.AngleSpeed;
			var centrAcc = new float3(-av2 * surfP.x, -av2 * surfP.y, 0);

			return tangAcc + centrAcc;
		}

		public static float3 SurfaceVelocity(in FlipperMovementData data, in float3 surfP)
		{
			return Math.CrossZ(data.AngleSpeed, in surfP);
		}

		public float GetHitTime(float angleStart, float angleEnd)
		{
			if (AngleSpeed == 0f) {
				return -1.0f;
			}

			var angleMin = math.min(angleStart, angleEnd);
			var angleMax = math.max(angleStart, angleEnd);

			var dist = AngleSpeed > 0
				? angleMax - Angle       // >= 0
				: angleMin - Angle;      // <= 0

			var hitTime = dist / AngleSpeed;

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0) {
				return -1.0f;
			}
			return hitTime;
		}
	}
}
