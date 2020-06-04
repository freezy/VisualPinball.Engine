using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.DebugUI;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Physics.Engine
{
	public class DefaultPhysicsEngine : IPhysicsEngine
	{
		public string Name => "Default VPX";

		private EntityManager _entityManager;
		private EntityQuery _flipperDataQuery;

		private Matrix4x4 _worldToLocal;
		private DebugFlipperState[] _flipperStates = new DebugFlipperState[0];
		private readonly DebugFlipperSlider[] _flipperSliders = new DebugFlipperSlider[0];

		public void Init(TableBehavior tableBehavior)
		{
			_entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			_flipperDataQuery = _entityManager.CreateEntityQuery(
				ComponentType.ReadOnly<FlipperMovementData>(),
				ComponentType.ReadOnly<FlipperStaticData>(),
				ComponentType.ReadOnly<SolenoidStateData>()
			);

			var visualPinballSimulationSystemGroup = _entityManager.World.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();
			var simulateCycleSystemGroup = _entityManager.World.GetOrCreateSystem<SimulateCycleSystemGroup>();

			visualPinballSimulationSystemGroup.Enabled = true;
			simulateCycleSystemGroup.PhysicsEngine = this; // needed for flipper status update we don't do in all engines

			_worldToLocal = tableBehavior.gameObject.transform.worldToLocalMatrix;
		}

		public Entity BallCreate(Mesh mesh, Material material, in float3 worldPos, in float3 localPos,
			in float3 localVel, in float scale, in float mass, in float radius)
		{
			return BallManager.CreateEntity(mesh, material, in worldPos, in localPos, in localVel,
				scale * radius * 2, in mass, in radius);
		}

		public void BallManualRoll(in Entity entity, in float3 targetWorldPosition)
		{
			// fail safe, if we get invalid entity
			if (entity == Entity.Null && entity.Index != -1) {
				return;
			}

			float3 target = _worldToLocal.MultiplyPoint(targetWorldPosition);
			var ballData = _entityManager.GetComponentData<BallData>(entity);
			ballData.Velocity = float3.zero;
			ballData.AngularVelocity = float3.zero;
			ballData.AngularMomentum = float3.zero;
			ballData.IsFrozen = false;

			var dir = (target - ballData.Position);
			var dist = math.length(dir);
			if (dist > 50) {
				dist = 50;
			}
			if (dist > 0.1f) {
				dist += 1.0f;
				ballData.Velocity = dir * dist * 0.001f;
				_entityManager.SetComponentData(entity, ballData);
			}
		}

		public void FlipperRotateToEnd(in Entity entity)
		{
			var mData = _entityManager.GetComponentData<FlipperMovementData>(entity);
			mData.EnableRotateEvent = 1;
			_entityManager.SetComponentData(entity, mData);
			_entityManager.SetComponentData(entity, new SolenoidStateData { Value = true });
		}

		public void FlipperRotateToStart(in Entity entity)
		{
			var mData = _entityManager.GetComponentData<FlipperMovementData>(entity);
			mData.EnableRotateEvent = -1;
			_entityManager.SetComponentData(entity, mData);
			_entityManager.SetComponentData(entity, new SolenoidStateData { Value = false });
		}

		public DebugFlipperState[] FlipperGetDebugStates()
		{
			return _flipperStates;
		}

		public DebugFlipperSlider[] FlipperGetDebugSliders()
		{
			return _flipperSliders;
		}

		public void SetFlipperDebugValue(DebugFlipperSliderParam param, float v)
		{
			throw new InvalidOperationException("No debug values for default engine.");
		}

		public float GetFlipperDebugValue(DebugFlipperSliderParam param)
		{
			throw new InvalidOperationException("No debug values for default engine.");
		}

		public void UpdateDebugFlipperStates()
		{
			// for each flipper
			var entities = _flipperDataQuery.ToEntityArray(Allocator.TempJob);
			if (_flipperStates.Length == 0) {
				_flipperStates = new DebugFlipperState[entities.Length];
			}
			for (var i = 0; i < entities.Length; i++) {
				var entity = entities[i];
				var movementData = _entityManager.GetComponentData<FlipperMovementData>(entity);
				var staticData = _entityManager.GetComponentData<FlipperStaticData>(entity);
				var solenoidData = _entityManager.GetComponentData<SolenoidStateData>(entity);
				_flipperStates[i] = new DebugFlipperState(
					entity,
					math.degrees(math.abs(movementData.Angle - staticData.AngleStart)),
					solenoidData.Value
				);
			}
			entities.Dispose();
		}
	}
}
