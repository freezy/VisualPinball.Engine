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
	[AddComponentMenu("Visual Pinball/Mesh/Plunger Spring Mesh")]
	public class PlungerSpringMeshComponent : PlungerMeshComponent
	{

		#region Data

		public float SpringDiam = 0.77f;

		public float SpringGauge = 1.38f;

		public float SpringLoops = 8.0f;

		public float SpringEndLoops = 2.5f;

		#endregion

		public static readonly Type[] ValidParentTypes = Type.EmptyTypes;

		public override IEnumerable<Type> ValidParents => ValidParentTypes;

		protected override RenderObject GetRenderObject(PlungerData data, Table table)
			=> new PlungerMeshGenerator(data).GetRenderObject(table, PlungerMeshGenerator.Spring, false);
		protected override Mesh GetMesh(PlungerData data)
			=> new PlungerMeshGenerator(data).GetMesh(MainComponent.PositionZ, PlungerMeshGenerator.Spring);
	}
}
