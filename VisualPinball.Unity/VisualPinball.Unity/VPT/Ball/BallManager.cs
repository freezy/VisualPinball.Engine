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

using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	/// <summary>
	/// A singleton class that handles ball creation and destruction.
	/// </summary>
	internal class BallManager
	{
		public static int NumBallsCreated { get; private set; }

		private readonly Table _table;
		private readonly Matrix4x4 _ltw;

		private static EntityManager EntityManager => World.DefaultGameObjectInjectionWorld.EntityManager;

		private static BallManager _instance;
		private static Mesh _unitySphereMesh; // used to cache ball mesh from GameObject

		public static BallManager Instance(Table table, Matrix4x4 ltw) => _instance ?? (_instance = new BallManager(table, ltw));

		public BallManager(Table table, Matrix4x4 ltw)
		{
			_table = table;
			_ltw = ltw;
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

			var worldPos = _ltw.MultiplyPoint(localPos);
			var scale3 = new Vector3(
				_ltw.GetColumn(0).magnitude,
				_ltw.GetColumn(1).magnitude,
				_ltw.GetColumn(2).magnitude
			);
			var scale = (scale3.x + scale3.y + scale3.z) / 3.0f; // scale is only scale (without radiusfloat now, not vector.
			var material = BallMaterial.CreateMaterial();
			var mesh = GetSphereMesh();

			// create ball entity
			EngineProvider<IPhysicsEngine>
				.Get()
				.BallCreate(mesh, material, worldPos, localPos, localVel, scale, mass, radius, in kickerRef);
		}

		public void CreateEntity(Mesh mesh, Material material, in float3 worldPos, in float3 localPos,
			in float3 localVel, in float scale, in float mass, in float radius, in Entity kickerEntity)
		{
			var world = World.DefaultGameObjectInjectionWorld;
			var ecbs = world.GetOrCreateSystem<CreateBallEntityCommandBufferSystem>();
			var ecb = ecbs.CreateCommandBuffer();

			var archetype = EntityManager.CreateArchetype(
				typeof(Translation),
				typeof(Scale),
				typeof(Rotation),
				typeof(RenderMesh),
				typeof(LocalToWorld),
				typeof(RenderBounds),
				typeof(BallData),
				typeof(CollisionEventData),
				typeof(OverlappingStaticColliderBufferElement),
				typeof(OverlappingDynamicBufferElement),
				typeof(ContactBufferElement),
				typeof(BallInsideOfBufferElement),
				typeof(BallLastPositionsBufferElement)
			);
			var entity = ecb.CreateEntity(archetype);

			ecb.SetSharedComponent(entity, new RenderMesh {
				mesh = mesh,
				material = material
			});

			ecb.SetComponent(entity, new RenderBounds {
				Value = mesh.bounds.ToAABB()
			});

			ecb.SetComponent(entity, new Translation {
				Value = worldPos
			});

			ecb.SetComponent(entity, new Scale {
				Value = scale
			});

			ecb.SetComponent(entity, new BallData {
				Id = NumBallsCreated++,
				IsFrozen = false,
				Position = localPos,
				Radius = radius,
				Mass = mass,
				Velocity = localVel,
				Orientation = float3x3.identity,
				RingCounterOldPos = 0,
				AngularMomentum = float3.zero
			});

			ecb.SetComponent(entity, new CollisionEventData {
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
		}

		public static void DestroyEntity(Entity ballEntity)
		{
			World.DefaultGameObjectInjectionWorld
				.GetOrCreateSystem<CreateBallEntityCommandBufferSystem>()
				.CreateCommandBuffer()
				.DestroyEntity(ballEntity);
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

		/// <summary>
		/// Ball material creator instance for the current graphics pipeline
		/// </summary>
		public static IBallMaterial BallMaterial => CreateBallMaterial();

		/// <summary>
		/// Create a material converter depending on the graphics pipeline
		/// </summary>
		/// <returns></returns>
		private static IBallMaterial CreateBallMaterial()
		{
			if (GraphicsSettings.renderPipelineAsset != null) {
				if (GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("UniversalRenderPipelineAsset")) {
					return new UrpBallMaterial();
				}

				if (GraphicsSettings.renderPipelineAsset.GetType().Name.Contains("HDRenderPipelineAsset")) {
					return new HdrpBallMaterial();
				}
			}

			return new StandardBallMaterial();
		}

		#endregion
	}
}
