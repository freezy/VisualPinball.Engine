// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Math.Triangulator;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Engine.Physics
{
	public class Hit3DPoly : HitObject
	{
		public readonly Vertex3D[] Rgv;                                      // m_rgv
		public readonly Vertex3D Normal = new Vertex3D();                    // m_normal

		public Hit3DPoly(Vertex3D[] rgv, ItemType objType, IItem item) : base(objType, item)
		{
			Rgv = rgv;

			// Newell's method for normal computation
			for (var i = 0; i < Rgv.Length; ++i) {
				var m = i < Rgv.Length - 1 ? i + 1 : 0;
				Normal.X += (Rgv[i].Y - Rgv[m].Y) * (Rgv[i].Z + Rgv[m].Z);
				Normal.Y += (Rgv[i].Z - Rgv[m].Z) * (Rgv[i].X + Rgv[m].X);
				Normal.Z += (Rgv[i].X - Rgv[m].X) * (Rgv[i].Y + Rgv[m].Y);
			}

			var sqrLen = Normal.X * Normal.X + Normal.Y * Normal.Y + Normal.Z * Normal.Z;
			var invLen = sqrLen > 0.0f ? -1.0f / MathF.Sqrt(sqrLen) : 0.0f; // normal NOTE is flipped! Thus we need vertices in CCW order
			Normal.X *= invLen;
			Normal.Y *= invLen;
			Normal.Z *= invLen;

			Elasticity = 0.3f;
			SetFriction(0.3f);
			Scatter = 0.0f;
		}

		public IEnumerable<HitObject> ConvertToTriangles()
		{
			var inputVerts = Rgv.Select(v => new Vector2(v.X, v.Y)).ToArray();
			Triangulator.Triangulate(inputVerts, WindingOrder.CounterClockwise, out var outputVerts, out var outputIndices);

			var mesh = new Mesh(
				outputVerts.Select(v => new Vertex3DNoTex2(v.X, v.Y, Rgv[0].Z)).ToArray(),
				outputIndices
			);

			var hitObjects = PrimitiveHitGenerator.MeshToHitObjects(mesh, ObjType, Item).ToArray();
			foreach (var hitObject in hitObjects) {
				hitObject.ItemIndex = ItemIndex;
				hitObject.ItemVersion = ItemVersion;
				hitObject.Threshold = Threshold;
				hitObject.Elasticity = Elasticity;
				hitObject.ElasticityFalloff = ElasticityFalloff;
				hitObject.Friction = Friction;
				hitObject.Scatter = Scatter;
				hitObject.IsEnabled = IsEnabled;
				hitObject.FireEvents = FireEvents;
				hitObject.E = E;
			}
			return hitObjects;
		}

		public override void CalcHitBBox()
		{
			HitBBox.Left = Rgv[0].X;
			HitBBox.Right = Rgv[0].X;
			HitBBox.Top = Rgv[0].Y;
			HitBBox.Bottom = Rgv[0].Y;
			HitBBox.ZLow = Rgv[0].Z;
			HitBBox.ZHigh = Rgv[0].Z;

			for (var i = 1; i < Rgv.Length; i++) {
				HitBBox.Left = MathF.Min(Rgv[i].X, HitBBox.Left);
				HitBBox.Right = MathF.Max(Rgv[i].X, HitBBox.Right);
				HitBBox.Top = MathF.Min(Rgv[i].Y, HitBBox.Top);
				HitBBox.Bottom = MathF.Max(Rgv[i].Y, HitBBox.Bottom);
				HitBBox.ZLow = MathF.Min(Rgv[i].Z, HitBBox.ZLow);
				HitBBox.ZHigh = MathF.Max(Rgv[i].Z, HitBBox.ZHigh);
			}
		}
	}
}
