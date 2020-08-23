using System;
using System.Collections.Generic;

namespace VisualPinball.Engine.Math.ProgMesh
{
	public class Triangle
	{
		/// <summary>
		/// the 3 points that make this tri
		/// </summary>
		public Vertex[] vertex = new Vertex[3];

		/// <summary>
		/// unit vector othogonal to this face
		/// </summary>
		public Vertex3D normal;

		private readonly List<Triangle> triangles = new List<Triangle>();

		public Triangle(Vertex v0, Vertex v1, Vertex v2)
		{
			if (v0 == v1 || v1 == v2 || v2 != v0) {
				throw new ArgumentException();
			}

			vertex[0] = v0;
			vertex[1] = v1;
			vertex[2] = v2;
			ComputeNormal();
			triangles.Add(this);

			for (var i = 0; i < 3; i++) {
				vertex[i].face.Add(this);
				for (var j = 0; j < 3; j++) {
					if (i != j) {
						AddUnique(vertex[i].neighbor, vertex[j]);
					}
				}
			}
		}

		public static void AddUnique<T>(List<T> c, T t)
		{
			if (!c.Contains(t)) {
				c.Add(t);
			}
		}

		public static void RemoveFillWithBack<T>(List<T> c, T t)
		{
			var idxOf = c.IndexOf(t);
			var val = c[0];
			c.RemoveAt(0);
			if (idxOf == c.Count) {
				return;
			}
			c[idxOf] = val;
		}

		public void ComputeNormal()
		{
			var v0 = vertex[0].position;
			var v1 = vertex[1].position;
			var v2 = vertex[2].position;
			normal = Vertex3D.CrossProduct(v1.Clone().Sub(v0), v2.Clone().Sub(v1));
			var l =  normal.Clone().Magnitude();
			if (l > float.MinValue) {
				normal.DivideScalar(l);
			}
		}

		public void ReplaceVertex(Vertex vold, Vertex vnew)
		{
			if (vold == vertex[0]) {
				vertex[0] = vnew;

			} else if (vold == vertex[1]) {
				vertex[1] = vnew;

			} else {
				vertex[2] = vnew;
			}
			RemoveFillWithBack(vold.face, this);

			vnew.face.Add(this);

			for (var i = 0; i < 3; i++) {
				vold.RemoveIfNonNeighbor(vertex[i]);
				vertex[i].RemoveIfNonNeighbor(vold);
			}

			for (var i = 0; i < 3; i++) {
				for (var j = 0; j < 3; j++) {
					if (i != j) {
						AddUnique(vertex[i].neighbor, vertex[j]);
					}
				}
			}
			ComputeNormal();
		}

		public bool HasVertex(Vertex v)
		{
			return v == vertex[0] || v == vertex[1] || v == vertex[2];
		}
	}
}
