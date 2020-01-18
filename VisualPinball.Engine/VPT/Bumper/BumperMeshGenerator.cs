using System;
using System.Collections.Generic;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Bumper
{
	internal class BumperMeshGenerator
	{
		private static readonly Mesh BaseMesh = new Mesh("Base", BumperBase.Vertices, BumperBase.Indices);
		private static readonly Mesh CapMesh = new Mesh("Cap", BumperCap.Vertices, BumperCap.Indices);
		private static readonly Mesh RingMesh = new Mesh("Ring", BumperRing.Vertices, BumperRing.Indices);
		private static readonly Mesh SocketMesh = new Mesh("Skirt", BumperSocket.Vertices, BumperSocket.Indices);

		private readonly BumperData _data;

		private readonly Mesh _scaledBaseMesh;
		private readonly Mesh _scaledCapMesh;
		private readonly Mesh _scaledRingMesh;
		private readonly Mesh _scaledSocketMesh;

		internal BumperMeshGenerator(BumperData data) {
			_data = data;
			_scaledBaseMesh = BaseMesh.Clone().MakeScale(_data.Radius, _data.Radius, _data.HeightScale);
			_scaledCapMesh = CapMesh.Clone().MakeScale(_data.Radius * 2, _data.Radius * 2, _data.HeightScale);
			_scaledRingMesh = RingMesh.Clone().MakeScale(_data.Radius, _data.Radius, _data.HeightScale);
			_scaledSocketMesh = SocketMesh.Clone().MakeScale(_data.Radius, _data.Radius, _data.HeightScale);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			var meshes = GetMeshes(table, origin);
			var translationMatrix = GetPostMatrix(origin);

			return new RenderObjectGroup(_data.Name, "Bumpers", new[] {
				new RenderObject(
					name: "Base",
					mesh: asRightHanded ? meshes["Base"].Transform(Matrix3D.RightHanded) : meshes["Base"],
					material: table.GetMaterial(_data.BaseMaterial),
					map: Texture.BumperBase,
					isVisible: _data.IsBaseVisible
				),
				new RenderObject(
					name: "Ring",
					mesh: asRightHanded ? meshes["Ring"].Transform(Matrix3D.RightHanded) : meshes["Ring"],
					material: table.GetMaterial(_data.RingMaterial),
					map: Texture.BumperRing,
					isVisible: _data.IsRingVisible
				),
				new RenderObject(
					name: "Skirt",
					mesh: asRightHanded ? meshes["Skirt"].Transform(Matrix3D.RightHanded) : meshes["Skirt"],
					material: table.GetMaterial(_data.SocketMaterial),
					map: Texture.BumperSocket,
					isVisible: _data.IsSocketVisible
				),
				new RenderObject(
					name: "Cap",
					mesh: asRightHanded ? meshes["Cap"].Transform(Matrix3D.RightHanded) : meshes["Cap"],
					material: table.GetMaterial(_data.CapMaterial),
					map: Texture.BumperCap,
					isVisible: _data.IsCapVisible
				)
			}, translationMatrix);
		}

		private Matrix3D GetPostMatrix(Origin origin)
		{
			switch (origin) {
				case Origin.Original:
					return new Matrix3D().SetTranslation(_data.Center.X, _data.Center.Y, 0f);

				case Origin.Global:
					return Matrix3D.Identity;

				default:
					throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown origin " + origin);
			}
		}

		private Dictionary<string, Mesh> GetMeshes(Table.Table table, Origin origin) {
			if (_data.Center == null) {
				throw new InvalidOperationException($"Cannot export bumper {_data.Name} without center.");
			}
			var matrix = new Matrix3D().RotateZMatrix(MathF.DegToRad(_data.Orientation));
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y) * table.GetScaleZ();
			return new Dictionary<string, Mesh> {
				{ "Base", GenerateMesh(_scaledBaseMesh, matrix, z => z * table.GetScaleZ() + height, origin) },
				{ "Ring", GenerateMesh(_scaledRingMesh, matrix, z => z * table.GetScaleZ() + height, origin) },
				{ "Skirt", GenerateMesh(_scaledSocketMesh, matrix, z => z * table.GetScaleZ() + (height + 5.0f), origin) },
				{ "Cap", GenerateMesh(_scaledCapMesh, matrix, z => (z + _data.HeightScale) * table.GetScaleZ() + height, origin) }
			};
		}

		private Mesh GenerateMesh(Mesh mesh, Matrix3D matrix, Func<float, float> zPos, Origin origin) {
			var generatedMesh = mesh.Clone();
			foreach (var vertex in generatedMesh.Vertices) {
				var vert = new Vertex3D(vertex.X, vertex.Y, vertex.Z).MultiplyMatrix(matrix);
				if (origin == Origin.Global) {
					vertex.X = vert.X + _data.Center.X;
					vertex.Y = vert.Y + _data.Center.Y;
				}
				vertex.Z = zPos(vert.Z);

				var normal = new Vertex3D(vertex.Nx, vertex.Ny, vertex.Nz).MultiplyMatrixNoTranslate(matrix);
				vertex.Nx = normal.X;
				vertex.Ny = normal.Y;
				vertex.Nz = normal.Z;
			}
			return generatedMesh;
		}
	}
}
