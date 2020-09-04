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

namespace VisualPinball.Unity
{
	public class BallAuthoring : MonoBehaviour, IConvertGameObjectToEntity
	{
		private static int _id;
		public static int NumBallsCreated { get => _id; }

		public float3 Position;
		public float3 Velocity;
		public float Radius;
		public float Mass;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			dstManager.AddComponentData(entity, new BallData {
				Id = _id++,
				IsFrozen = false,
				Position = Position,
				Radius = Radius,
				Mass = Mass,
				Velocity = Velocity,
				Orientation = float3x3.identity
			});
			dstManager.AddComponentData(entity, new CollisionEventData {
				HitTime = -1,
				HitDistance = 0,
				HitFlag = false,
				IsContact = false,
				HitNormal = new float3(0, 0, 0),
			});
			dstManager.AddBuffer<OverlappingStaticColliderBufferElement>(entity);
			dstManager.AddBuffer<OverlappingDynamicBufferElement>(entity);
			dstManager.AddBuffer<ContactBufferElement>(entity);
			dstManager.AddBuffer<BallInsideOfBufferElement>(entity);
		}

		public static void CreateEntity(EntityManager entityManager, Mesh mesh, Material material,
			float3 worldPos, float scale, float3 localPos, float3 velocity, float radius, float mass)
		{
			var ecbs = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<CreateBallEntityCommandBufferSystem>();
			var ecb = ecbs.CreateCommandBuffer();

			var archetype = entityManager.CreateArchetype(
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
				Id = _id++,
				IsFrozen = false,
				Position = localPos,
				Radius = radius,
				Mass = mass,
				Velocity = velocity,
				Orientation = float3x3.identity,
				RingCounterOldPos = 0
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
		}
	}
}
