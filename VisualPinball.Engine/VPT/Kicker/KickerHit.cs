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

using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Kicker
{
	public class KickerHit : HitCircle
	{
		public readonly Vertex3D[] HitMesh;

		public KickerHit(KickerData data, float radius, float height, Table.Table table, IItem item)
			: base(data.Center.Clone(), radius, height, height + data.HitHeight, ItemType.Kicker, item)
		{
			HitMesh = new Vertex3D[KickerHitMesh.Vertices.Length];
			if (!data.LegacyMode) {
				var rad = Radius * 0.8f;
				for (var t = 0; t < KickerHitMesh.Vertices.Length; t++) {

					// find the right normal by calculating the distance from current ball position to vertex of the kicker mesh
					var vPos = new Vertex3D(KickerHitMesh.Vertices[t].X, KickerHitMesh.Vertices[t].Y, KickerHitMesh.Vertices[t].Z);
					vPos.X = vPos.X * rad + data.Center.X;
					vPos.Y = vPos.Y * rad + data.Center.Y;
					vPos.Z = vPos.Z * rad * table.GetScaleZ() + height;
					HitMesh[t] = vPos;
				}
			}
			IsEnabled = data.IsEnabled;
		}
	}
}
