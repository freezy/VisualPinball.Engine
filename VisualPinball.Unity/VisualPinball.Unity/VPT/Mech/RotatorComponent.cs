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

using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Rotator")]
	public class RotatorComponent : MonoBehaviour
	{
		#region Data

		public IRotatableComponent Target { get => _target as IRotatableComponent; set => _target = value as MonoBehaviour; }
		[SerializeField]
		[TypeRestriction(typeof(IRotatableComponent), PickerLabel = "Rotatable Objects")]
		[Tooltip("The target that will rotate.")]
		public MonoBehaviour _target;

		[SerializeField]
		[TypeRestriction(typeof(IRotatableComponent), PickerLabel = "Rotatable Objects")]
		[Tooltip("Other objects at will rotate around the target.")]
		public MonoBehaviour[] _rotateWith;
		public IRotatableComponent[] RotateWith {
			get => _rotateWith.OfType<IRotatableComponent>().ToArray();
			set => _rotateWith = value.OfType<MonoBehaviour>().ToArray();
		}

		#endregion

		#region Access

		internal IEnumerable<KickerComponent> Kickers => _rotateWith.OfType<KickerComponent>();

		#endregion

		#region Runtime

		private Player _player;
		private KickerApi[] _kickers;
		private (KickerApi kicker, float distance, float angle, Entity ballEntity)[] _ballEntities;

		private Dictionary<IRotatableComponent, (float, float)> _rotatingObjectDistances = new();
		private static EntityManager EntityManager => World.DefaultGameObjectInjectionWorld.EntityManager;

		private void Awake()
		{
			_player = GetComponentInParent<Player>();

			var pos = Target.RotatedPosition;
			_rotatingObjectDistances = RotateWith.ToDictionary(
				r => r,
				r => (
					math.distance(pos, r.RotatedPosition),
					math.sign(pos.x - r.RotatedPosition.x) * Vector2.Angle(r.RotatedPosition - pos, new float2(0f, -1f))
				)
			);
		}

		private void Start()
		{
			_kickers = Kickers
				.Select(k => _player.TableApi.Kicker(k))
				.ToArray();
		}

		public void StartRotating()
		{
			var pos = Target.RotatedPosition;
			_ballEntities = _kickers.Where(k => k.HasBall()).Select(k => (
				k,
				math.distance(pos, k.Position.xy),
				math.sign(pos.x - k.Position.x) * Vector2.Angle(k.Position.xy - pos, new float2(0f, -1f)),
				k.BallEntity)
			).ToArray();
		}

		public void UpdateRotation(float angleDeg)
		{
			// rotate target
			Target.RotateZ = -angleDeg;
			var pos = Target.RotatedPosition;

			// rotate objects
			foreach (var obj in _rotatingObjectDistances.Keys) {
				var (distance, angle) = _rotatingObjectDistances[obj];
				obj.RotateZ = -angleDeg;
				obj.RotatedPosition = new float2(
					pos.x -distance * math.sin(math.radians(angleDeg + angle)),
					pos.y -distance * math.cos(math.radians(angleDeg + angle))
				);
			}

			// rotate ball(s) in kicker(s)
			foreach (var (kicker, distance, angle, ballEntity) in _ballEntities) {
				if (!kicker.HasBall()) {
					return;
				}
				var ballData = EntityManager.GetComponentData<BallData>(ballEntity);
				ballData.Position = new float3(
					pos.x -distance * math.sin(math.radians(angleDeg + angle)),
					pos.y -distance * math.cos(math.radians(angleDeg + angle)),
					ballData.Position.z
				);
				ballData.Velocity = float3.zero;
				ballData.AngularMomentum = float3.zero;
				ballData.AngularVelocity = float3.zero;

				EntityManager.SetComponentData(ballEntity, ballData);
			}
		}

		#endregion
	}

}
