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
using System.Collections.Generic;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Light
{
	public class LightMeshGenerator
	{
		public const string Bulb = "Bulb";
		public const string Socket = "Socket";

		private static readonly Mesh BulbMesh = new Mesh("Bulb", Resources.Meshes.BulbMesh.Vertices, Resources.Meshes.BulbMesh.Indices);
		private static readonly Mesh SocketMesh = new Mesh("Socket", BulbSocket.Vertices, BulbSocket.Indices);

		private readonly LightData _data;

		internal LightMeshGenerator(LightData data)
		{
			_data = data;
		}

		public RenderObject GetRenderObject(Table.Table table, string id, Origin origin, bool asRightHanded)
		{
			switch (id)
			{
				case Bulb:
					var bulbMesh = GetBulbMesh(table, origin);
					return new RenderObject(
						id,
						asRightHanded ? bulbMesh.Transform(Matrix3D.RightHanded) : bulbMesh,
						new PbrMaterial(GetBulbMaterial()),
						true
					);
				case Socket:
					var socketMesh = GetSocketMesh(table, origin);
					return new RenderObject(
						id,
						asRightHanded ? socketMesh.Transform(Matrix3D.RightHanded) : socketMesh,
						new PbrMaterial(GetSocketMaterial()),
						true
					);
				default:
					throw new ArgumentException("Unknown light mesh \"" + id + "\".");
			}
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded = true)
		{
			var translationMatrix = GetPostMatrix(table, origin);
			if (!RenderBulb()) {
				return new RenderObjectGroup(_data.Name, "Lights", translationMatrix);
			}

			var bulbMesh = GetBulbMesh(table, origin);
			var socketMesh = GetSocketMesh(table, origin);
			return new RenderObjectGroup(_data.Name, "Lights", translationMatrix,
				new RenderObject(
					Bulb,
					asRightHanded ? bulbMesh.Transform(Matrix3D.RightHanded) : bulbMesh,
					new PbrMaterial(GetBulbMaterial()),
					true
				),
				new RenderObject(
					Socket,
					asRightHanded ? socketMesh.Transform(Matrix3D.RightHanded) : socketMesh,
					new PbrMaterial(GetSocketMaterial()),
					true
				)
			);
		}

		public Matrix3D GetPostMatrix(Table.Table table, Origin origin)
		{
			switch (origin) {
				case Origin.Original:
					return new Matrix3D().SetTranslation(
						_data.Center.X,
						_data.Center.Y,
						table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y) * table.GetScaleZ()
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
				BaseColor = new Color(0x000000, ColorFormat.Bgr),
				WrapLighting = 0.5f,
				IsOpacityActive = true,
				Opacity = 0.2f,
				Glossiness = new Color(0xFFFFFF, ColorFormat.Bgr),
				IsMetal = false,
				Edge = 1.0f,
				EdgeAlpha = 1.0f,
				Roughness = 0.9f,
				GlossyImageLerp = 1.0f,
				Thickness = 0.05f,
				ClearCoat = new Color(0xFFFFFF, ColorFormat.Bgr),
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
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y) * table.GetScaleZ();
			var transX = origin == Origin.Global ? _data.Center.X : 0f;
			var transY = origin == Origin.Global ? _data.Center.Y : 0f;
			var transZ = origin == Origin.Global ? height : 0f;
			foreach (var vertex in bulbMesh.Vertices) {
				vertex.X = vertex.X * _data.MeshRadius + transX;
				vertex.Y = vertex.Y * _data.MeshRadius + transY;
				vertex.Z = vertex.Z * _data.MeshRadius * table.GetScaleZ() + transZ;
			}

			return bulbMesh;
		}

		private Mesh GetSocketMesh(Table.Table table, Origin origin)
		{
			var socketMesh = SocketMesh.Clone();
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y) * table.GetScaleZ();
			var transX = origin == Origin.Global ? _data.Center.X : 0f;
			var transY = origin == Origin.Global ? _data.Center.Y : 0f;
			var transZ = origin == Origin.Global ? height : 0f;

			foreach (var vertex in socketMesh.Vertices) {
				vertex.X = vertex.X * _data.MeshRadius + transX;
				vertex.Y = vertex.Y * _data.MeshRadius + transY;
				vertex.Z = vertex.Z * _data.MeshRadius * table.GetScaleZ() + transZ;
			}
			return socketMesh;
		}

		private bool RenderBulb()
		{
			return _data.IsBulbLight && _data.ShowBulbMesh;
		}
	}
}
