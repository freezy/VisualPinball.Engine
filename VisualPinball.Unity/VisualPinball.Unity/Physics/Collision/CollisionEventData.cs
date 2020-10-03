// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	internal struct CollisionEventData : IComponentData
	{
		public float HitTime;
		public float3 HitNormal;
		public float2 HitVelocity;
		public float HitDistance;
		public bool HitFlag;
		public float HitOrgNormalVelocity;
		public bool IsContact;

		public int ColliderId;
		public Entity ColliderEntity;

		public void SetCollider(int colliderId)
		{
			ColliderId = colliderId;
			ColliderEntity = Entity.Null;
		}

		public void SetCollider(Entity colliderEntity)
		{
			ColliderId = -1;
			ColliderEntity = colliderEntity;
		}

		public void ClearCollider(float hitTime)
		{
			HitTime = hitTime;
			ColliderId = -1;
			ColliderEntity = Entity.Null;
		}


		public void ClearCollider()
		{
			ColliderId = -1;
			ColliderEntity = Entity.Null;
		}

		public bool HasCollider()
		{
			return ColliderId > -1 || ColliderEntity != Entity.Null;
		}
	}
}
