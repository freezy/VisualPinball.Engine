// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Animation/Drop Target Animation")]
	[RequireComponent(typeof(HitTargetColliderAuthoring))]
	public class DropTargetAnimationAuthoring : ItemAnimationAuthoring<HitTarget, HitTargetData, HitTargetAuthoring>
	{
		public override IEnumerable<Type> ValidParents { get; } = new Type[0];

		#region Data

		public float Speed =  0.5f;

		public int RaiseDelay = 100;

		public bool IsDropped;

		#endregion
	}
}
