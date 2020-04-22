using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Unity.Common;

namespace VisualPinball.Unity.VPT.Flipper
{
	public struct FlipperMovementData : IComponentData
	{
		public float Angle;
		public float AngleSpeed;
		public float AngularMomentum;
		public sbyte EnableRotateEvent;
		public quaternion BaseRotation;

		public override string ToString()
		{
			return $"FlipperMovementData(Angle: {Angle}, AngleSpeed: {AngleSpeed}, AngularMomentum: {AngularMomentum}, BaseRotation: {BaseRotation})";
		}

		public float3 SurfaceAcceleration(in float3 surfP, float angularAcceleration)
		{
			// tangential acceleration = (0, 0, omega) x surfP
			var tangAcc = Math.CrossZ(angularAcceleration, surfP);

			// centripetal acceleration = (0,0,omega) x ( (0,0,omega) x surfP )
			var av2 = AngleSpeed * AngleSpeed;
			var centrAcc = new float3(-av2 * surfP.x, -av2 * surfP.y, 0);

			return tangAcc + centrAcc;
		}

		public void ApplyImpulse(in float3 rotI, float inertia)
		{
			AngularMomentum += rotI.z;                    // only rotation about z axis
			AngleSpeed = AngularMomentum / inertia;       // figure TODO out moment of inertia
		}

		public float3 SurfaceVelocity(in float3 surfP)
		{
			return Math.CrossZ(AngleSpeed, surfP);
		}
	}
}
