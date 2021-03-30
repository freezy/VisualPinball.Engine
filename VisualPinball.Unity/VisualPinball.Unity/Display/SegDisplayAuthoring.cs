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

using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class SegDisplayAuthoring : DisplayAuthoring
	{
		private const float Height = 0.2f;
		private const float Depth = 0.01f;

		public int Width;
		public float AspectRatio = 0.75f;

		private static readonly int NumChars = Shader.PropertyToID("_NumChars");
		private static readonly int TargetWidth = Shader.PropertyToID("_TargetWidth");
		private static readonly int TargetHeight = Shader.PropertyToID("_TargetHeight");
		private static readonly int SegmentWidthProp = Shader.PropertyToID("_SegmentWidth");
		private static readonly int SkewAngleProp = Shader.PropertyToID("_SkewAngle");
		private static readonly int OuterPaddingX = Shader.PropertyToID("_OuterPaddingX");
		private static readonly int OuterPaddingY = Shader.PropertyToID("_OuterPaddingY");
		private static readonly int InnerPaddingX = Shader.PropertyToID("_InnerPaddingX");
		private static readonly int InnerPaddingY = Shader.PropertyToID("_InnerPaddingY");
		private static readonly int ColorProp = Shader.PropertyToID("_Color");

		public Color Color {
			get {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					return mr.material.GetColor(ColorProp);
				}
				return Color.yellow;
			}
			set {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.material.SetColor(ColorProp, value);
				}
			}
		}

		public float SkewAngle {
			get {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					return mr.material.GetFloat(SkewAngleProp);
				}
				return 0;
			}
			set {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.material.SetFloat(SkewAngleProp, value);
				}
			}
		}

		public float SegmentWidth {
			get {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					return mr.material.GetFloat(SegmentWidthProp);
				}
				return 0;
			}
			set {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.material.SetFloat(SegmentWidthProp, value);
				}
			}
		}

		public float2 OuterPadding {
			get {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					return new float2(
						mr.material.GetFloat(OuterPaddingX),
						mr.material.GetFloat(OuterPaddingY)
					);
				}
				return float2.zero;
			}
			set {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.material.SetFloat(OuterPaddingX, value.x);
					mr.material.SetFloat(OuterPaddingY, value.y);
				}
			}
		}

		public float2 InnerPadding {
			get {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					return new float2(
						mr.material.GetFloat(InnerPaddingX),
						mr.material.GetFloat(InnerPaddingY)
					);
				}
				return float2.zero;
			}
			set {
				var mr = gameObject.GetComponent<MeshRenderer>();
				if (mr != null) {
					mr.material.SetFloat(InnerPaddingX, value.x);
					mr.material.SetFloat(InnerPaddingY, value.y);
				}
			}
		}

		public void RegenerateMesh()
		{
			var mr = gameObject.GetComponent<MeshRenderer>();
			if (mr == null) {
				mr = gameObject.AddComponent<MeshRenderer>();
				mr.material = new Material(Shader.Find("Visual Pinball/Alphanumeric Shader"));
			}
			var mf = gameObject.GetComponent<MeshFilter>();
			if (mf == null) {
				gameObject.AddComponent<MeshFilter>();
			}

			var length = Width * Height * AspectRatio;

			mr.material.SetFloat(NumChars, Width);
			mr.material.SetFloat(TargetWidth, length * 100);
			mr.material.SetFloat(TargetHeight, Height * 100);

			#region Mesh Construction

			var mesh = mf.mesh;

			var c = new[] {
				new Vector3(-length * .5f, -Height * .5f, Depth * .5f),
				new Vector3(length * .5f, -Height * .5f, Depth * .5f),
				new Vector3(length * .5f, -Height * .5f, -Depth * .5f),
				new Vector3(-length * .5f, -Height * .5f, -Depth * .5f),
				new Vector3(-length * .5f, Height * .5f, Depth * .5f),
				new Vector3(length * .5f, Height * .5f, Depth * .5f),
				new Vector3(length * .5f, Height * .5f, -Depth * .5f),
				new Vector3(-length * .5f, Height * .5f, -Depth * .5f)
			};

			var vertices = new[] {
				c[0], c[1], c[2], c[3], // Bottom
				c[7], c[4], c[0], c[3], // Left
				c[4], c[5], c[1], c[0], // Front
				c[6], c[7], c[3], c[2], // Back
				c[5], c[6], c[2], c[1], // Right
				c[7], c[6], c[5], c[4]  // Top
			};

			var up = Vector3.up;
			var down = Vector3.down;
			var forward = Vector3.forward;
			var back = Vector3.back;
			var left = Vector3.left;
			var right = Vector3.right;

			var normals = new [] {
				down, down, down, down,             // Bottom
				left, left, left, left,             // Left
				forward, forward, forward, forward, // Front
				back, back, back, back,             // Back
				right, right, right, right,         // Right
				up, up, up, up                      // Top
			};

			var uv00 = new Vector2(0f, 0f);
			var uv10 = new Vector2(1f, 0f);
			var uv01 = new Vector2(0f, 1f);
			var uv11 = new Vector2(1f, 1f);

			var uvs = new [] {
				uv00, uv00, uv00, uv00, // Bottom
				uv00, uv00, uv00, uv00, // Left
				uv10, uv00, uv01, uv11, // Front
				uv10, uv00, uv01, uv11, // Back
				uv00, uv00, uv00, uv00, // Right
				uv00, uv00, uv00, uv00  // Top
			};

			var triangles = new[] {
				3, 1, 0,        3, 2, 1,    // Bottom
				7, 5, 4,        7, 6, 5,    // Left
				11, 9, 8,       11, 10, 9,  // Front
				15, 13, 12,     15, 14, 13, // Back
				19, 17, 16,     19, 18, 17, // Right
				23, 21, 20,     23, 22, 21, // Top
			};

			mesh.Clear();
			mesh.vertices = vertices;
			mesh.triangles = triangles;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.Optimize();

			#endregion
		}
	}
}
