// ReSharper disable CompareOfFloatsByEqualityOperator

using System.Collections.Generic;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class FlipperMeshGenerator
	{
		private static readonly Mesh FlipperBaseMesh = new Mesh("Base", FlipperBase.Vertices, FlipperBase.Indices);

		private readonly FlipperData _data;

		public FlipperMeshGenerator(FlipperData data)
		{
			_data = data;
		}

		public RenderObject[] GetRenderObjects(Table.Table table)
		{
			var meshes = GenerateMeshes(table);
			var matrix = GetMatrix(_data.StartAngle);
			var renderObjects = new List<RenderObject>();

			renderObjects.Add(new RenderObject(
				name: "Base",
				mesh: meshes["Base"].Transform(matrix),
				material: table.GetMaterial(_data.Material),
				map: table.GetTexture(_data.Image),
				isVisible: _data.IsVisible));

			if (meshes.ContainsKey("Rubber")) {
				renderObjects.Add(new RenderObject(
					name: "Rubber",
					mesh: meshes["Rubber"].Transform(matrix),
					material: table.GetMaterial(_data.RubberMaterial),
					isVisible: _data.IsVisible));
			}

			return renderObjects.ToArray();
		}

		private Matrix3D GetMatrix(float rotation)
		{
			var trafoMatrix = new Matrix3D();
			var tempMatrix = new Matrix3D();
			trafoMatrix.SetTranslation(_data.Center.X, _data.Center.Y, 0);
			tempMatrix.RotateZMatrix(MathF.DegToRad(rotation));
			trafoMatrix.PreMultiply(tempMatrix);

			return trafoMatrix;
		}

		private Dictionary<string, Mesh> GenerateMeshes(Table.Table table)
		{
			var meshes = new Dictionary<string, Mesh>();
			var fullMatrix = new Matrix3D();
			fullMatrix.RotateZMatrix(MathF.DegToRad(180.0f));

			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			const float baseScale = 10.0f;
			const float tipScale = 10.0f;
			var baseRadius = _data.BaseRadius - _data.RubberThickness;
			var endRadius = _data.EndRadius - _data.RubberThickness;

			// base and tip
			var baseMesh = FlipperBaseMesh.Clone("Base");
			for (var t = 0; t < 13; t++) {
				foreach (var v in baseMesh.Vertices) {
					if (v.X == VertsBaseBottom[t].X && v.Y == VertsBaseBottom[t].Y && v.Z == VertsBaseBottom[t].Z) {
						v.X *= baseRadius * baseScale;
						v.Y *= baseRadius * baseScale;
					}

					if (v.X == VertsTipBottom[t].X && v.Y == VertsTipBottom[t].Y && v.Z == VertsTipBottom[t].Z) {
						v.X *= endRadius * tipScale;
						v.Y *= endRadius * tipScale;
						v.Y += _data.FlipperRadius - endRadius * 7.9f;
					}

					if (v.X == VertsBaseTop[t].X && v.Y == VertsBaseTop[t].Y && v.Z == VertsBaseTop[t].Z) {
						v.X *= baseRadius * baseScale;
						v.Y *= baseRadius * baseScale;
					}

					if (v.X == VertsTipTop[t].X && v.Y == VertsTipTop[t].Y && v.Z == VertsTipTop[t].Z) {
						v.X *= endRadius * tipScale;
						v.Y *= endRadius * tipScale;
						v.Y += _data.FlipperRadius - endRadius * 7.9f;
					}
				}
			}

			baseMesh.Transform(fullMatrix, null, z => z * _data.Height * table.GetScaleZ() + height);
			meshes["Base"] = baseMesh;

			// rubber
			if (_data.RubberThickness > 0.0) {
				const float rubberBaseScale = 10.0f;
				const float rubberTipScale = 10.0f;
				var rubberMesh = FlipperBaseMesh.Clone("Rubber");
				for (var t = 0; t < 13; t++) {
					foreach (var v in rubberMesh.Vertices) {
						if (v.X == VertsBaseBottom[t].X && v.Y == VertsBaseBottom[t].Y && v.Z == VertsBaseBottom[t].Z) {
							v.X = v.X * _data.BaseRadius * rubberBaseScale;
							v.Y = v.Y * _data.BaseRadius * rubberBaseScale;
						}

						if (v.X == VertsTipBottom[t].X && v.Y == VertsTipBottom[t].Y && v.Z == VertsTipBottom[t].Z) {
							v.X = v.X * _data.EndRadius * rubberTipScale;
							v.Y = v.Y * _data.EndRadius * rubberTipScale;
							v.Y = v.Y + _data.FlipperRadius - _data.EndRadius * 7.9f;
						}

						if (v.X == VertsBaseTop[t].X && v.Y == VertsBaseTop[t].Y && v.Z == VertsBaseTop[t].Z) {
							v.X = v.X * _data.BaseRadius * rubberBaseScale;
							v.Y = v.Y * _data.BaseRadius * rubberBaseScale;
						}

						if (v.X == VertsTipTop[t].X && v.Y == VertsTipTop[t].Y && v.Z == VertsTipTop[t].Z) {
							v.X = v.X * _data.EndRadius * rubberTipScale;
							v.Y = v.Y * _data.EndRadius * rubberTipScale;
							v.Y = v.Y + _data.FlipperRadius - _data.EndRadius * 7.9f;
						}
					}
				}

				rubberMesh.Transform(fullMatrix, null,
					z => z * _data.RubberWidth * table.GetScaleZ() + (height + _data.RubberHeight));
				meshes["Rubber"] = rubberMesh;
			}

			return meshes;
		}

		private static readonly Vertex3D[] VertsTipBottom = {
			new Vertex3D(-0.101425f, 0.786319f, 0.003753f),
			new Vertex3D(-0.097969f, 0.812569f, 0.003753f),
			new Vertex3D(-0.087837f, 0.837031f, 0.003753f),
			new Vertex3D(-0.071718f, 0.858037f, 0.003753f),
			new Vertex3D(-0.050713f, 0.874155f, 0.003753f),
			new Vertex3D(-0.026251f, 0.884288f, 0.003753f),
			new Vertex3D(-0.000000f, 0.887744f, 0.003753f),
			new Vertex3D(0.026251f, 0.884288f, 0.003753f),
			new Vertex3D(0.050713f, 0.874155f, 0.003753f),
			new Vertex3D(0.071718f, 0.858037f, 0.003753f),
			new Vertex3D(0.087837f, 0.837031f, 0.003753f),
			new Vertex3D(0.097969f, 0.812569f, 0.003753f),
			new Vertex3D(0.101425f, 0.786319f, 0.003753f),
		};

		private static readonly Vertex3D[] VertsTipTop = {
			new Vertex3D(-0.101425f, 0.786319f, 1.004253f),
			new Vertex3D(-0.097969f, 0.812569f, 1.004253f),
			new Vertex3D(-0.087837f, 0.837031f, 1.004253f),
			new Vertex3D(-0.071718f, 0.858037f, 1.004253f),
			new Vertex3D(-0.050713f, 0.874155f, 1.004253f),
			new Vertex3D(-0.026251f, 0.884288f, 1.004253f),
			new Vertex3D(-0.000000f, 0.887744f, 1.004253f),
			new Vertex3D(0.026251f, 0.884288f, 1.004253f),
			new Vertex3D(0.050713f, 0.874155f, 1.004253f),
			new Vertex3D(0.071718f, 0.858037f, 1.004253f),
			new Vertex3D(0.087837f, 0.837031f, 1.004253f),
			new Vertex3D(0.097969f, 0.812569f, 1.004253f),
			new Vertex3D(0.101425f, 0.786319f, 1.004253f),
		};

		private static readonly Vertex3D[] VertsBaseBottom = {
			new Vertex3D(-0.100762f, -0.000000f, 0.003753f),
			new Vertex3D(-0.097329f, -0.026079f, 0.003753f),
			new Vertex3D(-0.087263f, -0.050381f, 0.003753f),
			new Vertex3D(-0.071250f, -0.071250f, 0.003753f),
			new Vertex3D(-0.050381f, -0.087263f, 0.003753f),
			new Vertex3D(-0.026079f, -0.097329f, 0.003753f),
			new Vertex3D(-0.000000f, -0.100762f, 0.003753f),
			new Vertex3D(0.026079f, -0.097329f, 0.003753f),
			new Vertex3D(0.050381f, -0.087263f, 0.003753f),
			new Vertex3D(0.071250f, -0.071250f, 0.003753f),
			new Vertex3D(0.087263f, -0.050381f, 0.003753f),
			new Vertex3D(0.097329f, -0.026079f, 0.003753f),
			new Vertex3D(0.100762f, -0.000000f, 0.003753f),
		};

		private static readonly Vertex3D[] VertsBaseTop = {
			new Vertex3D(-0.100762f, 0.000000f, 1.004253f),
			new Vertex3D(-0.097329f, -0.026079f, 1.004253f),
			new Vertex3D(-0.087263f, -0.050381f, 1.004253f),
			new Vertex3D(-0.071250f, -0.071250f, 1.004253f),
			new Vertex3D(-0.050381f, -0.087263f, 1.004253f),
			new Vertex3D(-0.026079f, -0.097329f, 1.004253f),
			new Vertex3D(-0.000000f, -0.100762f, 1.004253f),
			new Vertex3D(0.026079f, -0.097329f, 1.004253f),
			new Vertex3D(0.050381f, -0.087263f, 1.004253f),
			new Vertex3D(0.071250f, -0.071250f, 1.004253f),
			new Vertex3D(0.087263f, -0.050381f, 1.004253f),
			new Vertex3D(0.097329f, -0.026079f, 1.004253f),
			new Vertex3D(0.100762f, -0.000000f, 1.004253f),
		};
	}
}
