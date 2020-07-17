using System;
using System.Collections.Generic;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Light
{
	internal class LightMeshGenerator
	{
		private static readonly Mesh Bulb = new Mesh("Bulb", BulbMesh.Vertices, BulbMesh.Indices);
		private static readonly Mesh Socket = new Mesh("Socket", BulbSocket.Vertices, BulbSocket.Indices);

		private readonly LightData _data;

		internal LightMeshGenerator(LightData data)
		{
			_data = data;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded = true)
		{
			var translationMatrix = GetPostMatrix(table, origin);
			if (!RenderBulb()) {
				return new RenderObjectGroup(_data.Name, "Lights", translationMatrix);
			}
			var meshes = GetMeshes(table, origin);
			return new RenderObjectGroup(_data.Name, "Lights", translationMatrix,
				new RenderObject(
					"Bulb",
					asRightHanded ? meshes["Bulb"].Transform(Matrix3D.RightHanded) : meshes["Bulb"],
					new PbrMaterial(GetBulbMaterial()),
					true
				),
				new RenderObject(
					"Socket",
					asRightHanded ? meshes["Socket"].Transform(Matrix3D.RightHanded) : meshes["Socket"],
					new PbrMaterial(GetSocketMaterial()),
					true
				)
			);
		}

		private Matrix3D GetPostMatrix(Table.Table table, Origin origin)
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

		private Dictionary<string, Mesh> GetMeshes(Table.Table table, Origin origin)
		{
			var lightMesh = Bulb.Clone();
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y) * table.GetScaleZ();
			var transX = origin == Origin.Global ? _data.Center.X : 0f;
			var transY = origin == Origin.Global ? _data.Center.Y : 0f;
			var transZ = origin == Origin.Global ? height : 0f;
			foreach (var vertex in lightMesh.Vertices) {
				vertex.X = vertex.X * _data.MeshRadius + transX;
				vertex.Y = vertex.Y * _data.MeshRadius + transY;
				vertex.Z = vertex.Z * _data.MeshRadius * table.GetScaleZ() + transZ;
			}

			var socketMesh = Socket.Clone();
			foreach (var vertex in socketMesh.Vertices) {
				vertex.X = vertex.X * _data.MeshRadius + transX;
				vertex.Y = vertex.Y * _data.MeshRadius + transY;
				vertex.Z = vertex.Z * _data.MeshRadius * table.GetScaleZ() + transZ;
			}

			return new Dictionary<string, Mesh> {
				{ "Bulb", lightMesh },
				{ "Socket", socketMesh },
			};
		}

		private bool RenderBulb()
		{
			return _data.IsBulbLight && _data.ShowBulbMesh;
		}
	}
}
