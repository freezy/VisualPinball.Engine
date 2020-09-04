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

using System;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	public class DefaultPhysicsEngine : IPhysicsEngine
	{
		public string Name => "Default VPX";

		private BallManager _ballManager;
		private EntityManager _entityManager;
		private EntityQuery _flipperDataQuery;
		private EntityQuery _ballDataQuery;

		private Matrix4x4 _worldToLocal;
		private DebugFlipperState[] _flipperStates = new DebugFlipperState[0];
		private readonly DebugFlipperSlider[] _flipperSliders = new DebugFlipperSlider[0];
		private int _nextBallIdToNotifyDebugUI;

		public void Init(TableAuthoring tableAuthoring)
		{
			_entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			_flipperDataQuery = _entityManager.CreateEntityQuery(
				ComponentType.ReadOnly<FlipperMovementData>(),
				ComponentType.ReadOnly<FlipperStaticData>(),
				ComponentType.ReadOnly<SolenoidStateData>()
			);

			_ballManager = BallManager.Instance(tableAuthoring.Table, tableAuthoring.gameObject.transform.localToWorldMatrix);
			_ballDataQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<BallData>());

			var visualPinballSimulationSystemGroup = _entityManager.World.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();
			var simulateCycleSystemGroup = _entityManager.World.GetOrCreateSystem<SimulateCycleSystemGroup>();

			visualPinballSimulationSystemGroup.Enabled = true;
			simulateCycleSystemGroup.PhysicsEngine = this; // needed for flipper status update we don't do in all engines

			_worldToLocal = tableAuthoring.gameObject.transform.worldToLocalMatrix;
		}

		public void BallCreate(Mesh mesh, Material material, in float3 worldPos, in float3 localPos,
			in float3 localVel, in float scale, in float mass, in float radius, in Entity kickerRef)
		{
			_ballManager.CreateEntity(mesh, material, in worldPos, in localPos, in localVel,
				scale * radius * 2, in mass, in radius, in kickerRef);
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

		public void PushPendingCreateBallNotifications()
		{
			if (_nextBallIdToNotifyDebugUI == BallManager.NumBallsCreated)
				return; // nothing to report

			var entities = _ballDataQuery.ToEntityArray(Allocator.TempJob);
			int numBallsToReport = BallManager.NumBallsCreated - _nextBallIdToNotifyDebugUI;
			foreach (var entity in entities)
			{
				var ballData = _entityManager.GetComponentData<BallData>(entity);
				if (ballData.Id >= _nextBallIdToNotifyDebugUI)
				{
					EngineProvider<IDebugUI>.Get().OnCreateBall(entity);
					--numBallsToReport;
				}
			}

			// error checking
			Assert.AreEqual(0, numBallsToReport);
			_nextBallIdToNotifyDebugUI = BallManager.NumBallsCreated;
		}
	}
}
