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
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Animation/Trigger Animation")]
	public class TriggerAnimationAuthoring : ItemAnimationAuthoring<Trigger, TriggerData, TriggerAuthoring>
	{
		public override IEnumerable<Type> ValidParents { get; } = Type.EmptyTypes; // animation components only apply to their own

		#region Data

		[Min(0)]
		[Tooltip("How quick the trigger moves down when the ball rolls over it.")]
		public float AnimSpeed = 1f;

		#endregion
	}
}
