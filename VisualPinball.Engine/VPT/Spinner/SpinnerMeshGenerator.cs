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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;
using MathF = VisualPinball.Engine.Math.MathF;

namespace VisualPinball.Engine.VPT.Spinner
{
	public class SpinnerMeshGenerator : MeshGenerator
	{
		public const string Plate = "Plate";
		public const string Bracket = "Bracket";

		private readonly SpinnerData _data;

		protected override Vertex3D Position => new Vertex3D(_data.Center.X, _data.Center.Y, _data.Height);
		protected override Vertex3D Scale => new Vertex3D(_data.Length, _data.Length, _data.Length);
		protected override float RotationZ => MathF.DegToRad(_data.Rotation);

		public SpinnerMeshGenerator(SpinnerData data)
		{
			_data = data;
		}

		public Mesh GetMesh(string id, Table.Table table, Origin origin, bool asRightHanded)
		{
			var (preMatrix, _) = GetPreMatrix(BaseHeight(table), origin, asRightHanded);
			switch (id) {
				case Plate:
					return SpinnerPlateMesh.Clone().Transform(preMatrix);

				case Bracket:
					return SpinnerBracketMesh.Clone().Transform(preMatrix);
			}
			throw new ArgumentException($"Invalid spinner mesh ID \"{id}\"");
		}

		public PbrMaterial GetMaterial(string id, Table.Table table)
		{
			switch (id) {
				case Plate:
					return new PbrMaterial(table.GetMaterial(_data.Material), table.GetTexture(_data.Image));

				case Bracket:
					return new PbrMaterial(GetBracketMaterial(), table.GetTexture(_data.Image));
			}
			throw new ArgumentException($"Invalid spinner mesh ID \"{id}\"");
		}

		private static Material GetBracketMaterial()
		{
			return new Material(Spinner.BracketMaterialName) {
				BaseColor = new Color(0x202020ff, ColorFormat.Bgr),
				WrapLighting = 0.9f,
				IsOpacityActive = false,
				Opacity = 1.0f,
				Glossiness = new Color(0x606060ff, ColorFormat.Bgr),
				IsMetal = false,
				Edge = 1.0f,
				EdgeAlpha = 1.0f,
				Roughness = 0.4f,
				GlossyImageLerp = 1.0f,
				Thickness = 0.05f,
				ClearCoat = new Color(0x202020ff, ColorFormat.Bgr),
			};
		}

		protected override float BaseHeight(Table.Table table)
		{
			return table?.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y) ?? 0f;
		}

		#region Mesh Imports

		private static readonly Mesh SpinnerPlateMesh = new Mesh("Plate", SpinnerPlate.Vertices, SpinnerPlate.Indices);
		private static readonly Mesh SpinnerBracketMesh = new Mesh("Bracket", SpinnerBracket.Vertices, SpinnerBracket.Indices);

		#endregion
	}
}
