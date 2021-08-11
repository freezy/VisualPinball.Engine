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
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.Table;
using Light = VisualPinball.Engine.VPT.Light.Light;

namespace VisualPinball.Unity
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Mesh/Light Socket Mesh")]
	public class LightSocketMeshAuthoring : ItemMeshAuthoring<Light, LightData, LightAuthoring>
	{
		public static readonly Type[] ValidParentTypes = new Type[0];

		public override IEnumerable<Type> ValidParents => ValidParentTypes;

		protected override RenderObject GetRenderObject(LightData data, Table table)
			=> new LightMeshGenerator(data).GetRenderObject(table, LightMeshGenerator.Socket, Origin.Original,  false);

		protected override string MeshId => LightMeshGenerator.Socket;
		protected override bool IsVisible {
			get => Data.ShowBulbMesh;
			set => Data.ShowBulbMesh = value;
		}
	}
}
