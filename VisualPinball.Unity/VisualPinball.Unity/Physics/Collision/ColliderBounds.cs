﻿// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

using System;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct ColliderBounds
	{
		public Entity ColliderEntity;
		public int ColliderId;
		public Aabb Aabb;

		public ColliderBounds(Entity colliderEntity, int colliderId, Aabb aabb)
		{
			if (colliderEntity == Entity.Null) {
				throw new ArgumentException("Entity must not be null.");
			}

			ColliderEntity = colliderEntity;
			ColliderId = colliderId;
			Aabb = aabb;
		}

		public override string ToString()
		{
			return $"{Aabb.ToString()} ({ColliderId}:{ColliderEntity})";
		}
	}
}
