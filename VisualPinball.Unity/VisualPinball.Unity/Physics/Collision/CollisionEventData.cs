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
	internal struct CollisionEventData
	{
		public float HitTime;
		public float3 HitNormal;
		public float2 HitVelocity;
		public float HitDistance;
		public bool HitFlag;
		public float HitOrgNormalVelocity;
		public bool IsContact;

		public int ColliderId;
		public bool IsKinematic;
		public int BallId;
		
		public void SetCollider(int colliderId, bool isKinematic)
		{
			// it's either collider id (for static colliders) or ball id (for dynamic colliders)
			ColliderId = colliderId;
			IsKinematic = isKinematic;
			BallId = 0;
		}

		public void SetBallItem(int ballId)
		{
			// it's either collider id (for static colliders) or ball id (for dynamic colliders)
			ColliderId = -1;
			BallId = ballId;
		}

		public void ClearCollider(float hitTime)
		{
			HitTime = hitTime;
			ClearCollider();
		}


		public void ClearCollider()
		{
			ColliderId = -1;
			BallId = 0;
		}

		public bool HasCollider()
		{
			return ColliderId > -1 || BallId != 0;
		}

		public void Transform(float4x4 matrix)
		{
			HitNormal = matrix.MultiplyVector(HitNormal);
			HitVelocity = matrix.MultiplyVector(new float3(HitVelocity, 0)).xy;
		}
	}
}
