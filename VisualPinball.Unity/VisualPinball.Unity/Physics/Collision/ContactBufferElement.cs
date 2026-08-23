// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal struct ContactBufferElement
	{
		public CollisionEventData CollEvent;
		public int BallId;
		public RollingContactData RollingContact;

		public ContactBufferElement(int ballId, CollisionEventData collEvent)
		{
			BallId = ballId;
			CollEvent = collEvent;
			RollingContact = default;
		}
	}

	internal struct RollingContactData
	{
		public bool IsContact;
		public float RollingResistance;
		public float SupportImpulse;
		public float3 ContactNormal;
		public float3 ColliderVelocity;

		public readonly bool IsValid => IsContact
			&& RollingResistance > 0f
			&& math.isfinite(RollingResistance)
			&& SupportImpulse > 0f
			&& math.isfinite(SupportImpulse)
			&& math.all(math.isfinite(ContactNormal))
			&& math.isfinite(math.lengthsq(ContactNormal))
			&& math.lengthsq(ContactNormal) > 0f
			&& math.all(math.isfinite(ColliderVelocity))
			&& math.isfinite(RollingImpulseLimit);

		public readonly float RollingImpulseLimit => RollingResistance * SupportImpulse;

		public readonly bool IsPreferredOver(in RollingContactData other)
		{
			if (RollingImpulseLimit != other.RollingImpulseLimit) {
				return RollingImpulseLimit > other.RollingImpulseLimit;
			}
			if (RollingResistance != other.RollingResistance) {
				return RollingResistance > other.RollingResistance;
			}
			if (ContactNormal.x != other.ContactNormal.x) {
				return ContactNormal.x > other.ContactNormal.x;
			}
			if (ContactNormal.y != other.ContactNormal.y) {
				return ContactNormal.y > other.ContactNormal.y;
			}
			if (ContactNormal.z != other.ContactNormal.z) {
				return ContactNormal.z > other.ContactNormal.z;
			}
			if (ColliderVelocity.x != other.ColliderVelocity.x) {
				return ColliderVelocity.x > other.ColliderVelocity.x;
			}
			if (ColliderVelocity.y != other.ColliderVelocity.y) {
				return ColliderVelocity.y > other.ColliderVelocity.y;
			}
			return ColliderVelocity.z > other.ColliderVelocity.z;
		}
	}
}
