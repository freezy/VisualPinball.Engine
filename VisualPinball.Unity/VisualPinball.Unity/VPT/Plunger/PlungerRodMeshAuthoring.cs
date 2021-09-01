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

using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Table;
using Mesh = VisualPinball.Engine.VPT.Mesh;

namespace VisualPinball.Unity
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Plunger Rod Mesh")]
	public class PlungerRodMeshAuthoring : PlungerMeshAuthoring
	{
		#region Data

		public float RodDiam = 0.6f;

		public float RingGap = 2.0f;

		public float RingDiam = 0.94f;

		public float RingWidth = 3.0f;

		public string TipShape = "0 .34; 2 .6; 3 .64; 5 .7; 7 .84; 8 .88; 9 .9; 11 .92; 14 .92; 39 .84";

		#endregion

		public static readonly Type[] ValidParentTypes = Type.EmptyTypes;

		public override IEnumerable<Type> ValidParents => ValidParentTypes;

		protected override RenderObject GetRenderObject(PlungerData data, Table table)
			=> new PlungerMeshGenerator(data).GetRenderObject(table, PlungerMeshGenerator.Rod, false);
		protected override Mesh GetMesh(PlungerData data)
			=> new PlungerMeshGenerator(data).GetMesh(MainComponent.PositionZ, PlungerMeshGenerator.Rod);
	}
}
