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
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity
{
	public class TriggerColliderGenerator
	{
		private readonly TriggerApi _api;
		private readonly TriggerData _data;

		private bool IsRound => _data.Shape == TriggerShape.TriggerStar || _data.Shape == TriggerShape.TriggerButton;

		public TriggerColliderGenerator(TriggerApi triggerApi)
		{
			_api = triggerApi;
			_data = triggerApi.Data;
		}

		internal void GenerateColliders(Table table, List<ICollider> colliders, ref int nextColliderId)
		{
			if (IsRound) {
				GenerateRoundHitObjects(table, colliders, ref nextColliderId);

			} else {
				GenerateCurvedHitObjects(table, colliders, ref nextColliderId);
			}
		}

		private void GenerateRoundHitObjects(Table table, ICollection<ICollider> colliders, ref int nextColliderId)
		{
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			colliders.Add(new CircleCollider(_data.Center.ToUnityFloat2(), _data.Radius, height, height + _data.HitHeight,
				_api.GetNextColliderInfo(table, ref nextColliderId), ColliderType.TriggerCircle));
		}

		private void GenerateCurvedHitObjects(Table table, List<ICollider> colliders, ref int nextColliderId)
		{
			var height = table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
			var vVertex = DragPoint.GetRgVertex<RenderVertex2D, CatmullCurve2DCatmullCurveFactory>(_data.DragPoints);

			var count = vVertex.Length;
			var rgv = new RenderVertex2D[count];
			var rgv3D = new float3[count];

			for (var i = 0; i < count; i++) {
				rgv[i] = vVertex[i];
				rgv3D[i] = new float3(rgv[i].X, rgv[i].Y, height + (float)(PhysicsConstants.PhysSkin * 2.0));
			}

			for (var i = 0; i < count; i++) {
				var pv2 = rgv[i < count - 1 ? i + 1 : 0];
				var pv3 = rgv[i < count - 2 ? i + 2 : i + 2 - count];
				AddLineSeg(pv2.ToUnityFloat2(), pv3.ToUnityFloat2(), height, table, colliders, ref nextColliderId);
			}

			GenerateTriangles(rgv3D, table, colliders, ref nextColliderId);
		}

		private void AddLineSeg(float2 pv1, float2 pv2, float height, Table table, ICollection<ICollider> colliders, ref int nextColliderId) {
			colliders.Add(new LineCollider(pv1, pv2, height, height + math.max(_data.HitHeight - 8.0f, 0f),
				_api.GetNextColliderInfo(table, ref nextColliderId), ColliderType.TriggerLine));
		}

		private void GenerateTriangles(in float3[] rgv, Table table, ICollection<ICollider> colliders, ref int nextColliderId)
		{
			var inputVerts = new float2[rgv.Length];

			// Newell's method for normal computation
			for (var i = 0; i < rgv.Length; ++i) {
				inputVerts[i] = rgv[i].xy;
			}

			// todo make triangulator use float3
			Triangulator.Triangulate(inputVerts, WindingOrder.CounterClockwise, out var outputVerts, out var outputIndices);

			var triangulatedVerts = new Vertex3DNoTex2[outputVerts.Length];
			for (var i = 0; i < outputVerts.Length; i++) {
				triangulatedVerts[i] = new Vertex3DNoTex2(outputVerts[i].x, outputVerts[i].y, rgv[0].z);
			}
			var mesh = new Mesh(triangulatedVerts, outputIndices);

			PrimitiveColliderGenerator.GenerateCollidersFromMesh(table, mesh, _api, colliders, ref nextColliderId);
		}
	}
}
