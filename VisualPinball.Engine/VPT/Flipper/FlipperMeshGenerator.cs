﻿// Visual Pinball Engine
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

// ReSharper disable CompareOfFloatsByEqualityOperator

#nullable enable

using System;
using System.Collections.Generic;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;
using MathF = VisualPinball.Engine.Math.MathF;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class FlipperMeshGenerator : MeshGenerator
	{
		public const string Base = "Base";
		public const string Rubber = "Rubber";

		private static readonly Mesh FlipperBaseMesh = new Mesh(Base, FlipperBase.Vertices, FlipperBase.Indices);

		private readonly FlipperData _data;

		protected override Vertex3D Position => new Vertex3D(_data.Center.X, _data.Center.Y, 0);
		protected override Vertex3D Scale => Vertex3D.One;
		protected override float RotationZ => MathF.DegToRad(_data.StartAngle);

		public FlipperMeshGenerator(FlipperData data)
		{
			_data = data;
		}

		public RenderObject? GetRenderObject(Table.Table table, string id, Origin origin, bool asRightHanded)
		{
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			var meshes = GenerateMeshes(height);
			var (preVertexMatrix, preNormalsMatrix) = GetPreMatrix(height, origin, asRightHanded);
			switch (id) {
				case Base:
					return new RenderObject(
						id,
						meshes[Base].Transform(preVertexMatrix, preNormalsMatrix),
						new PbrMaterial(table.GetMaterial(_data.Material), table.GetTexture(_data.Image)),
						_data.IsVisible
					);
				case Rubber:
					if (meshes.ContainsKey(Rubber)) {
						return new RenderObject(
							id,
							meshes[Rubber].Transform(preVertexMatrix, preNormalsMatrix),
							new PbrMaterial(table.GetMaterial(_data.RubberMaterial)),
							_data.IsVisible
						);
					}
					break;
			}
			return null;
		}

		public Mesh GetMesh(string id, float height)
		{
			var meshes = GenerateMeshes(height);
			var (preVertexMatrix, preNormalsMatrix) = GetPreMatrix(height, Origin.Original, false);
			switch (id) {
				case Base:
					return meshes[Base].Transform(preVertexMatrix, preNormalsMatrix);
				case Rubber:
					if (meshes.ContainsKey(Rubber)) {
						return meshes[Rubber].Transform(preVertexMatrix, preNormalsMatrix);
					}
					break;
			}
			return null;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded = true)
		{
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			var meshes = GenerateMeshes(table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y));
			var (preVertexMatrix, preNormalsMatrix) = GetPreMatrix(height, origin, asRightHanded);
			var postMatrix = GetPostMatrix(table, origin);
			var renderObjects = new List<RenderObject> {
				new RenderObject(
					Base,
					meshes[Base].Transform(preVertexMatrix, preNormalsMatrix),
					new PbrMaterial(table.GetMaterial(_data.Material), table.GetTexture(_data.Image)),
					_data.IsVisible
				)
			};

			if (meshes.ContainsKey(Rubber)) {
				renderObjects.Add(new RenderObject(
					Rubber,
					meshes[Rubber].Transform(preVertexMatrix, preNormalsMatrix),
					new PbrMaterial(table.GetMaterial(_data.RubberMaterial)),
					_data.IsVisible
				));
			}

			return new RenderObjectGroup(_data.Name, "Flippers", postMatrix, renderObjects.ToArray());
		}

		protected override float BaseHeight(Table.Table? table)
		{
			return 0f; // already in vertices
		}

		private Dictionary<string, Mesh> GenerateMeshes(float height)
		{
			var meshes = new Dictionary<string, Mesh>();
			var fullMatrix = new Matrix3D();
			fullMatrix.RotateZMatrix(MathF.DegToRad(180.0f));

			var baseRadius = _data.BaseRadius - _data.RubberThickness;
			var endRadius = _data.EndRadius - _data.RubberThickness;

			// calc angle needed to fix P0 location
			var sinAngle = (baseRadius - endRadius) / _data.FlipperRadius;
			if (sinAngle > 1.0) sinAngle = 1.0f;
			if (sinAngle < -1.0) sinAngle = -1.0f;
			var fixAngle = MathF.Asin(sinAngle);
			var fixAngleScale = fixAngle / (float)(System.Math.PI * 0.5); // scale (in relation to 90 deg.)
																	  // fixAngleScale = 0.0; // note: if you force fixAngleScale = 0.0 then all will look as old version

			// lambda used to apply fix
			void ApplyFix(ref Vertex3DNoTex2 vert, Vertex2D center, float midAngle, float radius, Vertex2D newCenter)
			{
				var vAngle = MathF.Atan2(vert.Y - center.Y, vert.X - center.X);
				var nAngle = MathF.Atan2(vert.Ny, vert.Nx);

				// we want have angles with same sign as midAngle, fix it:
				if (midAngle < 0.0) {
					if (vAngle > 0.0) vAngle -= (float) (System.Math.PI * 2.0);
					if (nAngle > 0.0) nAngle -= (float) (System.Math.PI * 2.0);
				}
				else {
					if (vAngle < 0.0) vAngle += (float) (System.Math.PI * 2.0);
					if (nAngle < 0.0) nAngle += (float) (System.Math.PI * 2.0);
				}

				nAngle -= (vAngle - midAngle) * fixAngleScale * MathF.Sign(midAngle);
				vAngle -= (vAngle - midAngle) * fixAngleScale * MathF.Sign(midAngle);
				float nL = new Vertex2D(vert.Nx, vert.Ny).Length();

				vert.X = MathF.Cos(vAngle) * radius + newCenter.X;
				vert.Y = MathF.Sin(vAngle) * radius + newCenter.Y;
				vert.Nx = MathF.Cos(nAngle) * nL;
				vert.Ny = MathF.Sin(nAngle) * nL;
			}

			// base and tip
			var baseMesh = FlipperBaseMesh.Clone(Base);
			for (var t = 0; t < 13; t++) {
				for (var i = 0; i < baseMesh.Vertices.Length; i++) {
					var v = baseMesh.Vertices[i];
					if (v.X == VertsBaseBottom[t].X && v.Y == VertsBaseBottom[t].Y && v.Z == VertsBaseBottom[t].Z) {
						ApplyFix(ref baseMesh.Vertices[i], new Vertex2D(VertsBaseBottom[6].X, VertsBaseBottom[0].Y),
							(float) -(System.Math.PI * 0.5), baseRadius, new Vertex2D(0, 0));
					}

					if (v.X == VertsTipBottom[t].X && v.Y == VertsTipBottom[t].Y && v.Z == VertsTipBottom[t].Z) {
						ApplyFix(ref baseMesh.Vertices[i], new Vertex2D(VertsTipBottom[6].X, VertsTipBottom[0].Y),
							(float) (System.Math.PI * 0.5), endRadius, new Vertex2D(0, _data.FlipperRadius));
					}

					if (v.X == VertsBaseTop[t].X && v.Y == VertsBaseTop[t].Y && v.Z == VertsBaseTop[t].Z) {
						ApplyFix(ref baseMesh.Vertices[i], new Vertex2D(VertsBaseBottom[6].X, VertsBaseBottom[0].Y),
							(float) (-System.Math.PI * 0.5), baseRadius, new Vertex2D(0, 0));
					}

					if (v.X == VertsTipTop[t].X && v.Y == VertsTipTop[t].Y && v.Z == VertsTipTop[t].Z) {
						ApplyFix(ref baseMesh.Vertices[i], new Vertex2D(VertsTipBottom[6].X, VertsTipBottom[0].Y),
							(float) (System.Math.PI * 0.5), endRadius, new Vertex2D(0, _data.FlipperRadius));
					}
				}
			}

			baseMesh.Transform(fullMatrix, null, z => z * _data.Height + height);
			meshes[Base] = baseMesh;

			// rubber
			if (_data.RubberThickness > 0.0)
			{
				var rubberMesh = FlipperBaseMesh.Clone(Rubber);
				for (var t = 0; t < 13; t++) {
					for (var i = 0; i < rubberMesh.Vertices.Length; i++) {
						var v = rubberMesh.Vertices[i];
						if (v.X == VertsBaseBottom[t].X && v.Y == VertsBaseBottom[t].Y && v.Z == VertsBaseBottom[t].Z) {
							ApplyFix(ref rubberMesh.Vertices[i], new Vertex2D(VertsBaseBottom[6].X, VertsBaseBottom[0].Y),
								(float) (-System.Math.PI * 0.5), baseRadius + _data.RubberThickness,
								new Vertex2D(0, 0));
						}

						if (v.X == VertsTipBottom[t].X && v.Y == VertsTipBottom[t].Y && v.Z == VertsTipBottom[t].Z) {
							ApplyFix(ref rubberMesh.Vertices[i], new Vertex2D(VertsTipBottom[6].X, VertsTipBottom[0].Y),
								(float) (System.Math.PI * 0.5), endRadius + _data.RubberThickness,
								new Vertex2D(0, _data.FlipperRadius));
						}

						if (v.X == VertsBaseTop[t].X && v.Y == VertsBaseTop[t].Y && v.Z == VertsBaseTop[t].Z) {
							ApplyFix(ref rubberMesh.Vertices[i], new Vertex2D(VertsBaseBottom[6].X, VertsBaseBottom[0].Y),
								(float) (-System.Math.PI * 0.5), baseRadius + _data.RubberThickness,
								new Vertex2D(0, 0));
						}

						if (v.X == VertsTipTop[t].X && v.Y == VertsTipTop[t].Y && v.Z == VertsTipTop[t].Z) {
							ApplyFix(ref rubberMesh.Vertices[i], new Vertex2D(VertsTipBottom[6].X, VertsTipBottom[0].Y),
								(float) (System.Math.PI * 0.5), endRadius + _data.RubberThickness,
								new Vertex2D(0, _data.FlipperRadius));
						}
					}
				}

				rubberMesh.Transform(fullMatrix, null,
					z => z * _data.RubberWidth + (height + _data.RubberHeight));
				meshes[Rubber] = rubberMesh;
			}

			return meshes;
		}

		#region Mesh Data

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

		#endregion
	}
}
