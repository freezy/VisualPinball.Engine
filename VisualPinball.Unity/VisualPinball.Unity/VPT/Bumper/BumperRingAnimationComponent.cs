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
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Animation/Bumper Ring Animation")]
	public class BumperRingAnimationComponent : ItemAnimationComponent<BumperData, BumperComponent>
	{
		public override IEnumerable<Type> ValidParents => Type.EmptyTypes; // animation components only apply to their own

		#region Data

		[Tooltip("How quick the ring moves down when the ball is hit.")]
		public float RingSpeed = 0.5f;

		[Tooltip("How low the ring drops. 0 = bottom")]
		public float RingDropOffset;

		#endregion
	}
}
