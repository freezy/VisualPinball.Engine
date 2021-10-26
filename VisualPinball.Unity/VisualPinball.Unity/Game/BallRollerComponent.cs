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

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VisualPinball.Unity
{
	public class BallRollerComponent : MonoBehaviour
	{
		private PlayfieldComponent _playfield;
		private Matrix4x4 _ltw;
		private Matrix4x4 _wtl;

		private Plane _playfieldPlane;

		private EntityManager _entityManager;
		private Entity _ballEntity = Entity.Null;
		private EntityQuery _ballEntityQuery;

		private void Awake()
		{
			_playfield = GetComponentInChildren<PlayfieldComponent>();

			var playfieldTransform = _playfield.transform;
			_ltw = playfieldTransform.localToWorldMatrix;
			_wtl = playfieldTransform.worldToLocalMatrix;

			var z = _playfield.PlayfieldHeight;
			var p1 = _ltw.MultiplyPoint(new Vector3(-100f, 100f, z));
			var p2 = _ltw.MultiplyPoint(new Vector3(100f, 100f, z));
			var p3 = _ltw.MultiplyPoint(new Vector3(100f, -100f, z));
			_playfieldPlane.Set3Points(p1, p2, p3);

			_entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			_ballEntityQuery = _entityManager.CreateEntityQuery(typeof(BallData));
		}

		private void Update()
		{
			if (Camera.main == null || _playfield == null) {
				return;
			}

			// find nearest ball
			if (Mouse.current.middleButton.wasPressedThisFrame) {
				if (GetCursorPositionOnPlayfield(out var mousePosition)) {
					var ballEntities = _ballEntityQuery.ToEntityArray(Allocator.Temp);
					var nearestDistance = float.PositiveInfinity;
					BallData nearestBall = default;
					var ballFound = false;
					foreach (var ballEntity in ballEntities) {
						var ballData = _entityManager.GetComponentData<BallData>(ballEntity);
						if (ballData.IsFrozen) {
							continue;
						}
						var distance = math.distance(mousePosition, ballData.Position.xy);
						if (distance < nearestDistance) {
							nearestDistance = distance;
							nearestBall = ballData;
							ballFound = true;
							_ballEntity = ballEntity;
						}
					}

					if (ballFound) {
						UpdateBall(ref nearestBall, mousePosition);
					}
				}

			} else if (Mouse.current.middleButton.isPressed && _ballEntity != Entity.Null) {
				if (GetCursorPositionOnPlayfield(out var mousePosition)) {
					var ballData = _entityManager.GetComponentData<BallData>(_ballEntity);
					UpdateBall(ref ballData, mousePosition);
				}
			}

			if (Mouse.current.middleButton.wasReleasedThisFrame && _ballEntity != Entity.Null) {
				var ballData = _entityManager.GetComponentData<BallData>(_ballEntity);
				ballData.ManualControl = false;
				_entityManager.SetComponentData(_ballEntity, ballData);
				_ballEntity = Entity.Null;
			}
		}

		private void UpdateBall(ref BallData ballData, float2 position)
		{
			ballData.ManualControl = true;
			ballData.ManualPosition = position;
			_entityManager.SetComponentData(_ballEntity, ballData);
		}

		private bool GetCursorPositionOnPlayfield(out float2 position)
		{
			if (Camera.main == null) {
				position = float2.zero;
				return false;
			}

			var mouseOnScreenPos = Mouse.current.position.ReadValue();
			var ray = Camera.main.ScreenPointToRay(mouseOnScreenPos);

			if (_playfieldPlane.Raycast(ray, out var enter)) {
				var playfieldPosWorld = ray.GetPoint(enter);
				var playfieldPosLocal = _wtl.MultiplyPoint(playfieldPosWorld);

				position = new float2(playfieldPosLocal.x, playfieldPosLocal.y);

				// todo check playfield bounds
				return true;
			}
			position = float2.zero;
			return false;
		}
	}
}
