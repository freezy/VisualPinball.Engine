using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Kicker
{
	public class KickerHit : HitCircle
	{
		public readonly Vertex3D[] HitMesh;

		public KickerHit(KickerData data, float radius, float height, Table.Table table) : base(data.Center.Clone(), radius, height, height + data.HitHeight, ItemType.Kicker)
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
