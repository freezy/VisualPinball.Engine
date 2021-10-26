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
using MathF = VisualPinball.Engine.Math.MathF;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class FlipperMeshGenerator : MeshGenerator
	{
		public const string Base = "Base";
		public const string Rubber = "Rubber";

		private readonly IFlipperData _data;

		protected override Vertex3D Position => new Vertex3D(_data.PosX, _data.PosY, 0);
		protected override Vertex3D Scale => Vertex3D.One;
		protected override float RotationZ => MathF.DegToRad(_data.StartAngle);

		public FlipperMeshGenerator(IFlipperData data)
		{
			_data = data;
		}

		public Mesh GetMesh(string id, float height, Origin origin = Origin.Original, bool asRightHanded = false, float margin = 0f)
		{
			return GetMesh(id, height, 0, origin, asRightHanded, margin);
		}

		public Mesh GetMesh(string id, float height, float zTransform, Origin origin = Origin.Original, bool asRightHanded = false, float margin = 0f)
		{
			var meshes = GenerateMeshes(height, margin);
			var (preVertexMatrix, preNormalsMatrix) = GetPreMatrix(zTransform, origin, asRightHanded);
			switch (id) {
				case Base:
					return meshes[Base].Transform(preVertexMatrix, preNormalsMatrix);
				case Rubber:
					return meshes[Rubber].Transform(preVertexMatrix, preNormalsMatrix);
			}
			throw new ArgumentException($"Unknown flipper ID \"{id}\".", nameof(id));
		}

		public static PbrMaterial GetMaterial(string id, Table.Table table, FlipperData data)
		{
			switch (id) {
				case Base:
					return new PbrMaterial(table.GetMaterial(data.Material), table.GetTexture(data.Image));
				case Rubber:
					return new PbrMaterial(table.GetMaterial(data.RubberMaterial));
			}
			throw new ArgumentException($"Unknown flipper ID \"{id}\".", nameof(id));
		}

		protected override float BaseHeight(Table.Table? table)
		{
			return 0f; // already in vertices
		}

		private Dictionary<string, Mesh> GenerateMeshes(float height, float margin = 0f)
		{
			var meshes = new Dictionary<string, Mesh>();
			var fullMatrix = new Matrix3D();
			fullMatrix.RotateZMatrix(MathF.DegToRad(180.0f));

			var baseRadius = _data.BaseRadius - _data.RubberThickness + margin;
			var endRadius = _data.EndRadius - _data.RubberThickness + margin;

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
			var baseMesh = new Mesh(Base, Vertices, Indices).Clone();
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
			var rubberMesh = new Mesh(Rubber, Vertices, Indices).Clone();
			for (var t = 0; t < 13; t++) {
				for (var i = 0; i < rubberMesh.Vertices.Length; i++) {
					var v = rubberMesh.Vertices[i];
					if (v.X == VertsBaseBottom[t].X && v.Y == VertsBaseBottom[t].Y && v.Z == VertsBaseBottom[t].Z) {
						ApplyFix(ref rubberMesh.Vertices[i], new Vertex2D(VertsBaseBottom[6].X, VertsBaseBottom[0].Y),
							(float) (-System.Math.PI * 0.5), baseRadius + _data.RubberThickness + margin,
							new Vertex2D(0, 0));
					}

					if (v.X == VertsTipBottom[t].X && v.Y == VertsTipBottom[t].Y && v.Z == VertsTipBottom[t].Z) {
						ApplyFix(ref rubberMesh.Vertices[i], new Vertex2D(VertsTipBottom[6].X, VertsTipBottom[0].Y),
							(float) (System.Math.PI * 0.5), endRadius + _data.RubberThickness + margin,
							new Vertex2D(0, _data.FlipperRadius));
					}

					if (v.X == VertsBaseTop[t].X && v.Y == VertsBaseTop[t].Y && v.Z == VertsBaseTop[t].Z) {
						ApplyFix(ref rubberMesh.Vertices[i], new Vertex2D(VertsBaseBottom[6].X, VertsBaseBottom[0].Y),
							(float) (-System.Math.PI * 0.5), baseRadius + _data.RubberThickness + margin,
							new Vertex2D(0, 0));
					}

					if (v.X == VertsTipTop[t].X && v.Y == VertsTipTop[t].Y && v.Z == VertsTipTop[t].Z) {
						ApplyFix(ref rubberMesh.Vertices[i], new Vertex2D(VertsTipBottom[6].X, VertsTipBottom[0].Y),
							(float) (System.Math.PI * 0.5), endRadius + _data.RubberThickness + margin,
							new Vertex2D(0, _data.FlipperRadius));
					}
				}
			}
			rubberMesh.Transform(fullMatrix, null, z => z * _data.RubberWidth + (height + _data.RubberHeight + margin * 10f));
			meshes[Rubber] = rubberMesh;

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

		private static readonly float[][] Vertices = {
			new[] {-0.101425f, 0.786319f, 0.003753f, -0.997900f, 0.065000f, -0.000000f, 0.126235f, 0.422635f},
			new[] {-0.101425f, 0.786319f, 1.004253f, -0.997900f, 0.065000f, -0.000000f, 0.068619f, 0.486620f},
			new[] {-0.097969f, 0.812569f, 1.004253f, -0.965900f, 0.258800f, -0.000000f, 0.055945f, 0.474362f},
			new[] {-0.097969f, 0.812569f, 0.003753f, -0.965900f, 0.258800f, -0.000000f, 0.113479f, 0.420353f},
			new[] {-0.050713f, 0.874155f, 0.003753f, -0.500000f, 0.866000f, -0.000000f, 0.095408f, 0.402450f},
			new[] {-0.050713f, 0.874155f, 1.004253f, -0.500000f, 0.866000f, -0.000000f, 0.024611f, 0.428958f},
			new[] {-0.026251f, 0.884288f, 1.004253f, -0.258800f, 0.965900f, -0.000000f, 0.018144f, 0.410811f},
			new[] {-0.026251f, 0.884288f, 0.003753f, -0.258800f, 0.965900f, -0.000000f, 0.092619f, 0.394091f},
			new[] {0.050713f, 0.874155f, 0.003753f, 0.500000f, 0.866000f, -0.000000f, 0.094255f, 0.367888f},
			new[] {0.050713f, 0.874155f, 1.004253f, 0.500000f, 0.866000f, -0.000000f, 0.015402f, 0.351824f},
			new[] {0.071718f, 0.858037f, 1.004253f, 0.707100f, 0.707100f, -0.000000f, 0.020311f, 0.332136f},
			new[] {0.071718f, 0.858037f, 0.003753f, 0.707100f, 0.707100f, -0.000000f, 0.098450f, 0.360184f},
			new[] {-0.050713f, 0.874155f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.020766f, 0.089615f},
			new[] {-0.026251f, 0.884288f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.015631f, 0.077247f},
			new[] {-0.000000f, 0.887744f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.013871f, 0.063972f},
			new[] {0.026251f, 0.884288f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.015608f, 0.050694f},
			new[] {0.050713f, 0.874155f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.020722f, 0.038317f},
			new[] {0.071718f, 0.858037f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.028865f, 0.027686f},
			new[] {0.087837f, 0.837031f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.039483f, 0.019525f},
			new[] {0.097969f, 0.812569f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.051850f, 0.014390f},
			new[] {0.101425f, 0.786319f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.065126f, 0.012631f},
			new[] {0.100762f, -0.000000f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.462821f, 0.012629f},
			new[] {0.097329f, -0.026079f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.476012f, 0.014355f},
			new[] {0.087263f, -0.050381f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.488308f, 0.019436f},
			new[] {0.071250f, -0.071250f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.498869f, 0.027526f},
			new[] {0.050381f, -0.087263f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.506977f, 0.038073f},
			new[] {0.026079f, -0.097329f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.512079f, 0.050360f},
			new[] {-0.000000f, -0.100762f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.513826f, 0.063549f},
			new[] {-0.026079f, -0.097329f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.512101f, 0.076740f},
			new[] {-0.050381f, -0.087263f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.507020f, 0.089036f},
			new[] {-0.071250f, -0.071250f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.498930f, 0.099597f},
			new[] {-0.087263f, -0.050381f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.488382f, 0.107705f},
			new[] {-0.097329f, -0.026079f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.476096f, 0.112806f},
			new[] {-0.100762f, -0.000000f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.462907f, 0.114554f},
			new[] {-0.101425f, 0.786319f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.065212f, 0.115226f},
			new[] {-0.097969f, 0.812569f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.051934f, 0.113489f},
			new[] {-0.087837f, 0.837031f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.039558f, 0.108375f},
			new[] {-0.071718f, 0.858037f, 0.003753f, 0.000000f, 0.000000f, -1.000000f, 0.028927f, 0.100232f},
			new[] {-0.087837f, 0.837031f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.047295f, 0.152337f},
			new[] {-0.097969f, 0.812569f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.059663f, 0.147202f},
			new[] {-0.101425f, 0.786319f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.072938f, 0.145443f},
			new[] {-0.100762f, 0.000000f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.470633f, 0.145442f},
			new[] {-0.097329f, -0.026079f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.483825f, 0.147167f},
			new[] {-0.087263f, -0.050381f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.496120f, 0.152248f},
			new[] {-0.071250f, -0.071250f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.506682f, 0.160338f},
			new[] {-0.050381f, -0.087263f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.514789f, 0.170886f},
			new[] {-0.026079f, -0.097329f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.519891f, 0.183173f},
			new[] {-0.000000f, -0.100762f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.521639f, 0.196361f},
			new[] {0.026079f, -0.097329f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.519913f, 0.209553f},
			new[] {0.050381f, -0.087263f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.514832f, 0.221848f},
			new[] {0.071250f, -0.071250f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.506743f, 0.232410f},
			new[] {0.087263f, -0.050381f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.496195f, 0.240517f},
			new[] {0.097329f, -0.026079f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.483908f, 0.245619f},
			new[] {0.100762f, 0.000000f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.470719f, 0.247367f},
			new[] {0.101425f, 0.786319f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.073025f, 0.248038f},
			new[] {0.097969f, 0.812569f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.059747f, 0.246302f},
			new[] {0.087837f, 0.837031f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.047370f, 0.241188f},
			new[] {0.071718f, 0.858037f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.036739f, 0.233045f},
			new[] {0.050713f, 0.874155f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.028578f, 0.222427f},
			new[] {0.026251f, 0.884288f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.023443f, 0.210059f},
			new[] {-0.000000f, 0.887744f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.021684f, 0.196784f},
			new[] {-0.026251f, 0.884288f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.023421f, 0.183506f},
			new[] {-0.050713f, 0.874155f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.028535f, 0.171130f},
			new[] {-0.071718f, 0.858037f, 1.004253f, 0.000000f, 0.000000f, 1.000000f, 0.036678f, 0.160499f},
			new[] {0.050381f, -0.087263f, 0.003753f, 0.500000f, -0.866000f, -0.000000f, 0.438656f, 0.363047f},
			new[] {0.050381f, -0.087263f, 1.004253f, 0.500000f, -0.866000f, -0.000000f, 0.506767f, 0.339752f},
			new[] {0.026079f, -0.097329f, 1.004253f, 0.258800f, -0.965900f, -0.000000f, 0.511832f, 0.357037f},
			new[] {0.026079f, -0.097329f, 0.003753f, 0.258800f, -0.965900f, -0.000000f, 0.441094f, 0.370485f},
			new[] {-0.087837f, 0.837031f, 0.003753f, -0.866000f, 0.500000f, -0.000000f, 0.105813f, 0.416117f},
			new[] {-0.087837f, 0.837031f, 1.004253f, -0.866000f, 0.500000f, -0.000000f, 0.044005f, 0.460868f},
			new[] {-0.071718f, 0.858037f, 1.004253f, -0.707100f, 0.707100f, -0.000000f, 0.033382f, 0.445712f},
			new[] {-0.071718f, 0.858037f, 0.003753f, -0.707100f, 0.707100f, -0.000000f, 0.099801f, 0.409952f},
			new[] {0.026251f, 0.884288f, 0.003753f, 0.258800f, 0.965900f, -0.000000f, 0.091964f, 0.376411f},
			new[] {0.026251f, 0.884288f, 1.004253f, 0.258800f, 0.965900f, -0.000000f, 0.013389f, 0.371796f},
			new[] {0.100762f, 0.000000f, 1.004253f, 0.997800f, -0.065800f, -0.000000f, 0.468620f, 0.280370f},
			new[] {0.097329f, -0.026079f, 1.004253f, 0.965900f, -0.258800f, -0.000000f, 0.479895f, 0.294135f},
			new[] {0.097329f, -0.026079f, 0.003753f, 0.965900f, -0.258800f, -0.000000f, 0.417618f, 0.344945f},
			new[] {0.100762f, -0.000000f, 0.003753f, 0.997800f, -0.065800f, -0.000000f, 0.407171f, 0.339603f},
			new[] {-0.050381f, -0.087263f, 0.003753f, -0.500000f, -0.866000f, -0.000000f, 0.437853f, 0.393942f},
			new[] {-0.050381f, -0.087263f, 1.004253f, -0.500000f, -0.866000f, -0.000000f, 0.510807f, 0.410912f},
			new[] {-0.071250f, -0.071250f, 1.004253f, -0.707100f, -0.707100f, -0.000000f, 0.505895f, 0.428541f},
			new[] {-0.071250f, -0.071250f, 0.003753f, -0.707100f, -0.707100f, -0.000000f, 0.433266f, 0.401633f},
			new[] {-0.000000f, -0.100762f, 1.004253f, 0.000000f, -1.000000f, -0.000000f, 0.513324f, 0.374983f},
			new[] {-0.000000f, -0.100762f, 0.003753f, 0.000000f, -1.000000f, -0.000000f, 0.441814f, 0.378280f},
			new[] {0.087263f, -0.050381f, 1.004253f, 0.866000f, -0.500000f, -0.000000f, 0.490376f, 0.308278f},
			new[] {0.087263f, -0.050381f, 0.003753f, 0.866000f, -0.500000f, -0.000000f, 0.427220f, 0.349765f},
			new[] {-0.000000f, 0.887744f, 0.003753f, 0.000000f, 1.000000f, -0.000000f, 0.091451f, 0.385282f},
			new[] {-0.000000f, 0.887744f, 1.004253f, 0.000000f, 1.000000f, -0.000000f, 0.014330f, 0.391613f},
			new[] {0.101425f, 0.786319f, 0.003753f, 0.997900f, 0.065000f, -0.000000f, 0.138673f, 0.343607f},
			new[] {0.101425f, 0.786319f, 1.004253f, 0.997900f, 0.065000f, -0.000000f, 0.049811f, 0.277514f},
			new[] {0.097969f, 0.812569f, 0.003753f, 0.965900f, 0.258800f, -0.000000f, 0.115647f, 0.347988f},
			new[] {0.097969f, 0.812569f, 1.004253f, 0.965900f, 0.258800f, -0.000000f, 0.037908f, 0.294943f},
			new[] {-0.026079f, -0.097329f, 0.003753f, -0.258800f, -0.965900f, -0.000000f, 0.440688f, 0.386117f},
			new[] {-0.026079f, -0.097329f, 1.004253f, -0.258800f, -0.965900f, -0.000000f, 0.513324f, 0.392950f},
			new[] {-0.087263f, -0.050381f, 1.004253f, -0.866000f, -0.500000f, -0.000000f, 0.498810f, 0.445589f},
			new[] {-0.087263f, -0.050381f, 0.003753f, -0.866000f, -0.500000f, -0.000000f, 0.426819f, 0.409057f},
			new[] {0.071250f, -0.071250f, 0.003753f, 0.707100f, -0.707100f, -0.000000f, 0.434158f, 0.356035f},
			new[] {0.071250f, -0.071250f, 1.004253f, 0.707100f, -0.707100f, -0.000000f, 0.499490f, 0.323467f},
			new[] {-0.097329f, -0.026079f, 0.003753f, -0.965900f, -0.258800f, -0.000000f, 0.418318f, 0.416061f},
			new[] {-0.097329f, -0.026079f, 1.004253f, -0.965900f, -0.258800f, -0.000000f, 0.489842f, 0.461919f},
			new[] {-0.100762f, 0.000000f, 1.004253f, -0.997800f, -0.065800f, -0.000000f, 0.479319f, 0.477522f},
			new[] {-0.100762f, -0.000000f, 0.003753f, -0.997800f, -0.065800f, -0.000000f, 0.407449f, 0.422464f},
			new[] {0.087837f, 0.837031f, 0.003753f, 0.866000f, 0.500000f, -0.000000f, 0.104696f, 0.353845f},
			new[] {0.087837f, 0.837031f, 1.004253f, 0.866000f, 0.500000f, -0.000000f, 0.027922f, 0.313093f}
		};

		private static readonly int[] Indices = {
			12, 13, 14,
			12, 14, 15,
			12, 15, 16,
			12, 16, 17,
			12, 17, 18,
			12, 18, 19,
			12, 19, 20,
			12, 20, 21,
			12, 21, 22,
			12, 22, 23,
			12, 23, 24,
			12, 24, 25,
			12, 25, 26,
			12, 26, 27,
			12, 27, 28,
			12, 28, 29,
			12, 29, 30,
			12, 30, 31,
			12, 31, 32,
			12, 32, 33,
			12, 33, 34,
			12, 34, 35,
			12, 35, 36,
			12, 36, 37,
			0, 1, 2,
			101, 1, 0,
			0, 2, 3,
			101, 100, 1,
			3, 2, 69,
			98, 100, 101,
			3, 69, 68,
			98, 99, 100,
			68, 69, 70,
			95, 99, 98,
			68, 70, 71,
			95, 94, 99,
			71, 70, 5,
			81, 94, 95,
			71, 5, 4,
			81, 80, 94,
			4, 5, 6,
			78, 80, 81,
			4, 6, 7,
			78, 79, 80,
			7, 6, 87,
			92, 79, 78,
			7, 87, 86,
			92, 93, 79,
			86, 87, 73,
			83, 93, 92,
			86, 73, 72,
			83, 82, 93,
			72, 73, 9,
			66, 82, 83,
			72, 9, 8,
			66, 83, 67,
			8, 9, 10,
			64, 66, 67,
			8, 10, 11,
			64, 65, 66,
			11, 10, 103,
			96, 65, 64,
			11, 103, 102,
			96, 97, 65,
			102, 103, 91,
			85, 97, 96,
			102, 91, 90,
			85, 84, 97,
			90, 91, 89,
			76, 84, 85,
			90, 89, 88,
			76, 75, 84,
			88, 89, 74,
			74, 75, 76,
			88, 74, 77,
			74, 76, 77,
			38, 39, 40,
			38, 40, 41,
			38, 41, 42,
			38, 42, 43,
			38, 43, 44,
			38, 44, 45,
			38, 45, 46,
			38, 46, 47,
			38, 47, 48,
			38, 48, 49,
			38, 49, 50,
			38, 50, 51,
			38, 51, 52,
			38, 52, 53,
			38, 53, 54,
			38, 54, 55,
			38, 55, 56,
			38, 56, 57,
			38, 57, 58,
			38, 58, 59,
			38, 59, 60,
			38, 60, 61,
			38, 61, 62,
			38, 62, 63
		};


		#endregion
	}
}
