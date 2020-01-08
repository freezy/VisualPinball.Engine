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

		public RenderObject[] GetRenderObjects(Table.Table table)
		{
			if (!RenderBulb()) {
				return new RenderObject[0];
			}
			var meshes = GetMeshes(table);
			return new[] {
				new RenderObject(
					name: "Bulb",
					mesh: meshes["Bulb"],
					material: GetBulbMaterial()
				),
				new RenderObject(
					name: "Socket",
					mesh: meshes["Socket"],
					material: GetSocketMaterial()
				),
			};
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

		private Dictionary<string, Mesh> GetMeshes(Table.Table table)
		{
			var lightMesh = Bulb.Clone();
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y) * table.GetScaleZ();
			foreach (var vertex in lightMesh.Vertices) {
				vertex.X = vertex.X * _data.MeshRadius + _data.Center.X;
				vertex.Y = vertex.Y * _data.MeshRadius + _data.Center.Y;
				vertex.Z = vertex.Z * _data.MeshRadius * table.GetScaleZ() + height;
			}

			var socketMesh = Socket.Clone();
			foreach (var vertex in socketMesh.Vertices) {
				vertex.X = vertex.X * _data.MeshRadius + _data.Center.X;
				vertex.Y = vertex.Y * _data.MeshRadius + _data.Center.Y;
				vertex.Z = vertex.Z * _data.MeshRadius * table.GetScaleZ() + height;
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
