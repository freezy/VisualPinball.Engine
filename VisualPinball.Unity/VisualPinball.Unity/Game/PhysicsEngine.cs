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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NativeTrees;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Collections;
using AABB = NativeTrees.AABB;
using Debug = UnityEngine.Debug;

namespace VisualPinball.Unity
{
	public class PhysicsEngine : MonoBehaviour
	{
		#region Configuration

		[Tooltip("Gravity constant, in VPX units.")]
		public float GravityStrength = PhysicsConstants.GravityConst * PhysicsConstants.DefaultTableGravity;

		#endregion

		#region States

		[NonSerialized] private AABB _playfieldBounds;
		[NonSerialized] private InsideOfs _insideOfs;
		[NonSerialized] private NativeOctree<int> _octree;
		[NonSerialized] private NativeColliders _colliders;
		[NonSerialized] private NativeArray<PhysicsEnv> _physicsEnv = new(1, Allocator.Persistent);
		[NonSerialized] private NativeQueue<EventData> _eventQueue = new(Allocator.Persistent);

		[NonSerialized] private NativeParallelHashMap<int, BallState> _ballStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, BumperState> _bumperStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, FlipperState> _flipperStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, GateState> _gateStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, DropTargetState> _dropTargetStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, HitTargetState> _hitTargetStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, KickerState> _kickerStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, PlungerState> _plungerStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, SpinnerState> _spinnerStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, SurfaceState> _surfaceStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, TriggerState> _triggerStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashSet<int> _disabledCollisionItems = new(0, Allocator.Persistent);
		[NonSerialized] private bool _swapBallCollisionHandling;

		[NonSerialized] private NativeParallelHashMap<int, float4x4> _itemTransforms = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, NativeColliderIds> _colliderLookup = new(0, Allocator.Persistent);

		#endregion

		#region Transforms

		[NonSerialized] private readonly Dictionary<int, Transform> _transforms = new();
		[NonSerialized] private readonly Dictionary<int, SkinnedMeshRenderer[]> _skinnedMeshRenderers = new();

		#endregion

		[NonSerialized] private readonly Queue<InputAction> _inputActions = new();
		[NonSerialized] private readonly List<ScheduledAction> _scheduledActions = new();

		[NonSerialized] private Player _player;

		private static ulong NowUsec => (ulong)(Time.timeAsDouble * 1000000);

		#region API

		public void ScheduleAction(int timeoutMs, Action action) => ScheduleAction((uint)timeoutMs, action);
		public void ScheduleAction(uint timeoutMs, Action action)
		{
			lock (_scheduledActions) {
				_scheduledActions.Add(new ScheduledAction(_physicsEnv[0].CurPhysicsFrameTime + (ulong)timeoutMs * 1000, action));
			}
		}

		internal delegate void InputAction(ref PhysicsState state);

		internal ref NativeParallelHashMap<int, BallState> Balls => ref _ballStates;
		internal ref InsideOfs InsideOfs => ref _insideOfs;
		internal NativeQueue<EventData>.ParallelWriter EventQueue => _eventQueue.AsParallelWriter();

		internal void Schedule(InputAction action) => _inputActions.Enqueue(action);
		internal ref BallState BallState(int ballId) => ref _ballStates.GetValueByRef(ballId);
		internal ref BumperState BumperState(int itemId) => ref _bumperStates.GetValueByRef(itemId);
		internal ref FlipperState FlipperState(int itemId) => ref _flipperStates.GetValueByRef(itemId);
		internal ref GateState GateState(int itemId) => ref _gateStates.GetValueByRef(itemId);
		internal ref DropTargetState DropTargetState(int itemId) => ref _dropTargetStates.GetValueByRef(itemId);
		internal ref HitTargetState HitTargetState(int itemId) => ref _hitTargetStates.GetValueByRef(itemId);
		internal ref KickerState KickerState(int itemId) => ref _kickerStates.GetValueByRef(itemId);
		internal ref PlungerState PlungerState(int itemId) => ref _plungerStates.GetValueByRef(itemId);
		internal ref SpinnerState SpinnerState(int itemId) => ref _spinnerStates.GetValueByRef(itemId);
		internal ref SurfaceState SurfaceState(int itemId) => ref _surfaceStates.GetValueByRef(itemId);
		internal ref TriggerState TriggerState(int itemId) => ref _triggerStates.GetValueByRef(itemId);
		internal void SetBallInsideOf(int ballId, int itemId) => _insideOfs.SetInsideOf(itemId, ballId);
		internal uint TimeMsec => _physicsEnv[0].TimeMsec;
		internal void Register<T>(T item) where T : MonoBehaviour
		{
			var go = item.gameObject;
			var itemId = go.GetInstanceID();
			_transforms.TryAdd(itemId, go.transform);

			switch (item) {
				case BallComponent c:
					if (!_ballStates.ContainsKey(itemId)) {
						_ballStates[itemId] = c.CreateState();
					}
					break;
				case BumperComponent c: _bumperStates[itemId] = c.CreateState(); break;
				case FlipperComponent c: _flipperStates[itemId] = c.CreateState(); break;
				case GateComponent c: _gateStates[itemId] = c.CreateState(); break;
				case DropTargetComponent c: _dropTargetStates[itemId] = c.CreateState(); break;
				case HitTargetComponent c: _hitTargetStates[itemId] = c.CreateState(); break;
				case KickerComponent c: _kickerStates[itemId] = c.CreateState(); break;
				case PlungerComponent c:
					_plungerStates[itemId] = c.CreateState();
					_skinnedMeshRenderers[itemId] = c.GetComponentsInChildren<SkinnedMeshRenderer>();
					break;
				case SpinnerComponent c: _spinnerStates[itemId] = c.CreateState(); break;
				case SurfaceComponent c: _surfaceStates[itemId] = c.CreateState(); break;
				case TriggerComponent c: _triggerStates[itemId] = c.CreateState(); break;
			}
		}

