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

using System;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Spinner
{
	public class SpinnerMeshGenerator : MeshGenerator
	{
		private readonly SpinnerData _data;

		protected override Vertex3D Position => new Vertex3D(_data.Center.X, _data.Center.Y, _data.Height);
		protected override Vertex3D Scale => new Vertex3D(_data.Length, _data.Length, _data.Length);
		protected override float RotationZ => MathF.DegToRad(_data.Rotation);

		public SpinnerMeshGenerator(SpinnerData data)
		{
			_data = data;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded)
		{
			var (preMatrix, _) = GetPreMatrix(table, origin, asRightHanded);
			var postMatrix = GetPostMatrix(table, origin);
			return new RenderObjectGroup(_data.Name, "Spinners", postMatrix, new RenderObject(
					"Plate",
					SpinnerPlateMesh.Clone().Transform(preMatrix),
					new PbrMaterial(table.GetMaterial(_data.Material), table.GetTexture(_data.Image)),
					_data.IsVisible
				),
				new RenderObject(
					"Bracket",
					SpinnerBracketMesh.Clone().Transform(preMatrix),
					new PbrMaterial(GetBracketMaterial(), table.GetTexture(_data.Image)),
					_data.IsVisible && _data.ShowBracket
				)
			);
		}

		private static Material GetBracketMaterial()
		{
			return new Material(Spinner.BracketMaterialName) {
				BaseColor = new Color(0x20202020, ColorFormat.Bgr),
				WrapLighting = 0.9f,
				IsOpacityActive = false,
				Opacity = 1.0f,
				Glossiness = new Color(0x60606060, ColorFormat.Bgr),
				IsMetal = false,
				Edge = 1.0f,
				EdgeAlpha = 1.0f,
				Roughness = 0.4f,
				GlossyImageLerp = 1.0f,
				Thickness = 0.05f,
				ClearCoat = new Color(0x20202020, ColorFormat.Bgr),
			};
		}

		protected override float BaseHeight(Table.Table table)
		{
			return table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
		}

		#region Mesh Imports

		private static readonly Mesh SpinnerPlateMesh = new Mesh("Plate", SpinnerPlate.Vertices, SpinnerPlate.Indices);
		private static readonly Mesh SpinnerBracketMesh = new Mesh("Bracket", SpinnerBracket.Vertices, SpinnerBracket.Indices);

		#endregion
	}
}
