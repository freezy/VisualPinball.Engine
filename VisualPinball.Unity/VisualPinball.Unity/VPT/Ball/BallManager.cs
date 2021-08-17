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

using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Table;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A singleton class that handles ball creation and destruction.
	/// </summary>
	[Serializable]
	public class BallManager
	{
		public int NumBallsCreated { get; private set; }
		public int NumBalls { get; private set; }

		private readonly Table _table;
		private readonly GameObject _playfield;
		private readonly Player _player;

		private static EntityManager EntityManager => World.DefaultGameObjectInjectionWorld.EntityManager;

		private static Mesh _unitySphereMesh; // used to cache ball mesh from GameObject

		public BallManager(Table table, Player player)
		{
			_table = table;
			_player = player;
			_playfield = player.Playfield;
		}

		public void CreateBall(IBallCreationPosition ballCreator, float radius = 25f, float mass = 1f)
		{
			CreateBall(ballCreator, radius, mass, Entity.Null);
		}

		public void CreateBall(IBallCreationPosition ballCreator, float radius, float mass, in Entity kickerRef)
		{
			var localPos = ballCreator.GetBallCreationPosition(_table).ToUnityFloat3();
			var localVel = ballCreator.GetBallCreationVelocity(_table).ToUnityFloat3();
			localPos.z += radius;

			var ltw = _playfield.transform.localToWorldMatrix;
			var worldPos = ltw.MultiplyPoint(localPos);
			var scale3 = new Vector3(
				ltw.GetColumn(0).magnitude,
				ltw.GetColumn(1).magnitude,
				ltw.GetColumn(2).magnitude
			);
			var scale = (scale3.x + scale3.y + scale3.z) / 3.0f; // scale is only scale (without radiusfloat now, not vector.

			var ballId = NumBallsCreated++;
			var ballPrefab = RenderPipeline.Current.BallConverter.CreateDefaultBall();
			var ballGo = Object.Instantiate(ballPrefab, _playfield.transform);
			ballGo.name = $"Ball{ballId}";
			ballGo.transform.localScale = new Vector3(radius, radius, radius) * 2f;
			ballGo.transform.localPosition = localPos;

			// create ball entity
			EngineProvider<IPhysicsEngine>
				.Get()
				.BallCreate(ballGo, ballId, worldPos, localPos, localVel, scale, mass, radius, in kickerRef);
		}

		public void CreateEntity(GameObject ballGo, int id, in float3 worldPos, in float3 localPos, in float3 localVel, in float scale,
			in float mass, in float radius, in Entity kickerEntity)
		{
			// Efficiently instantiate a bunch of entities from the already converted entity prefab
			var entity = EntityManager.CreateEntity(
				typeof(OverlappingStaticColliderBufferElement),
				typeof(OverlappingDynamicBufferElement),
				typeof(BallInsideOfBufferElement),
				typeof(BallLastPositionsBufferElement),
				typeof(BallData),
				typeof(CollisionEventData)
			);

			_player.Balls[entity] = ballGo;

			var world = World.DefaultGameObjectInjectionWorld;
			var ecbs = world.GetOrCreateSystem<CreateBallEntityCommandBufferSystem>();
			var ecb = ecbs.CreateCommandBuffer();

			ecb.AddBuffer<OverlappingStaticColliderBufferElement>(entity);
			ecb.AddBuffer<OverlappingDynamicBufferElement>(entity);
			ecb.AddBuffer<BallInsideOfBufferElement>(entity);
			ecb.AddBuffer<BallLastPositionsBufferElement>(entity);

			ecb.AddComponent(entity, new BallData {
				Id = id,
				IsFrozen = false,
				Position = localPos,
				Radius = radius,
				Mass = mass,
				Velocity = localVel,
				Orientation = float3x3.identity,
				RingCounterOldPos = 0,
				AngularMomentum = float3.zero
			});

			ecb.AddComponent(entity, new CollisionEventData {
				HitTime = -1,
				HitDistance = 0,
				HitFlag = false,
				IsContact = false,
				HitNormal = new float3(0, 0, 0),
			});

			var lastBallPostBuffer = ecb.AddBuffer<BallLastPositionsBufferElement>(entity);
			for (var i = 0; i < BallRingCounterSystem.MaxBallTrailPos; i++) {
				lastBallPostBuffer.Add(new BallLastPositionsBufferElement
					{ Value = new float3(float.MaxValue, float.MaxValue, float.MaxValue) }
				);
			}

			// handle inside-kicker creation
			if (kickerEntity != Entity.Null) {
				var kickerData = EntityManager.GetComponentData<KickerStaticData>(kickerEntity);
				if (!kickerData.FallThrough) {
					var kickerCollData = EntityManager.GetComponentData<KickerCollisionData>(kickerEntity);
					var inside = ecb.AddBuffer<BallInsideOfBufferElement>(entity);
					BallData.SetInsideOf(ref inside, kickerEntity);
					kickerCollData.BallEntity = entity;
					kickerCollData.LastCapturedBallEntity = entity;
					ecb.SetComponent(kickerEntity, kickerCollData);
				}
			}

			NumBalls++;
		}

		public void DestroyEntity(Entity ballEntity)
		{
			// destroy game object
			Object.DestroyImmediate(_player.Balls[ballEntity]);
			_player.Balls.Remove(ballEntity);

			// destroy entity
			World.DefaultGameObjectInjectionWorld
				.GetOrCreateSystem<CreateBallEntityCommandBufferSystem>()
				.CreateCommandBuffer()
				.DestroyEntity(ballEntity);

			NumBalls--;
		}

		/// <summary>
		/// Dirty way to get SphereMesh from Unity.
		/// ToDo: Get Mesh from our resources
		/// </summary>
		/// <returns>Sphere Mesh</returns>
		private static Mesh GetSphereMesh()
		{
			if (!_unitySphereMesh)
			{
				var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				_unitySphereMesh = go.GetComponent<MeshFilter>().sharedMesh;
				Object.Destroy(go);
			}

			return _unitySphereMesh;
		}

		#region Material

		#endregion
	}
}