		internal Transform UnregisterBall(int ballId)
		{
			var transform = _transforms[ballId];
			_transforms.Remove(ballId);
			_ballStates.Remove(ballId);
			return transform;
		}

		internal void EnableCollider(int itemId)
		{
			if (_disabledCollisionItems.Contains(itemId)) {
				_disabledCollisionItems.Remove(itemId);
			}
		}
		internal void DisableCollider(int itemId)
		{
			if (!_disabledCollisionItems.Contains(itemId)) {
				_disabledCollisionItems.Add(itemId);
			}
		}

		#endregion

		#region Event Functions

		private void Awake()
		{
			_player = GetComponentInParent<Player>();
			_insideOfs = new InsideOfs(Allocator.Persistent);
			_physicsEnv[0] = new PhysicsEnv(NowUsec, GetComponentInChildren<PlayfieldComponent>(), GravityStrength);
		}

		private void Start()
		{
			// create static octree
			var sw = Stopwatch.StartNew();
			var colliderItems = GetComponentsInChildren<ICollidableComponent>();
			Debug.Log($"Found {colliderItems.Length} collidable items.");
			var colliders = new ColliderReference(Allocator.TempJob);
			var kinematicColliders = new ColliderReference(Allocator.TempJob, true);
			foreach (var colliderItem in colliderItems) {
				if (!colliderItem.IsCollidable) {
					_disabledCollisionItems.Add(colliderItem.ItemId);
				}
				colliderItem.GetColliders(_player, ref colliders, ref kinematicColliders, 0);
			}

			// allocate colliders
			_colliders = new NativeColliders(ref colliders, Allocator.Persistent);

			// create octree
			var elapsedMs = sw.Elapsed.TotalMilliseconds;
			var playfieldBounds = GetComponentInChildren<PlayfieldComponent>().Bounds;
			_playfieldBounds = GetComponentInChildren<PlayfieldComponent>().Bounds;
			_octree = new NativeOctree<int>(playfieldBounds, 1024, 10, Allocator.Persistent);

			sw.Restart();
			var populateJob = new PhysicsPopulateJob {
				Colliders = _colliders,
				Octree = _octree,
			};
			populateJob.Run();
			_octree = populateJob.Octree;
			Debug.Log($"Octree of {_colliders.Length} constructed (colliders: {elapsedMs}ms, tree: {sw.Elapsed.TotalMilliseconds}ms).");

			// get balls
			var balls = GetComponentsInChildren<BallComponent>();
			foreach (var ball in balls) {
				Register(ball);
			}
		}

		private void Update()
		{
			// prepare job
			var events = _eventQueue.AsParallelWriter();
			var updatePhysics = new PhysicsUpdateJob {
				InitialTimeUsec = NowUsec,
				DeltaTimeMs = Time.deltaTime * 1000,
				PhysicsEnv = _physicsEnv,
				Octree = _octree,
				Colliders = _colliders,
				InsideOfs = _insideOfs,
				Events = events,
				Balls = _ballStates,
				BumperStates = _bumperStates,
				DropTargetStates = _dropTargetStates,
				FlipperStates = _flipperStates,
				GateStates = _gateStates,
				HitTargetStates = _hitTargetStates,
				KickerStates = _kickerStates,
				PlungerStates = _plungerStates,
				SpinnerStates = _spinnerStates,
				SurfaceStates = _surfaceStates,
				TriggerStates = _triggerStates,
				DisabledCollisionItems = _disabledCollisionItems,
				PlayfieldBounds = _playfieldBounds,
				OverlappingColliders = new NativeParallelHashSet<int>(0, Allocator.TempJob)
			};

			var env = _physicsEnv[0];
			var state = new PhysicsState(ref env, ref _octree, ref _colliders, ref events, ref _insideOfs, ref _ballStates,
				ref _bumperStates, ref _dropTargetStates, ref _flipperStates, ref _gateStates,
				ref _hitTargetStates, ref _kickerStates, ref _plungerStates, ref _spinnerStates,
				ref _surfaceStates, ref _triggerStates, ref _disabledCollisionItems, ref _swapBallCollisionHandling);

			// process input
			while (_inputActions.Count > 0) {
				var action = _inputActions.Dequeue();
				action(ref state);
			}

			// run physics loop
			updatePhysics.Run();

			// dequeue events
			while (_eventQueue.TryDequeue(out var eventData)) {
				_player.OnEvent(in eventData);
			}

			// process scheduled events from managed land
			lock (_scheduledActions) {
				for (var i = _scheduledActions.Count - 1; i >= 0; i--) {
					if (_physicsEnv[0].CurPhysicsFrameTime > _scheduledActions[i].ScheduleAt) {
						_scheduledActions[i].Action();
						_scheduledActions.RemoveAt(i);
					}
				}
			}

			// retrieve updated data
			_ballStates = updatePhysics.Balls;
			_physicsEnv = updatePhysics.PhysicsEnv;
			_flipperStates = updatePhysics.FlipperStates;

			#region Movements

			// balls
			using (var enumerator = state.Balls.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var ball = ref enumerator.Current.Value;
					BallMovementPhysics.Move(ball, _transforms[ball.Id]);
				}
			}

