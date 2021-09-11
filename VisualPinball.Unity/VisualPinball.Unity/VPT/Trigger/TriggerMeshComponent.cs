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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Trigger Mesh")]
	public class TriggerMeshComponent : ItemMeshComponent<TriggerData, TriggerComponent>
	{
		#region Data

		public int Shape;

		[Min(0)]
		[Tooltip("Thickness of the trigger wire. Doesn't have any impact on the ball.")]
		public float WireThickness;

		#endregion

		public bool IsCircle => Shape == TriggerShape.TriggerStar || Shape == TriggerShape.TriggerButton;

		public static readonly Type[] ValidParentTypes = Type.EmptyTypes;

		public override IEnumerable<Type> ValidParents => ValidParentTypes;

		protected override RenderObject GetRenderObject(TriggerData data, Table table)
			=> new TriggerMeshGenerator(data).GetRenderObject(table, Origin.Original, false);

		protected override Mesh GetMesh(TriggerData data)
			=> new TriggerMeshGenerator(data).GetMesh(MainComponent.PlayfieldHeight);
	}
}
