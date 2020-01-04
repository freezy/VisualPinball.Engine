using System;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Bumper
{
	internal class BumperMeshGenerator
	{
		private static readonly Mesh BaseMesh = new Mesh("Base", BumperBase.Vertices, BumperBase.Indices);
		private static readonly Mesh CapMesh = new Mesh("Cap", BumperCap.Vertices, BumperCap.Indices);
		private static readonly Mesh RingMesh = new Mesh("Ring", BumperRing.Vertices, BumperRing.Indices);
		private static readonly Mesh SocketMesh = new Mesh("Socket", BumperSocket.Vertices, BumperSocket.Indices);

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

		public Mesh[] GetMeshes(Table.Table table) {
			/* istanbul ignore if */
			if (_data.Center == null) {
				throw new InvalidOperationException($"Cannot export bumper {_data.Name} without center.");
			}
			var matrix = new Matrix3D().RotateZMatrix(MathF.DegToRad(_data.Orientation));
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y) * table.GetScaleZ();
			return new [] {
				GenerateMesh(_scaledBaseMesh, matrix, z => z * table.GetScaleZ() + height),
				GenerateMesh(_scaledRingMesh, matrix, z => z * table.GetScaleZ() + height),
				GenerateMesh(_scaledSocketMesh, matrix, z => z * table.GetScaleZ() + (height + 5.0f)),
				GenerateMesh(_scaledCapMesh, matrix, z => (z + _data.HeightScale) * table.GetScaleZ() + height )
			};
		}

		private Mesh GenerateMesh(Mesh mesh, Matrix3D matrix, Func<float, float> zPos) {
			var generatedMesh = mesh.Clone();
			foreach (var vertex in generatedMesh.Vertices) {
				var vert = new Vertex3D(vertex.X, vertex.Y, vertex.Z).MultiplyMatrix(matrix);
				vertex.X = vert.X + _data.Center.X;
				vertex.Y = vert.Y + _data.Center.Y;
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
