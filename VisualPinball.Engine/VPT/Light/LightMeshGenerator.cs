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

namespace VisualPinball.Engine.VPT.Light
{
	public class LightMeshGenerator
	{
		public const string Bulb = "Bulb";
		public const string Socket = "Socket";

		private static readonly Mesh BulbMesh = new Mesh("Bulb", Resources.Meshes.Bulb.Vertices, Resources.Meshes.Bulb.Indices);
		private static readonly Mesh SocketMesh = new Mesh("Socket", BulbSocket.Vertices, BulbSocket.Indices);

		private readonly LightData _data;

		public LightMeshGenerator(LightData data)
		{
			_data = data;
		}

		public Mesh GetMesh(string id, Table.Table table, Origin origin, bool asRightHanded)
		{
			switch (id) {
				case Bulb:
					var bulbMesh = GetBulbMesh(table, origin);
					return asRightHanded ? bulbMesh.Transform(Matrix3D.RightHanded) : bulbMesh;
				case Socket:
					var socketMesh = GetSocketMesh(table, origin);
					return asRightHanded ? socketMesh.Transform(Matrix3D.RightHanded) : socketMesh;
			}
			throw new ArgumentException("Unknown light mesh \"" + id + "\".");
		}

		public PbrMaterial GetMaterial(string id, Table.Table table)
		{
			switch (id) {
				case Bulb:
					return new PbrMaterial(GetBulbMaterial());
				case Socket:
					return new PbrMaterial(GetSocketMaterial());
			}
			throw new ArgumentException("Unknown light mesh \"" + id + "\".");
		}

		public Matrix3D GetPostMatrix(Table.Table table, Origin origin)
		{
			switch (origin) {
				case Origin.Original:
					return new Matrix3D().SetTranslation(
						_data.Center.X,
						_data.Center.Y,
						table?.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y) ?? 0f
					);

				case Origin.Global:
					return Matrix3D.Identity;

				default:
					throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown origin " + origin);
			}
		}

		private static Material GetBulbMaterial()
		{
			return new Material(Light.BulbMaterialName) {
				BaseColor = new Color(0x000000ff, ColorFormat.Bgr),
				WrapLighting = 0.5f,
				IsOpacityActive = true,
				Opacity = 0.2f,
				Glossiness = new Color(0xFFFFFFFF, ColorFormat.Bgr),
				IsMetal = false,
				Edge = 1.0f,
				EdgeAlpha = 1.0f,
				Roughness = 0.9f,
				GlossyImageLerp = 1.0f,
				Thickness = 0.05f,
				ClearCoat = new Color(0xFFFFFFFF, ColorFormat.Bgr),
			};
		}

		private static Material GetSocketMaterial()
		{
			return new Material(Light.SocketMaterialName) {
				BaseColor = new Color(0x181818, ColorFormat.Bgr),
				WrapLighting = 0.5f,
				IsOpacityActive = false,
				Opacity = 1.0f,
				Glossiness = new Color(0xB4B4B4, ColorFormat.Bgr),
				IsMetal = false,
				Edge = 1.0f,
				EdgeAlpha = 1.0f,
				Roughness = 0.9f,
				GlossyImageLerp = 1.0f,
				Thickness = 0.05f,
				ClearCoat = new Color(0x000000, ColorFormat.Bgr),
			};
		}

		private Mesh GetBulbMesh(Table.Table table, Origin origin)
		{
			var bulbMesh = BulbMesh.Clone();
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			var transX = origin == Origin.Global ? _data.Center.X : 0f;
			var transY = origin == Origin.Global ? _data.Center.Y : 0f;
			var transZ = origin == Origin.Global ? height : 0f;
			for (var i = 0; i < bulbMesh.Vertices.Length; i++) {
				bulbMesh.Vertices[i].X = bulbMesh.Vertices[i].X * _data.MeshRadius + transX;
				bulbMesh.Vertices[i].Y = bulbMesh.Vertices[i].Y * _data.MeshRadius + transY;
				bulbMesh.Vertices[i].Z = bulbMesh.Vertices[i].Z * _data.MeshRadius + transZ;
			}

			return bulbMesh;
		}

		private Mesh GetSocketMesh(Table.Table table, Origin origin)
		{
			var socketMesh = SocketMesh.Clone();
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			var transX = origin == Origin.Global ? _data.Center.X : 0f;
			var transY = origin == Origin.Global ? _data.Center.Y : 0f;
			var transZ = origin == Origin.Global ? height : 0f;

			for (var i = 0; i < socketMesh.Vertices.Length; i++) {
				socketMesh.Vertices[i].X = socketMesh.Vertices[i].X * _data.MeshRadius + transX;
				socketMesh.Vertices[i].Y = socketMesh.Vertices[i].Y * _data.MeshRadius + transY;
				socketMesh.Vertices[i].Z = socketMesh.Vertices[i].Z * _data.MeshRadius + transZ;
			}

			return socketMesh;
		}

		private bool RenderBulb()
		{
			return _data.IsBulbLight && _data.ShowBulbMesh;
		}
	}
}
