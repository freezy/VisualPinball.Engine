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
		private readonly float4x4 _matrix;

		private bool IsRound => _meshComponent && _meshComponent.Shape is TriggerShape.TriggerStar or TriggerShape.TriggerButton;

		public TriggerColliderGenerator(TriggerApi triggerApi, TriggerComponent component, TriggerColliderComponent colliderComponent, TriggerMeshComponent meshComponent, float4x4 matrix)
		{
			_api = triggerApi;
			_component = component;
			_meshComponent = meshComponent;
			_colliderComponent = colliderComponent;
			_matrix = matrix;

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
			var height = _component.Position.z;
			colliders.Add(new CircleCollider(new float2(0), _colliderComponent.HitCircleRadius, 0, height + _colliderComponent.HitHeight,
				_api.GetColliderInfo(), ColliderType.TriggerCircle), _matrix);
		}

		private void GenerateCurvedHitObjects(ref ColliderReference colliders)
		{
			var vVertex = DragPoint.GetRgVertex<RenderVertex2D, CatmullCurve2DCatmullCurveFactory>(_component.DragPoints);

			var count = vVertex.Length;
			var rgv = new RenderVertex2D[count];
			var rgv3D = new float3[count];

			// top surface
			for (var i = 0; i < count; i++) {
				rgv[i] = vVertex[i];
				rgv3D[i] = new float3(rgv[i].X, rgv[i].Y, _colliderComponent.HitHeight);
			}
			ColliderUtils.Generate3DPolyColliders(rgv3D, _api.GetColliderInfo(), ref colliders, _matrix);

			// walls
			for (var i = 0; i < count; i++) {
				var pv2 = rgv[i < count - 1 ? i + 1 : 0];
				var pv3 = rgv[i < count - 2 ? i + 2 : i + 2 - count];
				colliders.Add(new LineCollider(
					pv2.ToUnityFloat2(),
					pv3.ToUnityFloat2(),
					0,
					_colliderComponent.HitHeight,
					_api.GetColliderInfo()), _matrix);
			}
		}
	}
}
