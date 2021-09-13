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
	[RequireComponent(typeof(DropTargetColliderComponent))]
	public class DropTargetAnimationComponent : AnimationComponent<HitTargetData, DropTargetComponent>
	{
		public override IEnumerable<Type> ValidParents => Type.EmptyTypes;

		#region Data

		[Tooltip("How fast the drop target moves down.")]
		public float Speed =  0.5f;

		[Tooltip("Time in milliseconds how long it takes to start the raise animation after being triggered.")]
		public int RaiseDelay = 100;

		[Tooltip("If set, the drop target is initially dropped.")]
		public bool IsDropped;

		#endregion
	}
}
