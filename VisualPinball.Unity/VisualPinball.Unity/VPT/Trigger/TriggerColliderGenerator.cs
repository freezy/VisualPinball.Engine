// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	public class TriggerColliderGenerator
	{
		private readonly TriggerApi _api;
		private readonly TriggerComponent _component;
		private readonly TriggerMeshComponent _meshComponent;
		private readonly TriggerColliderComponent _colliderComponent;

		private bool IsRound => _meshComponent && _meshComponent.Shape is TriggerShape.TriggerStar or TriggerShape.TriggerButton;

		public TriggerColliderGenerator(TriggerApi triggerApi, TriggerComponent component, TriggerColliderComponent colliderComponent, TriggerMeshComponent meshComponent)
		{
			_api = triggerApi;
			_component = component;
			_meshComponent = meshComponent;
			_colliderComponent = colliderComponent;
		}

		internal void GenerateColliders(ref ColliderReference colliders)
		{
			if (IsRound) {
				GenerateRoundHitObjects(ref colliders);

			} else {
				GenerateCurvedHitObjects(ref colliders);
			}
		}

		private void GenerateRoundHitObjects(ref ColliderReference colliders)
		{
			var height = _component.PositionZ;
			colliders.Add(new CircleCollider(_component.Center, _colliderComponent.HitCircleRadius, height, height + _colliderComponent.HitHeight,
				_api.GetColliderInfo(), ColliderType.TriggerCircle));
		}

		private void GenerateCurvedHitObjects(ref ColliderReference colliders)
		{
			var height = _component.PositionZ;
			var vVertex = DragPoint.GetRgVertex<RenderVertex2D, CatmullCurve2DCatmullCurveFactory>(_component.DragPoints);

			var count = vVertex.Length;
			var rgv = new RenderVertex2D[count];
			var rgv3D = new float3[count];

			for (var i = 0; i < count; i++) {
				rgv[i] = vVertex[i];
				rgv3D[i] = new float3(rgv[i].X, rgv[i].Y, height + (float)(PhysicsConstants.PhysSkin * 2.0));
			}
			ColliderUtils.Generate3DPolyColliders(rgv3D, _api.GetColliderInfo(), ref colliders, float4x4.identity); // todo adapt

			for (var i = 0; i < count; i++) {
				var pv2 = rgv[i < count - 1 ? i + 1 : 0];
				var pv3 = rgv[i < count - 2 ? i + 2 : i + 2 - count];
				AddLineSeg(pv2.ToUnityFloat2(), pv3.ToUnityFloat2(), height, ref colliders);
			}
		}

		private void AddLineSeg(float2 pv1, float2 pv2, float height, ref ColliderReference colliders) {
			colliders.Add(new LineCollider(pv1, pv2, height, height + math.max(_colliderComponent.HitHeight - 8.0f, 0f),
				_api.GetColliderInfo(), ColliderType.TriggerLine));
		}
	}
}
