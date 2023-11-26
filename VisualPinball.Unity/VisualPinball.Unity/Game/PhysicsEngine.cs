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
using Random = Unity.Mathematics.Random;

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
		[NonSerialized] private NativeColliders _kinematicColliders;
		[NonSerialized] private NativeColliders _kinematicCollidersAtIdentity;
		[NonSerialized] private NativeParallelHashMap<int, NativeColliderIds> _kinematicColliderLookups;
		[NonSerialized] private readonly LazyInit<NativeArray<PhysicsEnv>> _physicsEnv = new(() =>new(1, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeQueue<EventData>> _eventQueue = new(() => new(Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, BallState>> _ballStates = new(() => new (0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, BumperState>> _bumperStates = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, FlipperState>> _flipperStates = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, GateState>> _gateStates = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, DropTargetState>>_dropTargetStates = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, HitTargetState>> _hitTargetStates = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, KickerState>> _kickerStates = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, PlungerState>> _plungerStates = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, SpinnerState>> _spinnerStates = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, SurfaceState>> _surfaceStates = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, TriggerState>> _triggerStates = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashSet<int>> _disabledCollisionItems = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private bool _swapBallCollisionHandling;

		#endregion

		#region Transforms

		[NonSerialized] private readonly Dictionary<int, Transform> _transforms = new();
		[NonSerialized] private LazyInit<NativeParallelHashMap<int, float4x4>> _kinematicTransforms = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private LazyInit<NativeParallelHashMap<int, float4x4>> _updatedKinematicTransforms = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private LazyInit<NativeParallelHashMap<int, float4x4>> _nonTransformableColliderMatrices = new(() => new(0, Allocator.Persistent));
		[NonSerialized] private readonly Dictionary<int, SkinnedMeshRenderer[]> _skinnedMeshRenderers = new();

		[NonSerialized] private readonly Dictionary<int, IRotatableAnimationComponent> _rotatableComponent = new();

		#endregion

		[NonSerialized] private readonly Queue<InputAction> _inputActions = new();
		[NonSerialized] private readonly List<ScheduledAction> _scheduledActions = new();

		[NonSerialized] private Player _player;
		[NonSerialized] private IKinematicColliderComponent[] _kinematicColliderComponents;


		private static ulong NowUsec => (ulong)(Time.timeAsDouble * 1000000);

		#region API

		public void ScheduleAction(int timeoutMs, Action action) => ScheduleAction((uint)timeoutMs, action);
		public void ScheduleAction(uint timeoutMs, Action action)
		{
			lock (_scheduledActions) {
				_scheduledActions.Add(new ScheduledAction(_physicsEnv.Ref[0].CurPhysicsFrameTime + (ulong)timeoutMs * 1000, action));
			}
		}

		internal delegate void InputAction(ref PhysicsState state);

		internal ref NativeParallelHashMap<int, BallState> Balls => ref _ballStates.Ref;
		internal ref InsideOfs InsideOfs => ref _insideOfs;
		internal NativeQueue<EventData>.ParallelWriter EventQueue => _eventQueue.Ref.AsParallelWriter();

		internal void Schedule(InputAction action) => _inputActions.Enqueue(action);
		internal ref BallState BallState(int ballId) => ref _ballStates.Ref.GetValueByRef(ballId);
		internal ref BumperState BumperState(int itemId) => ref _bumperStates.Ref.GetValueByRef(itemId);
		internal ref FlipperState FlipperState(int itemId) => ref _flipperStates.Ref.GetValueByRef(itemId);
		internal ref GateState GateState(int itemId) => ref _gateStates.Ref.GetValueByRef(itemId);
		internal ref DropTargetState DropTargetState(int itemId) => ref _dropTargetStates.Ref.GetValueByRef(itemId);
		internal ref HitTargetState HitTargetState(int itemId) => ref _hitTargetStates.Ref.GetValueByRef(itemId);
		internal ref KickerState KickerState(int itemId) => ref _kickerStates.Ref.GetValueByRef(itemId);
		internal ref PlungerState PlungerState(int itemId) => ref _plungerStates.Ref.GetValueByRef(itemId);
		internal ref SpinnerState SpinnerState(int itemId) => ref _spinnerStates.Ref.GetValueByRef(itemId);
		internal ref SurfaceState SurfaceState(int itemId) => ref _surfaceStates.Ref.GetValueByRef(itemId);
		internal ref TriggerState TriggerState(int itemId) => ref _triggerStates.Ref.GetValueByRef(itemId);
		internal void SetBallInsideOf(int ballId, int itemId) => _insideOfs.SetInsideOf(itemId, ballId);
		internal uint TimeMsec => _physicsEnv.Ref[0].TimeMsec;
		internal Random Random => _physicsEnv.Ref[0].Random;
		internal void Register<T>(T item) where T : MonoBehaviour
		{
			var go = item.gameObject;
			var itemId = go.GetInstanceID();
			_transforms.TryAdd(itemId, go.transform);

			// states
			switch (item) {
				case BallComponent c:
					if (!_ballStates.Ref.ContainsKey(itemId)) {
						_ballStates.Ref[itemId] = c.CreateState();
					}
					break;
				case BumperComponent c: _bumperStates.Ref[itemId] = c.CreateState(); break;
				case FlipperComponent c: _flipperStates.Ref[itemId] = c.CreateState(); break;
				case GateComponent c: _gateStates.Ref[itemId] = c.CreateState(); break;
				case DropTargetComponent c: _dropTargetStates.Ref[itemId] = c.CreateState(); break;
				case HitTargetComponent c: _hitTargetStates.Ref[itemId] = c.CreateState(); break;
				case KickerComponent c: _kickerStates.Ref[itemId] = c.CreateState(); break;
				case PlungerComponent c:
					_plungerStates.Ref[itemId] = c.CreateState();
					_skinnedMeshRenderers[itemId] = c.GetComponentsInChildren<SkinnedMeshRenderer>();
					break;
				case SpinnerComponent c: _spinnerStates.Ref[itemId] = c.CreateState(); break;
				case SurfaceComponent c: _surfaceStates.Ref[itemId] = c.CreateState(); break;
				case TriggerComponent c: _triggerStates.Ref[itemId] = c.CreateState(); break;
			}

			// animations
			if (item is IRotatableAnimationComponent rotatableComponent) {
				_rotatableComponent.TryAdd(itemId, rotatableComponent);
			}
		}

		internal Transform UnregisterBall(int ballId)
		{
			var transform = _transforms[ballId];
			_transforms.Remove(ballId);
			_ballStates.Ref.Remove(ballId);
			_insideOfs.SetOutsideOfAll(ballId);
			return transform;
		}

		internal void EnableCollider(int itemId)
		{
			if (_disabledCollisionItems.Ref.Contains(itemId)) {
				_disabledCollisionItems.Ref.Remove(itemId);
			}
		}
		internal void DisableCollider(int itemId)
		{
			if (!_disabledCollisionItems.Ref.Contains(itemId)) {
				_disabledCollisionItems.Ref.Add(itemId);
			}
		}

		#endregion

		#region Event Functions

		private void Awake()
		{
			_player = GetComponentInParent<Player>();
			_insideOfs = new InsideOfs(Allocator.Persistent);
			_physicsEnv.Ref[0] = new PhysicsEnv(NowUsec, GetComponentInChildren<PlayfieldComponent>(), GravityStrength);
			_kinematicColliderComponents = GetComponentsInChildren<IKinematicColliderComponent>();
		}

		private void Start()
		{
			// create static octree
			var sw = Stopwatch.StartNew();
			var playfield = GetComponentInChildren<PlayfieldComponent>();

			var colliderItems = GetComponentsInChildren<ICollidableComponent>();
			Debug.Log($"Found {colliderItems.Length} collidable items.");
			var colliders = new ColliderReference(Allocator.Temp);
			var kinematicColliders = new ColliderReference(Allocator.Temp, true);
			foreach (var colliderItem in colliderItems) {
				if (!colliderItem.IsCollidable) {
					_disabledCollisionItems.Ref.Add(colliderItem.ItemId);
				}

				var translateWithinPlayfieldMatrix = colliderItem is ICollidableNonTransformableComponent nonTransformableColliderItem
					? nonTransformableColliderItem.TranslateWithinPlayfieldMatrix(playfield.transform.worldToLocalMatrix)
					: float4x4.identity;
				if (colliderItem is ICollidableNonTransformableComponent) {
					_nonTransformableColliderMatrices.Ref[colliderItem.ItemId] = translateWithinPlayfieldMatrix;
				}

				colliderItem.GetColliders(_player, this, ref colliders, ref kinematicColliders, translateWithinPlayfieldMatrix, 0);
			}

			// allocate colliders
			_colliders = new NativeColliders(ref colliders, Allocator.Persistent);
			_kinematicColliders = new NativeColliders(ref kinematicColliders, Allocator.Persistent);

			// get kinetic collider matrices
			foreach (var coll in _kinematicColliderComponents) {
				_kinematicTransforms.Ref[coll.ItemId] = coll.TransformationWithinPlayfield;
			}
			_kinematicColliderLookups = kinematicColliders.CreateLookup(Allocator.Persistent);

			// create identity kinematic colliders
			kinematicColliders.TransformToIdentity(_kinematicTransforms.Ref);
			_kinematicCollidersAtIdentity = new NativeColliders(ref kinematicColliders, Allocator.Persistent);

			// create octree
			var elapsedMs = sw.Elapsed.TotalMilliseconds;
			_playfieldBounds = playfield.Bounds;
			_octree = new NativeOctree<int>(_playfieldBounds, 1024, 10, Allocator.Persistent);

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
			// check for updated kinematic transforms
			_updatedKinematicTransforms.Ref.Clear();
			foreach (var coll in _kinematicColliderComponents) {
				if (!coll.IsKinematic) { // kinematic enabled?
					continue;
				}
				var lastTransformationMatrix = _kinematicTransforms.Ref[coll.ItemId];
				var currTransformationMatrix = coll.TransformationWithinPlayfield;
				if (lastTransformationMatrix.Equals(currTransformationMatrix)) {
					continue;
				}
				_updatedKinematicTransforms.Ref.Add(coll.ItemId, currTransformationMatrix);
				_kinematicTransforms.Ref[coll.ItemId] = currTransformationMatrix;
			}

			// prepare job
			var events = _eventQueue.Ref.AsParallelWriter();
			using var overlappingColliders = new NativeParallelHashSet<int>(0, Allocator.TempJob);

			var updatePhysics = new PhysicsUpdateJob {
				InitialTimeUsec = NowUsec,
				DeltaTimeMs = Time.deltaTime * 1000,
				PhysicsEnv = _physicsEnv.Ref,
				Octree = _octree,
				Colliders = _colliders,
				KinematicColliders = _kinematicColliders,
				KinematicCollidersAtIdentity = _kinematicCollidersAtIdentity,
				KinematicColliderLookups = _kinematicColliderLookups,
				UpdatedKinematicTransforms = _updatedKinematicTransforms.Ref,
				NonTransformableColliderMatrices = _nonTransformableColliderMatrices.Ref,
				InsideOfs = _insideOfs,
				Events = events,
				Balls = _ballStates.Ref,
				BumperStates = _bumperStates.Ref,
				DropTargetStates = _dropTargetStates.Ref,
				FlipperStates = _flipperStates.Ref,
				GateStates = _gateStates.Ref,
				HitTargetStates = _hitTargetStates.Ref,
				KickerStates = _kickerStates.Ref,
				PlungerStates = _plungerStates.Ref,
				SpinnerStates = _spinnerStates.Ref,
				SurfaceStates = _surfaceStates.Ref,
				TriggerStates = _triggerStates.Ref,
				DisabledCollisionItems = _disabledCollisionItems.Ref,
				PlayfieldBounds = _playfieldBounds,
				OverlappingColliders = overlappingColliders,
			};

			var env = _physicsEnv.Ref[0];
			var state = new PhysicsState(ref env, ref _octree, ref _colliders, ref _kinematicColliders,
				ref _kinematicCollidersAtIdentity, ref _updatedKinematicTransforms.Ref, ref _nonTransformableColliderMatrices.Ref,
				ref _kinematicColliderLookups, ref events,
				ref _insideOfs, ref _ballStates.Ref, ref _bumperStates.Ref, ref _dropTargetStates.Ref, ref _flipperStates.Ref, ref _gateStates.Ref,
				ref _hitTargetStates.Ref, ref _kickerStates.Ref, ref _plungerStates.Ref, ref _spinnerStates.Ref,
				ref _surfaceStates.Ref, ref _triggerStates.Ref, ref _disabledCollisionItems.Ref, ref _swapBallCollisionHandling);

			// process input
			while (_inputActions.Count > 0) {
				var action = _inputActions.Dequeue();
				action(ref state);
			}

			// run physics loop
			updatePhysics.Run();

			// dequeue events
			while (_eventQueue.Ref.TryDequeue(out var eventData)) {
				_player.OnEvent(in eventData);
			}

			// process scheduled events from managed land
			lock (_scheduledActions) {
				for (var i = _scheduledActions.Count - 1; i >= 0; i--) {
					if (_physicsEnv.Ref[0].CurPhysicsFrameTime > _scheduledActions[i].ScheduleAt) {
						_scheduledActions[i].Action();
						_scheduledActions.RemoveAt(i);
					}
				}
			}

			#region Movements

			// balls
			using (var enumerator = state.Balls.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var ball = ref enumerator.Current.Value;
					BallMovementPhysics.Move(ball, _transforms[ball.Id]);
				}
			}

			// flippers
			using (var enumerator = _flipperStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var flipperState = ref enumerator.Current.Value;
					var flipperTransform = _transforms[enumerator.Current.Key];
					flipperTransform.localRotation = quaternion.Euler(0, flipperState.Movement.Angle, 0);
				}
			}

			// bumpers
			using (var enumerator = _bumperStates.Ref.GetEnumerator()) {
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
			using (var enumerator = _dropTargetStates.Ref.GetEnumerator()) {
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
			using (var enumerator = _hitTargetStates.Ref.GetEnumerator()) {
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
			using (var enumerator = _gateStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var gateState = ref enumerator.Current.Value;
					var component = _rotatableComponent[enumerator.Current.Key];
					component.OnRotationUpdated(gateState.Movement.Angle);
				}
			}

			// plungers
			using (var enumerator = _plungerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var plungerState = ref enumerator.Current.Value;
					foreach (var skinnedMeshRenderer in _skinnedMeshRenderers[enumerator.Current.Key]) {
						skinnedMeshRenderer.SetBlendShapeWeight(0, plungerState.Animation.Position);
					}
				}
			}

			// spinners
			using (var enumerator = _spinnerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var spinnerState = ref enumerator.Current.Value;
					var component = _rotatableComponent[enumerator.Current.Key];
					component.OnRotationUpdated(spinnerState.Movement.Angle);
				}
			}

			// triggers
			using (var enumerator = _triggerStates.Ref.GetEnumerator()) {
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

			overlappingColliders.Dispose();
		}
		
		private void OnDestroy()
		{
			_physicsEnv.Ref.Dispose();
			_eventQueue.Ref.Dispose();
			_ballStates.Ref.Dispose();
			_colliders.Dispose();
			_kinematicColliders.Dispose();
			_insideOfs.Dispose();
			_octree.Dispose();
			_bumperStates.Ref.Dispose();
			_dropTargetStates.Ref.Dispose();
			_flipperStates.Ref.Dispose();
			_gateStates.Ref.Dispose();
			_hitTargetStates.Ref.Dispose();
			using (var enumerator = _kickerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			_kickerStates.Ref.Dispose();
			_plungerStates.Ref.Dispose();
			_spinnerStates.Ref.Dispose();
			_surfaceStates.Ref.Dispose();
			using (var enumerator = _triggerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			_triggerStates.Ref.Dispose();
			_disabledCollisionItems.Ref.Dispose();
			_kinematicTransforms.Ref.Dispose();
			_updatedKinematicTransforms.Ref.Dispose();
			using (var enumerator = _kinematicColliderLookups.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			_kinematicColliderLookups.Dispose();

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

		public ICollider[] GetKinematicColliders(int itemId)
		{
			ref var colliderIds = ref _kinematicColliderLookups.GetValueByRef(itemId);
			var colliders = new ICollider[colliderIds.Length];
			for (var i = 0; i < colliderIds.Length; i++) {
				var colliderId = colliderIds[i];
				colliders[i] = _kinematicColliders[colliderId];
			}
			return colliders;
		}
	}
}