			// flippers
			using (var enumerator = _flipperStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var flipperState = ref enumerator.Current.Value;
					var flipperTransform = _transforms[enumerator.Current.Key];
					flipperTransform.localRotation = quaternion.Euler(0, flipperState.Movement.Angle, 0);
				}
			}

			// bumpers
			using (var enumerator = _bumperStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var bumperState = ref enumerator.Current.Value;
					if (bumperState.SkirtItemId != 0) {
						BumperTransform.UpdateSkirt(in bumperState.SkirtAnimation, _transforms[bumperState.SkirtItemId]);
					}
					if (bumperState.RingItemId != 0) {
						BumperTransform.UpdateRing(bumperState.RingItemId, in bumperState.RingAnimation, _transforms[bumperState.RingItemId]);
					}
				}
			}

			// drop targets
			using (var enumerator = _dropTargetStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var dropTargetState = ref enumerator.Current.Value;
					var dropTargetTransform = _transforms[dropTargetState.AnimatedItemId];
					var localPos = dropTargetTransform.localPosition;
					dropTargetTransform.localPosition = new Vector3(
						localPos.x,
						Physics.ScaleToWorld(dropTargetState.Animation.ZOffset),
						localPos.z
					);
				}
			}

			// hit targets
			using (var enumerator = _hitTargetStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var hitTargetState = ref enumerator.Current.Value;
					var hitTargetTransform = _transforms[hitTargetState.AnimatedItemId];
					var localRot = hitTargetTransform.localEulerAngles;
					hitTargetTransform.localEulerAngles = new Vector3(
						hitTargetState.Animation.XRotation,
						localRot.y,
						localRot.z
					);
				}
			}

			// gates
			using (var enumerator = _gateStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var gateState = ref enumerator.Current.Value;
					var gateTransform = _transforms[gateState.WireItemId];
					gateTransform.localRotation = quaternion.RotateX(-gateState.Movement.Angle);
				}
			}

			// plungers
			using (var enumerator = _plungerStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var plungerState = ref enumerator.Current.Value;
					foreach (var skinnedMeshRenderer in _skinnedMeshRenderers[enumerator.Current.Key]) {
						skinnedMeshRenderer.SetBlendShapeWeight(0, plungerState.Animation.Position);
					}
				}
			}

			// spinners
			using (var enumerator = _spinnerStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var spinnerState = ref enumerator.Current.Value;
					var spinnerTransform = _transforms[spinnerState.AnimationItemId];
					spinnerTransform.localRotation = quaternion.RotateX(-spinnerState.Movement.Angle);
				}
			}

			// triggers
			using (var enumerator = _triggerStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var triggerState = ref enumerator.Current.Value;
					if (triggerState.AnimatedItemId == 0) {
						continue;
					}
					var triggerTransform = _transforms[triggerState.AnimatedItemId];
					TriggerTransform.Update(triggerState.AnimatedItemId, in triggerState.Movement, triggerTransform);
				}
			}

			#endregion
		}
		
		private void OnDestroy()
		{
			_physicsEnv.Dispose();
			_eventQueue.Dispose();
			_ballStates.Dispose();
			_colliders.Dispose();
			_insideOfs.Dispose();
			_octree.Dispose();
			_bumperStates.Dispose();
			_dropTargetStates.Dispose();
			_flipperStates.Dispose();
			_gateStates.Dispose();
			_hitTargetStates.Dispose();
			using (var enumerator = _kickerStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			_kickerStates.Dispose();
			_plungerStates.Dispose();
			_spinnerStates.Dispose();
			_surfaceStates.Dispose();
			using (var enumerator = _triggerStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			_triggerStates.Dispose();
			_disabledCollisionItems.Dispose();
		}

		#endregion

		private class ScheduledAction
		{
			public readonly ulong ScheduleAt;
			public readonly Action Action;

			public ScheduledAction(ulong scheduleAt, Action action)
			{
				ScheduleAt = scheduleAt;
				Action = action;
			}
		}
	}
}
