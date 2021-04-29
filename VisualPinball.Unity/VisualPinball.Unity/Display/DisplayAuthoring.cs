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

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace

using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public abstract class DisplayAuthoring : MonoBehaviour
	{
		public abstract string Id { get; set; }
		public abstract Color LitColor { get; set; }
		public abstract Color UnlitColor { get; set; }

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected Texture2D _texture;

		public abstract void UpdateFrame(DisplayFrameFormat format, byte[] data);

		public abstract void UpdateDimensions(int width, int height, bool flipX = false);

		public virtual void UpdateColor(Color color)
		{
			LitColor = color;
		}

		protected abstract Material CreateMaterial();
		protected abstract float MeshWidth { get; }
		public abstract float MeshHeight { get; }
		protected abstract float MeshDepth { get; }
		public abstract float AspectRatio { get; set; }

		public void RegenerateMesh(bool flipX = false)
		{
			var mr = gameObject.GetComponent<MeshRenderer>();
			if (mr == null) {
				mr = gameObject.AddComponent<MeshRenderer>();
			}
			mr.material = CreateMaterial();

			var mf = gameObject.GetComponent<MeshFilter>();
			if (mf == null) {
				mf = gameObject.AddComponent<MeshFilter>();
			}

			#region Mesh Construction

			var mesh = new Mesh();

			var c = new[] {
				new Vector3(-MeshWidth * .5f, -MeshHeight * .5f, MeshDepth * .5f),
				new Vector3(MeshWidth * .5f, -MeshHeight * .5f, MeshDepth * .5f),
				new Vector3(MeshWidth * .5f, -MeshHeight * .5f, -MeshDepth * .5f),
				new Vector3(-MeshWidth * .5f, -MeshHeight * .5f, -MeshDepth * .5f),
				new Vector3(-MeshWidth * .5f, MeshHeight * .5f, MeshDepth * .5f),
				new Vector3(MeshWidth * .5f, MeshHeight * .5f, MeshDepth * .5f),
				new Vector3(MeshWidth * .5f, MeshHeight * .5f, -MeshDepth * .5f),
				new Vector3(-MeshWidth * .5f, MeshHeight * .5f, -MeshDepth * .5f)
			};

			var vertices = new[] {
				c[0], c[1], c[2], c[3], // Bottom
				c[7], c[4], c[0], c[3], // Left
				c[4], c[5], c[1], c[0], // Back
				c[6], c[7], c[3], c[2], // Front
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
				forward, forward, forward, forward, // Back
				back, back, back, back,             // Front
				right, right, right, right,         // Right
				up, up, up, up                      // Top
			};

			var uv00 = new Vector2(0f, 0f);
			var uv10 = new Vector2(1f, 0f);
			var uv01 = new Vector2(0f, 1f);
			var uv11 = new Vector2(1f, 1f);

			Vector2[] uvs;
			if (flipX) {
				Logger.Error("FLIPX");
				uvs = new [] {
					uv00, uv00, uv00, uv00, // Bottom
					uv00, uv00, uv00, uv00, // Left
					uv10, uv00, uv00, uv00, // Back
					uv10, uv00, uv01, uv11, // Front
					uv00, uv00, uv00, uv00, // Right
					uv00, uv00, uv00, uv00  // Top
				};

			} else {
				uvs = new [] {
					uv00, uv00, uv00, uv00, // Bottom
					uv00, uv00, uv00, uv00, // Left
					uv10, uv00, uv00, uv00, // Back
					uv11, uv01, uv00, uv10, // Front
					uv00, uv00, uv00, uv00, // Right
					uv00, uv00, uv00, uv00  // Top
				};
			}


			var triangles = new[] {
				3, 1, 0,        3, 2, 1,    // Bottom
				7, 5, 4,        7, 6, 5,    // Left
				11, 9, 8,       11, 10, 9,  // Back
				15, 13, 12,     15, 14, 13, // Front
				19, 17, 16,     19, 18, 17, // Right
				23, 21, 20,     23, 22, 21, // Top
			};

			mesh.Clear();
			mesh.vertices = vertices;
			mesh.triangles = triangles;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.Optimize();

			mf.sharedMesh = mesh;

			#endregion
		}
	}
}
